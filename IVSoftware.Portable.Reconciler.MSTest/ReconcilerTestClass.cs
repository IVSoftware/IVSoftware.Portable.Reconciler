using IVSoftware.Portable.Disposable;
using IVSoftware.WinOS.MSTest.Extensions;
using Newtonsoft.Json;
using SQLite;
using System.Collections;
using System.Diagnostics;
using System.Reflection.Metadata.Ecma335;
using static IVSoftware.Portable.Reconciler;
using static IVSoftware.WinOS.MSTest.Extensions.Static;

namespace IVSoftware.Portable.Static.Reconciler.MSTest
{
    [TestClass]
    public class ReconcilerTestClass
    {
        #region S T A B L E

        #region C O M M O N
        /// <summary>
        /// Creates a new MockDatabaseRecord with a unique UID and a default timestamp.
        /// </summary>
        static MockDatabaseRecord internalGetNewRecord()
        {
            var i = autoIncrement++;
            return new MockDatabaseRecord
            {
                Uid = $"ID{i:D2}",
                TimeStamp = EpochTime,
                Description = $"Record {i:D2}",
                Properties = new Dictionary<string, object>
                {
                    { "InitialValue", i
                    }
                },
                IsModified = false, // Reset because property changes make record dirty.
            };
        }

        /// <summary>
        /// Removes a random record from the specified target list and returns its UID.
        /// </summary>
        string internalRemoveRandom(IList target)
        {
            var index = rando.Next(target.Count);
            var remove = listA[index];
            target.Remove(remove);
            return remove.Uid;
        }

        /// <summary>
        /// Modifies the timestamp of a random record in the specified target list and returns the UID and modified timestamp.
        /// </summary>
        (string uid, DateTime timeStamp) internalModifyRandom(IList target)
        {
            var index = rando.Next(target.Count);
            var modify = target.OfType<MockDatabaseRecord>().ToArray()[index];
            var timeStamp = modify.TimeStamp.AddSeconds(10000 * rando.NextDouble());
            modify.TimeStamp = timeStamp;

            return (modify.Uid, timeStamp);
        }

        const int
          SAMPLE = 10,
          CHANGED_THRESHOLD = 2,
          BOTH = SAMPLE - 1;
        MockDatabaseRecord[] internalApplyWeightedOdds(List<MockDatabaseRecord> target)
        {
            var builder = new List<MockDatabaseRecord>();
            int mode;
            foreach (var record in target)
            {
                mode = rando.Next(SAMPLE);
                Debug.WriteLineIf(false, $"{nameof(internalMakeWeightedRandomChanges)}: MODE={mode} IsMax ={mode == BOTH}");
                switch (mode)
                {
                    case int _ when mode <= CHANGED_THRESHOLD:
                        builder.Add(record);
                        break;
                    case BOTH:
                        var uidToChange = listA[rando.Next(listA.Count)].Uid;
                        if (listA.FirstOrDefault(_ => _.Uid == uidToChange) is MockDatabaseRecord a &&
                            listB.FirstOrDefault(_ => _.Uid == uidToChange) is MockDatabaseRecord b)
                        {
                            builder.AddRange([a, b]);
                        }
                        break;
                    default:
                        break;
                }
            }
            return builder.ToArray();
        }

        void internalMakeWeightedRandomChanges()
        {
            var recordsToChange = new List<MockDatabaseRecord>();

            recordsToChange.AddRange(internalApplyWeightedOdds(listA));
            recordsToChange.AddRange(internalApplyWeightedOdds(listB));

            foreach (var apply in recordsToChange)
            {
                apply.TimeStamp = EpochTime.AddSeconds(10000 * rando.NextDouble());
                apply.IsModified = true;
            }
        }

        /// <summary>
        /// Provides a simplified and convenient wrapper for accessing Reconciled<MockDatabaseRecord>.DefaultExec,
        /// a mutable delegate used to apply reconciliation updates with loopback in test and production scenarios.
        /// The action upon invoking the delegate can vary from test to test, so it’s essential to check the assignment
        /// at the start of each test.
        ///
        /// Example assignment:
        /// Reconciled<MockDatabaseRecord>.DefaultExec = () => Reconcile(
        ///     listA,
        ///     listB,
        ///     uidSorter: (a, b) => (CompareUIDResult)a.Uid.CompareTo(b.Uid),
        ///     versionComparer: (a, b) => (CompareVersionResult)a.TimeStamp.CompareTo(b.TimeStamp));
        /// </summary>
        private Reconciled<MockDatabaseRecord> DefaultExec() =>
            (Reconciled<MockDatabaseRecord>)Reconciled.DefaultExec.Invoke();
        #endregion C O M M O N

        [ClassInitialize]
        public static void Initialize(TestContext context)
        {
        }
        private static readonly DateTime EpochTime = new DateTime(2010, 3, 9, 0, 0, 0);
        internal Random rando { get; set; } = new Random(1);
        static int autoIncrement { get; set; } = 1;
        string? actual, expected, expr;

        DisposableApplyContext DHOST_APPLYCONTEXT = Reconciled<MockDatabaseRecord>.DHostApplyContext;

        List<MockDatabaseRecord>
            listA,
            listB;

        [TestInitialize]
        public void OnInitTest()
        {
            rando = new Random(1);
            autoIncrement = 1;
            actual = expected = expr = null;
            listA = Enumerable.Range(1, 12).Select(_ => internalGetNewRecord()).ToList();
            listB = new List<MockDatabaseRecord>();
        }

        [TestCleanup]
        public void Cleanup()
        {
        }

        /// <summary>
        /// Verifies that the DisposableExecutionHost context has initialized properties
        /// and verifies that reconciliation modes can be pushed and popped.
        /// </summary>
        [TestMethod]
        public void TestDisposableExecutionHost()
        {
            var uut = Reconciled<MockDatabaseRecord>.DHostApplyContext;
            Assert.AreEqual(
                uut.DefaultMode,
                ReconciliationMode.Append,
                $"Expecting that, as tests run, this will stay the default value for the class");
            Assert.IsTrue(uut.IsZero());
            Assert.AreEqual(
                uut.DefaultMode,
                uut.Mode,
                $"Expecting identity while count IsZero()");
            using (uut.GetToken(ReconciliationMode.Trim))
            {
                Assert.AreEqual(
                    ReconciliationMode.Trim,
                    uut.Mode,
                    $"Expecting pushed value.");
                using (uut.GetToken(ReconciliationMode.TakeA))
                {
                    Assert.AreEqual(
                        ReconciliationMode.TakeA,
                        uut.Mode,
                        $"Expecting pushed value.");
                    using (uut.GetToken(ReconciliationMode.TakeB))
                    {
                        Assert.AreEqual(
                            ReconciliationMode.TakeB,
                            uut.Mode,
                            $"Expecting pushed value.");
                    }
                    Assert.AreEqual(
                        ReconciliationMode.TakeA,
                        uut.Mode,
                        $"Expecting popped value.");
                }
                Assert.AreEqual(
                    ReconciliationMode.Trim,
                    uut.Mode,
                    $"Expecting popped value.");
            }
            Assert.AreEqual(
                uut.DefaultMode,
                ReconciliationMode.Append,
                $"Expecting that, as tests run, this will stay the default value for the class");
            Assert.IsTrue(uut.IsZero());
            Assert.AreEqual(
                uut.DefaultMode,
                uut.Mode,
                $"Expecting identity while count IsZero()");

            Assert.IsNull(uut.SrceA);
            Assert.IsNull(uut.SrceB);
            using (uut.GetToken(listA, listB))
            {
                Assert.IsTrue(ReferenceEquals(listA, uut.SrceA));
                Assert.IsTrue(ReferenceEquals(listB, uut.SrceB));
                Assert.IsFalse(ReferenceEquals(listA, uut.SrceB));
                Assert.IsFalse(ReferenceEquals(listB, uut.SrceA));
            }
            Assert.IsNull(uut.SrceA);
            Assert.IsNull(uut.SrceB);
        }

        /// <summary>
        /// Calls the V1 signature without any modes.
        /// Tests the Reconcile method (version 1) by setting up scenarios for various
        /// record categories: Equal, OnlyInA, OnlyInB, NewerInA, NewerInB.
        /// </summary>
        /// <remarks>
        /// Uses the V1 Reconcile call exclusively, although the new data structures 
        /// in Reconciled.V2 are accessedand exercised extensively in the course of testing.
        /// </remarks>
        [TestMethod]
        public void TestReconcileMethod_V1()
        {
            Reconciled.DefaultExec = () => Reconcile(
                    listA,
                    listB,
                    uidSorter: (a, b) => (CompareUIDResult)a.Uid.CompareTo(b.Uid),
                    versionComparer: (a, b) => (CompareVersionResult)a.TimeStamp.CompareTo(b.TimeStamp));

            Reconciled<MockDatabaseRecord> reconciled;

            reconciled = DefaultExec();

#if false
            // These are still in there, just internal now.
            Assert.IsTrue(ReferenceEquals(listA, reconciled._srceA));
            Assert.IsTrue(ReferenceEquals(listB, reconciled._srceB));
#endif

            // Initial comparison of listA and empty listB
            actual = reconciled.ToString();
            actual.ToClipboard();
            actual.ToClipboardAssert("Expecting initialized 12 OnlyInA values with default Epoch.");
            { }

            expected = @" 
Equal

OnlyInA
UID: ID01, TimeStamp: 03/09/2010 00:00:00, Description: Record 01 modified=False
UID: ID02, TimeStamp: 03/09/2010 00:00:00, Description: Record 02 modified=False
UID: ID03, TimeStamp: 03/09/2010 00:00:00, Description: Record 03 modified=False
UID: ID04, TimeStamp: 03/09/2010 00:00:00, Description: Record 04 modified=False
UID: ID05, TimeStamp: 03/09/2010 00:00:00, Description: Record 05 modified=False
UID: ID06, TimeStamp: 03/09/2010 00:00:00, Description: Record 06 modified=False
UID: ID07, TimeStamp: 03/09/2010 00:00:00, Description: Record 07 modified=False
UID: ID08, TimeStamp: 03/09/2010 00:00:00, Description: Record 08 modified=False
UID: ID09, TimeStamp: 03/09/2010 00:00:00, Description: Record 09 modified=False
UID: ID10, TimeStamp: 03/09/2010 00:00:00, Description: Record 10 modified=False
UID: ID11, TimeStamp: 03/09/2010 00:00:00, Description: Record 11 modified=False
UID: ID12, TimeStamp: 03/09/2010 00:00:00, Description: Record 12 modified=False
OnlyInB

NewerInA

NewerInB

";
            Assert.AreEqual(
                expected.NormalizeResult(),
                actual.NormalizeResult(),
                "Expecting 12 OnlyInA values with default Epoch."
            );

            // Adding cloned records from listA to listB, making them equal.
            reconciled++;

            actual = reconciled.ToString();
            actual.ToClipboard();
            actual.ToClipboardAssert("Expecting records are Equal, based on the comparers (not on Equals)");
            { }
            expected = @" 
Equal
UID: ID01, TimeStamp: 03/09/2010 00:00:00, Description: Record 01 modified=False
UID: ID01, TimeStamp: 03/09/2010 00:00:00, Description: Record 01 modified=False
UID: ID02, TimeStamp: 03/09/2010 00:00:00, Description: Record 02 modified=False
UID: ID02, TimeStamp: 03/09/2010 00:00:00, Description: Record 02 modified=False
UID: ID03, TimeStamp: 03/09/2010 00:00:00, Description: Record 03 modified=False
UID: ID03, TimeStamp: 03/09/2010 00:00:00, Description: Record 03 modified=False
UID: ID04, TimeStamp: 03/09/2010 00:00:00, Description: Record 04 modified=False
UID: ID04, TimeStamp: 03/09/2010 00:00:00, Description: Record 04 modified=False
UID: ID05, TimeStamp: 03/09/2010 00:00:00, Description: Record 05 modified=False
UID: ID05, TimeStamp: 03/09/2010 00:00:00, Description: Record 05 modified=False
UID: ID06, TimeStamp: 03/09/2010 00:00:00, Description: Record 06 modified=False
UID: ID06, TimeStamp: 03/09/2010 00:00:00, Description: Record 06 modified=False
UID: ID07, TimeStamp: 03/09/2010 00:00:00, Description: Record 07 modified=False
UID: ID07, TimeStamp: 03/09/2010 00:00:00, Description: Record 07 modified=False
UID: ID08, TimeStamp: 03/09/2010 00:00:00, Description: Record 08 modified=False
UID: ID08, TimeStamp: 03/09/2010 00:00:00, Description: Record 08 modified=False
UID: ID09, TimeStamp: 03/09/2010 00:00:00, Description: Record 09 modified=False
UID: ID09, TimeStamp: 03/09/2010 00:00:00, Description: Record 09 modified=False
UID: ID10, TimeStamp: 03/09/2010 00:00:00, Description: Record 10 modified=False
UID: ID10, TimeStamp: 03/09/2010 00:00:00, Description: Record 10 modified=False
UID: ID11, TimeStamp: 03/09/2010 00:00:00, Description: Record 11 modified=False
UID: ID11, TimeStamp: 03/09/2010 00:00:00, Description: Record 11 modified=False
UID: ID12, TimeStamp: 03/09/2010 00:00:00, Description: Record 12 modified=False
UID: ID12, TimeStamp: 03/09/2010 00:00:00, Description: Record 12 modified=False
OnlyInA

OnlyInB

NewerInA

NewerInB

";

            Assert.AreEqual(
                expected.NormalizeResult(),
                actual.NormalizeResult(),
                "Expecting records are Equal, based on the comparers (not on Equals)"
            );

            // Applying random modifications to both lists
            internalMakeWeightedRandomChanges();

            reconciled = DefaultExec();
            actual = reconciled.ToString();
            actual.ToClipboard();
            actual.ToClipboardAssert("Expecting to see changes in each.");
            { }
            // This output demonstrates where Version1 falls a little short perhaps,
            // because ID02, ID09, and ID11 have all received possibly conflicting
            // modifications, but are reported in NeweInX without complaint.
            expected = @" 
Equal
UID: ID04, TimeStamp: 03/09/2010 00:00:00, Description: Record 04 modified=False
UID: ID04, TimeStamp: 03/09/2010 00:00:00, Description: Record 04 modified=False
UID: ID05, TimeStamp: 03/09/2010 00:00:00, Description: Record 05 modified=False
UID: ID05, TimeStamp: 03/09/2010 00:00:00, Description: Record 05 modified=False
UID: ID06, TimeStamp: 03/09/2010 00:00:00, Description: Record 06 modified=False
UID: ID06, TimeStamp: 03/09/2010 00:00:00, Description: Record 06 modified=False
UID: ID07, TimeStamp: 03/09/2010 00:00:00, Description: Record 07 modified=False
UID: ID07, TimeStamp: 03/09/2010 00:00:00, Description: Record 07 modified=False
UID: ID12, TimeStamp: 03/09/2010 00:00:00, Description: Record 12 modified=False
UID: ID12, TimeStamp: 03/09/2010 00:00:00, Description: Record 12 modified=False
OnlyInA

OnlyInB

NewerInA
a:UID: ID01, TimeStamp: 03/09/2010 00:51:12, Description: Record 01 modified=True
b:UID: ID01, TimeStamp: 03/09/2010 00:00:00, Description: Record 01 modified=False
a:UID: ID02, TimeStamp: 03/09/2010 02:23:07, Description: Record 02 modified=True
b:UID: ID02, TimeStamp: 03/09/2010 00:10:25, Description: Record 02 modified=True
a:UID: ID10, TimeStamp: 03/09/2010 01:59:27, Description: Record 10 modified=True
b:UID: ID10, TimeStamp: 03/09/2010 00:00:00, Description: Record 10 modified=False
a:UID: ID11, TimeStamp: 03/09/2010 01:56:32, Description: Record 11 modified=True
b:UID: ID11, TimeStamp: 03/09/2010 01:27:32, Description: Record 11 modified=True
NewerInB
a:UID: ID03, TimeStamp: 03/09/2010 00:00:00, Description: Record 03 modified=False
b:UID: ID03, TimeStamp: 03/09/2010 02:14:23, Description: Record 03 modified=True
a:UID: ID08, TimeStamp: 03/09/2010 00:00:00, Description: Record 08 modified=False
b:UID: ID08, TimeStamp: 03/09/2010 01:25:43, Description: Record 08 modified=True
a:UID: ID09, TimeStamp: 03/09/2010 00:02:16, Description: Record 09 modified=True
b:UID: ID09, TimeStamp: 03/09/2010 02:45:18, Description: Record 09 modified=True
";

            Assert.AreEqual(
                expected.NormalizeResult(),
                actual.NormalizeResult(),
                "Expecting to see changes in each."
            );

            // Adding and removing records from both lists
            reconciled++;

            actual = reconciled.ToString();
            actual.ToClipboard();
            actual.ToClipboardAssert("Expecting reconciled lists.");
            { }
            expected = @" 
Equal
UID: ID01, TimeStamp: 03/09/2010 00:51:12, Description: Record 01 modified=False
UID: ID01, TimeStamp: 03/09/2010 00:51:12, Description: Record 01 modified=False
UID: ID02, TimeStamp: 03/09/2010 02:23:07, Description: Record 02 modified=False
UID: ID02, TimeStamp: 03/09/2010 02:23:07, Description: Record 02 modified=False
UID: ID03, TimeStamp: 03/09/2010 02:14:23, Description: Record 03 modified=False
UID: ID03, TimeStamp: 03/09/2010 02:14:23, Description: Record 03 modified=False
UID: ID04, TimeStamp: 03/09/2010 00:00:00, Description: Record 04 modified=False
UID: ID04, TimeStamp: 03/09/2010 00:00:00, Description: Record 04 modified=False
UID: ID05, TimeStamp: 03/09/2010 00:00:00, Description: Record 05 modified=False
UID: ID05, TimeStamp: 03/09/2010 00:00:00, Description: Record 05 modified=False
UID: ID06, TimeStamp: 03/09/2010 00:00:00, Description: Record 06 modified=False
UID: ID06, TimeStamp: 03/09/2010 00:00:00, Description: Record 06 modified=False
UID: ID07, TimeStamp: 03/09/2010 00:00:00, Description: Record 07 modified=False
UID: ID07, TimeStamp: 03/09/2010 00:00:00, Description: Record 07 modified=False
UID: ID08, TimeStamp: 03/09/2010 01:25:43, Description: Record 08 modified=False
UID: ID08, TimeStamp: 03/09/2010 01:25:43, Description: Record 08 modified=False
UID: ID09, TimeStamp: 03/09/2010 02:45:18, Description: Record 09 modified=False
UID: ID09, TimeStamp: 03/09/2010 02:45:18, Description: Record 09 modified=False
UID: ID10, TimeStamp: 03/09/2010 01:59:27, Description: Record 10 modified=False
UID: ID10, TimeStamp: 03/09/2010 01:59:27, Description: Record 10 modified=False
UID: ID11, TimeStamp: 03/09/2010 01:56:32, Description: Record 11 modified=False
UID: ID11, TimeStamp: 03/09/2010 01:56:32, Description: Record 11 modified=False
UID: ID12, TimeStamp: 03/09/2010 00:00:00, Description: Record 12 modified=False
UID: ID12, TimeStamp: 03/09/2010 00:00:00, Description: Record 12 modified=False
OnlyInA

OnlyInB

NewerInA

NewerInB

";
            Assert.AreEqual(
                expected.NormalizeResult(),
                actual.NormalizeResult(),
                "Expecting reconciled lists."
            );

            listA.Add(internalGetNewRecord());

            reconciled = DefaultExec();

            actual = reconciled.ToString();
            actual.ToClipboard();
            actual.ToClipboardAssert("Expecting new item in OnlyInA.");
            { }
            expected = @" 
Equal
UID: ID01, TimeStamp: 03/09/2010 00:51:12, Description: Record 01 modified=False
UID: ID01, TimeStamp: 03/09/2010 00:51:12, Description: Record 01 modified=False
UID: ID02, TimeStamp: 03/09/2010 02:23:07, Description: Record 02 modified=False
UID: ID02, TimeStamp: 03/09/2010 02:23:07, Description: Record 02 modified=False
UID: ID03, TimeStamp: 03/09/2010 02:14:23, Description: Record 03 modified=False
UID: ID03, TimeStamp: 03/09/2010 02:14:23, Description: Record 03 modified=False
UID: ID04, TimeStamp: 03/09/2010 00:00:00, Description: Record 04 modified=False
UID: ID04, TimeStamp: 03/09/2010 00:00:00, Description: Record 04 modified=False
UID: ID05, TimeStamp: 03/09/2010 00:00:00, Description: Record 05 modified=False
UID: ID05, TimeStamp: 03/09/2010 00:00:00, Description: Record 05 modified=False
UID: ID06, TimeStamp: 03/09/2010 00:00:00, Description: Record 06 modified=False
UID: ID06, TimeStamp: 03/09/2010 00:00:00, Description: Record 06 modified=False
UID: ID07, TimeStamp: 03/09/2010 00:00:00, Description: Record 07 modified=False
UID: ID07, TimeStamp: 03/09/2010 00:00:00, Description: Record 07 modified=False
UID: ID08, TimeStamp: 03/09/2010 01:25:43, Description: Record 08 modified=False
UID: ID08, TimeStamp: 03/09/2010 01:25:43, Description: Record 08 modified=False
UID: ID09, TimeStamp: 03/09/2010 02:45:18, Description: Record 09 modified=False
UID: ID09, TimeStamp: 03/09/2010 02:45:18, Description: Record 09 modified=False
UID: ID10, TimeStamp: 03/09/2010 01:59:27, Description: Record 10 modified=False
UID: ID10, TimeStamp: 03/09/2010 01:59:27, Description: Record 10 modified=False
UID: ID11, TimeStamp: 03/09/2010 01:56:32, Description: Record 11 modified=False
UID: ID11, TimeStamp: 03/09/2010 01:56:32, Description: Record 11 modified=False
UID: ID12, TimeStamp: 03/09/2010 00:00:00, Description: Record 12 modified=False
UID: ID12, TimeStamp: 03/09/2010 00:00:00, Description: Record 12 modified=False
OnlyInA
UID: ID13, TimeStamp: 03/09/2010 00:00:00, Description: Record 13 modified=False
OnlyInB

NewerInA

NewerInB

";
            Assert.AreEqual(
                expected.NormalizeResult(),
                actual.NormalizeResult(),
                "Expecting new item in OnlyInA."
            );
            reconciled++;

            actual = reconciled.ToString();
            actual.ToClipboard();
            actual.ToClipboardAssert("Expecting new record 13 is unmodified equal");
            { }
            expected = @" 
Equal
UID: ID01, TimeStamp: 03/09/2010 00:51:12, Description: Record 01 modified=False
UID: ID01, TimeStamp: 03/09/2010 00:51:12, Description: Record 01 modified=False
UID: ID02, TimeStamp: 03/09/2010 02:23:07, Description: Record 02 modified=False
UID: ID02, TimeStamp: 03/09/2010 02:23:07, Description: Record 02 modified=False
UID: ID03, TimeStamp: 03/09/2010 02:14:23, Description: Record 03 modified=False
UID: ID03, TimeStamp: 03/09/2010 02:14:23, Description: Record 03 modified=False
UID: ID04, TimeStamp: 03/09/2010 00:00:00, Description: Record 04 modified=False
UID: ID04, TimeStamp: 03/09/2010 00:00:00, Description: Record 04 modified=False
UID: ID05, TimeStamp: 03/09/2010 00:00:00, Description: Record 05 modified=False
UID: ID05, TimeStamp: 03/09/2010 00:00:00, Description: Record 05 modified=False
UID: ID06, TimeStamp: 03/09/2010 00:00:00, Description: Record 06 modified=False
UID: ID06, TimeStamp: 03/09/2010 00:00:00, Description: Record 06 modified=False
UID: ID07, TimeStamp: 03/09/2010 00:00:00, Description: Record 07 modified=False
UID: ID07, TimeStamp: 03/09/2010 00:00:00, Description: Record 07 modified=False
UID: ID08, TimeStamp: 03/09/2010 01:25:43, Description: Record 08 modified=False
UID: ID08, TimeStamp: 03/09/2010 01:25:43, Description: Record 08 modified=False
UID: ID09, TimeStamp: 03/09/2010 02:45:18, Description: Record 09 modified=False
UID: ID09, TimeStamp: 03/09/2010 02:45:18, Description: Record 09 modified=False
UID: ID10, TimeStamp: 03/09/2010 01:59:27, Description: Record 10 modified=False
UID: ID10, TimeStamp: 03/09/2010 01:59:27, Description: Record 10 modified=False
UID: ID11, TimeStamp: 03/09/2010 01:56:32, Description: Record 11 modified=False
UID: ID11, TimeStamp: 03/09/2010 01:56:32, Description: Record 11 modified=False
UID: ID12, TimeStamp: 03/09/2010 00:00:00, Description: Record 12 modified=False
UID: ID12, TimeStamp: 03/09/2010 00:00:00, Description: Record 12 modified=False
UID: ID13, TimeStamp: 03/09/2010 00:00:00, Description: Record 13 modified=False
UID: ID13, TimeStamp: 03/09/2010 00:00:00, Description: Record 13 modified=False
OnlyInA

OnlyInB

NewerInA

NewerInB

";
            Assert.AreEqual(
                expected.NormalizeResult(),
                actual.NormalizeResult(),
                "Expecting new record 13 is unmodified equal"
            );
            { }


            listB.Add(internalGetNewRecord());

            reconciled = DefaultExec();

            actual = reconciled.ToString();
            actual.ToClipboard();
            actual.ToClipboardAssert("Expecting new item in OnlyInB.");
            { }

            expected = @" 
Equal
UID: ID01, TimeStamp: 03/09/2010 00:51:12, Description: Record 01 modified=False
UID: ID01, TimeStamp: 03/09/2010 00:51:12, Description: Record 01 modified=False
UID: ID02, TimeStamp: 03/09/2010 02:23:07, Description: Record 02 modified=False
UID: ID02, TimeStamp: 03/09/2010 02:23:07, Description: Record 02 modified=False
UID: ID03, TimeStamp: 03/09/2010 02:14:23, Description: Record 03 modified=False
UID: ID03, TimeStamp: 03/09/2010 02:14:23, Description: Record 03 modified=False
UID: ID04, TimeStamp: 03/09/2010 00:00:00, Description: Record 04 modified=False
UID: ID04, TimeStamp: 03/09/2010 00:00:00, Description: Record 04 modified=False
UID: ID05, TimeStamp: 03/09/2010 00:00:00, Description: Record 05 modified=False
UID: ID05, TimeStamp: 03/09/2010 00:00:00, Description: Record 05 modified=False
UID: ID06, TimeStamp: 03/09/2010 00:00:00, Description: Record 06 modified=False
UID: ID06, TimeStamp: 03/09/2010 00:00:00, Description: Record 06 modified=False
UID: ID07, TimeStamp: 03/09/2010 00:00:00, Description: Record 07 modified=False
UID: ID07, TimeStamp: 03/09/2010 00:00:00, Description: Record 07 modified=False
UID: ID08, TimeStamp: 03/09/2010 01:25:43, Description: Record 08 modified=False
UID: ID08, TimeStamp: 03/09/2010 01:25:43, Description: Record 08 modified=False
UID: ID09, TimeStamp: 03/09/2010 02:45:18, Description: Record 09 modified=False
UID: ID09, TimeStamp: 03/09/2010 02:45:18, Description: Record 09 modified=False
UID: ID10, TimeStamp: 03/09/2010 01:59:27, Description: Record 10 modified=False
UID: ID10, TimeStamp: 03/09/2010 01:59:27, Description: Record 10 modified=False
UID: ID11, TimeStamp: 03/09/2010 01:56:32, Description: Record 11 modified=False
UID: ID11, TimeStamp: 03/09/2010 01:56:32, Description: Record 11 modified=False
UID: ID12, TimeStamp: 03/09/2010 00:00:00, Description: Record 12 modified=False
UID: ID12, TimeStamp: 03/09/2010 00:00:00, Description: Record 12 modified=False
UID: ID13, TimeStamp: 03/09/2010 00:00:00, Description: Record 13 modified=False
UID: ID13, TimeStamp: 03/09/2010 00:00:00, Description: Record 13 modified=False
OnlyInA

OnlyInB
UID: ID14, TimeStamp: 03/09/2010 00:00:00, Description: Record 14 modified=False
NewerInA

NewerInB

";
            Assert.AreEqual(
                expected.NormalizeResult(),
                actual.NormalizeResult(),
                "Expecting new item in OnlyInB."
            );

            reconciled++;

            actual = reconciled.ToString();
            actual.ToClipboard();
            actual.ToClipboardAssert("Expecting new record 14 is unmodified equal");
            { }
            expected = @" 
Equal
UID: ID01, TimeStamp: 03/09/2010 00:51:12, Description: Record 01 modified=False
UID: ID01, TimeStamp: 03/09/2010 00:51:12, Description: Record 01 modified=False
UID: ID02, TimeStamp: 03/09/2010 02:23:07, Description: Record 02 modified=False
UID: ID02, TimeStamp: 03/09/2010 02:23:07, Description: Record 02 modified=False
UID: ID03, TimeStamp: 03/09/2010 02:14:23, Description: Record 03 modified=False
UID: ID03, TimeStamp: 03/09/2010 02:14:23, Description: Record 03 modified=False
UID: ID04, TimeStamp: 03/09/2010 00:00:00, Description: Record 04 modified=False
UID: ID04, TimeStamp: 03/09/2010 00:00:00, Description: Record 04 modified=False
UID: ID05, TimeStamp: 03/09/2010 00:00:00, Description: Record 05 modified=False
UID: ID05, TimeStamp: 03/09/2010 00:00:00, Description: Record 05 modified=False
UID: ID06, TimeStamp: 03/09/2010 00:00:00, Description: Record 06 modified=False
UID: ID06, TimeStamp: 03/09/2010 00:00:00, Description: Record 06 modified=False
UID: ID07, TimeStamp: 03/09/2010 00:00:00, Description: Record 07 modified=False
UID: ID07, TimeStamp: 03/09/2010 00:00:00, Description: Record 07 modified=False
UID: ID08, TimeStamp: 03/09/2010 01:25:43, Description: Record 08 modified=False
UID: ID08, TimeStamp: 03/09/2010 01:25:43, Description: Record 08 modified=False
UID: ID09, TimeStamp: 03/09/2010 02:45:18, Description: Record 09 modified=False
UID: ID09, TimeStamp: 03/09/2010 02:45:18, Description: Record 09 modified=False
UID: ID10, TimeStamp: 03/09/2010 01:59:27, Description: Record 10 modified=False
UID: ID10, TimeStamp: 03/09/2010 01:59:27, Description: Record 10 modified=False
UID: ID11, TimeStamp: 03/09/2010 01:56:32, Description: Record 11 modified=False
UID: ID11, TimeStamp: 03/09/2010 01:56:32, Description: Record 11 modified=False
UID: ID12, TimeStamp: 03/09/2010 00:00:00, Description: Record 12 modified=False
UID: ID12, TimeStamp: 03/09/2010 00:00:00, Description: Record 12 modified=False
UID: ID13, TimeStamp: 03/09/2010 00:00:00, Description: Record 13 modified=False
UID: ID13, TimeStamp: 03/09/2010 00:00:00, Description: Record 13 modified=False
UID: ID14, TimeStamp: 03/09/2010 00:00:00, Description: Record 14 modified=False
UID: ID14, TimeStamp: 03/09/2010 00:00:00, Description: Record 14 modified=False
OnlyInA

OnlyInB

NewerInA

NewerInB

";
            Assert.AreEqual(
                expected.NormalizeResult(),
                actual.NormalizeResult(),
                "Expecting new record 14 is unmodified equal"
            );

            listA.RemoveAt(rando.Next(listA.Count));
            listA.RemoveAt(rando.Next(listA.Count));

            reconciled = DefaultExec();

            actual = reconciled.ToString();
            actual.ToClipboard();
            actual.ToClipboardAssert("Expecting 2 items removed from A are now OnlyInB.");
            { }
            expected = @" 
Equal
UID: ID01, TimeStamp: 03/09/2010 00:51:12, Description: Record 01 modified=False
UID: ID01, TimeStamp: 03/09/2010 00:51:12, Description: Record 01 modified=False
UID: ID02, TimeStamp: 03/09/2010 02:23:07, Description: Record 02 modified=False
UID: ID02, TimeStamp: 03/09/2010 02:23:07, Description: Record 02 modified=False
UID: ID03, TimeStamp: 03/09/2010 02:14:23, Description: Record 03 modified=False
UID: ID03, TimeStamp: 03/09/2010 02:14:23, Description: Record 03 modified=False
UID: ID05, TimeStamp: 03/09/2010 00:00:00, Description: Record 05 modified=False
UID: ID05, TimeStamp: 03/09/2010 00:00:00, Description: Record 05 modified=False
UID: ID06, TimeStamp: 03/09/2010 00:00:00, Description: Record 06 modified=False
UID: ID06, TimeStamp: 03/09/2010 00:00:00, Description: Record 06 modified=False
UID: ID07, TimeStamp: 03/09/2010 00:00:00, Description: Record 07 modified=False
UID: ID07, TimeStamp: 03/09/2010 00:00:00, Description: Record 07 modified=False
UID: ID08, TimeStamp: 03/09/2010 01:25:43, Description: Record 08 modified=False
UID: ID08, TimeStamp: 03/09/2010 01:25:43, Description: Record 08 modified=False
UID: ID09, TimeStamp: 03/09/2010 02:45:18, Description: Record 09 modified=False
UID: ID09, TimeStamp: 03/09/2010 02:45:18, Description: Record 09 modified=False
UID: ID10, TimeStamp: 03/09/2010 01:59:27, Description: Record 10 modified=False
UID: ID10, TimeStamp: 03/09/2010 01:59:27, Description: Record 10 modified=False
UID: ID11, TimeStamp: 03/09/2010 01:56:32, Description: Record 11 modified=False
UID: ID11, TimeStamp: 03/09/2010 01:56:32, Description: Record 11 modified=False
UID: ID12, TimeStamp: 03/09/2010 00:00:00, Description: Record 12 modified=False
UID: ID12, TimeStamp: 03/09/2010 00:00:00, Description: Record 12 modified=False
UID: ID13, TimeStamp: 03/09/2010 00:00:00, Description: Record 13 modified=False
UID: ID13, TimeStamp: 03/09/2010 00:00:00, Description: Record 13 modified=False
OnlyInA

OnlyInB
UID: ID04, TimeStamp: 03/09/2010 00:00:00, Description: Record 04 modified=False
UID: ID14, TimeStamp: 03/09/2010 00:00:00, Description: Record 14 modified=False
NewerInA

NewerInB

";
            Assert.AreEqual(
                expected.NormalizeResult(),
                actual.NormalizeResult(),
                "Expecting new item in OnlyInB."
            );

            listB.RemoveAt(rando.Next(listB.Count));
            listB.RemoveAt(rando.Next(listB.Count));

            reconciled = DefaultExec();

            actual = reconciled.ToString();
            actual.ToClipboard();
            actual.ToClipboardAssert("Expecting 2 items removed from A are now OnlyInB.");
            { }
            expected = @" 
Equal
UID: ID01, TimeStamp: 03/09/2010 00:51:12, Description: Record 01 modified=False
UID: ID01, TimeStamp: 03/09/2010 00:51:12, Description: Record 01 modified=False
UID: ID02, TimeStamp: 03/09/2010 02:23:07, Description: Record 02 modified=False
UID: ID02, TimeStamp: 03/09/2010 02:23:07, Description: Record 02 modified=False
UID: ID03, TimeStamp: 03/09/2010 02:14:23, Description: Record 03 modified=False
UID: ID03, TimeStamp: 03/09/2010 02:14:23, Description: Record 03 modified=False
UID: ID05, TimeStamp: 03/09/2010 00:00:00, Description: Record 05 modified=False
UID: ID05, TimeStamp: 03/09/2010 00:00:00, Description: Record 05 modified=False
UID: ID07, TimeStamp: 03/09/2010 00:00:00, Description: Record 07 modified=False
UID: ID07, TimeStamp: 03/09/2010 00:00:00, Description: Record 07 modified=False
UID: ID08, TimeStamp: 03/09/2010 01:25:43, Description: Record 08 modified=False
UID: ID08, TimeStamp: 03/09/2010 01:25:43, Description: Record 08 modified=False
UID: ID09, TimeStamp: 03/09/2010 02:45:18, Description: Record 09 modified=False
UID: ID09, TimeStamp: 03/09/2010 02:45:18, Description: Record 09 modified=False
UID: ID11, TimeStamp: 03/09/2010 01:56:32, Description: Record 11 modified=False
UID: ID11, TimeStamp: 03/09/2010 01:56:32, Description: Record 11 modified=False
UID: ID12, TimeStamp: 03/09/2010 00:00:00, Description: Record 12 modified=False
UID: ID12, TimeStamp: 03/09/2010 00:00:00, Description: Record 12 modified=False
UID: ID13, TimeStamp: 03/09/2010 00:00:00, Description: Record 13 modified=False
UID: ID13, TimeStamp: 03/09/2010 00:00:00, Description: Record 13 modified=False
OnlyInA
UID: ID06, TimeStamp: 03/09/2010 00:00:00, Description: Record 06 modified=False
UID: ID10, TimeStamp: 03/09/2010 01:59:27, Description: Record 10 modified=False
OnlyInB
UID: ID04, TimeStamp: 03/09/2010 00:00:00, Description: Record 04 modified=False
UID: ID14, TimeStamp: 03/09/2010 00:00:00, Description: Record 14 modified=False
NewerInA

NewerInB

";
            Assert.AreEqual(
                expected.NormalizeResult(),
                actual.NormalizeResult(),
                "Expecting new item in OnlyInB."
            );

            // Using Trim mode to reduce records and validate
            using (DHOST_APPLYCONTEXT.GetToken(ReconciliationMode.Trim))
            {
                reconciled++;
            }

            actual = reconciled.ToString();
            actual.ToClipboard();
            actual.ToClipboardAssert("Expecting removal of 4 records leaving 10 equal.");
            { }
            expected = @" 
Equal
UID: ID01, TimeStamp: 03/09/2010 00:51:12, Description: Record 01 modified=False
UID: ID01, TimeStamp: 03/09/2010 00:51:12, Description: Record 01 modified=False
UID: ID02, TimeStamp: 03/09/2010 02:23:07, Description: Record 02 modified=False
UID: ID02, TimeStamp: 03/09/2010 02:23:07, Description: Record 02 modified=False
UID: ID03, TimeStamp: 03/09/2010 02:14:23, Description: Record 03 modified=False
UID: ID03, TimeStamp: 03/09/2010 02:14:23, Description: Record 03 modified=False
UID: ID05, TimeStamp: 03/09/2010 00:00:00, Description: Record 05 modified=False
UID: ID05, TimeStamp: 03/09/2010 00:00:00, Description: Record 05 modified=False
UID: ID07, TimeStamp: 03/09/2010 00:00:00, Description: Record 07 modified=False
UID: ID07, TimeStamp: 03/09/2010 00:00:00, Description: Record 07 modified=False
UID: ID08, TimeStamp: 03/09/2010 01:25:43, Description: Record 08 modified=False
UID: ID08, TimeStamp: 03/09/2010 01:25:43, Description: Record 08 modified=False
UID: ID09, TimeStamp: 03/09/2010 02:45:18, Description: Record 09 modified=False
UID: ID09, TimeStamp: 03/09/2010 02:45:18, Description: Record 09 modified=False
UID: ID11, TimeStamp: 03/09/2010 01:56:32, Description: Record 11 modified=False
UID: ID11, TimeStamp: 03/09/2010 01:56:32, Description: Record 11 modified=False
UID: ID12, TimeStamp: 03/09/2010 00:00:00, Description: Record 12 modified=False
UID: ID12, TimeStamp: 03/09/2010 00:00:00, Description: Record 12 modified=False
UID: ID13, TimeStamp: 03/09/2010 00:00:00, Description: Record 13 modified=False
UID: ID13, TimeStamp: 03/09/2010 00:00:00, Description: Record 13 modified=False
OnlyInA

OnlyInB

NewerInA

NewerInB

";
            Assert.AreEqual(
                expected.NormalizeResult(),
                actual.NormalizeResult(),
                "Expecting removal of 4 records leaving 10 equal."
            );
            Assert.AreEqual(10, reconciled.Equal.Count());
            { }

            IList target = listA;
            var builder = new List<string>();
            listA.Add(internalGetNewRecord());
            listA.Add(internalGetNewRecord());
            listA.Add(internalGetNewRecord());
            builder.Add($"{internalRemoveRandom(target)}");
            builder.Add($"{internalRemoveRandom(target)}");
            builder.Add($"{internalRemoveRandom(target)}");
            builder.Add($"{internalModifyRandom(target)}");
            builder.Add($"{internalModifyRandom(target)}");

            target = listB;
            listB.Add(internalGetNewRecord());
            builder.Add($"{internalRemoveRandom(target)}");
            builder.Add($"{internalModifyRandom(target)}");

            actual = string.Join(Environment.NewLine, builder);
            actual.ToClipboard();
            actual.ToClipboardAssert("Expecting consistent pseudorando index values.");
            { }
            // This explains the valid result where ID05 is modified in both.
            expected = @" 
ID08
ID13
ID12
(ID07, 3/9/2010 12:40:45 AM)
(ID05, 3/9/2010 1:27:42 AM)
ID02
(ID05, 3/9/2010 1:55:16 AM)";
            Assert.AreEqual(
                expected.NormalizeResult(),
                actual.NormalizeResult(),
                "Expecting consistent pseudorando index values."
            );

            reconciled = DefaultExec();

            actual = reconciled.ToString();
            actual.ToClipboard();
            actual.ToClipboardAssert("Expecting random changes.");
            { }
            // Expect 4 OnlyInA. Three new A and removed from B.
            // Expect 4 OnlyInB. Three removed A and one new B.
            // Expect 1 newer TUPLE PAIR in A.
            // Expect 1 newer TUPLE PAIR in B.
            expected = @" 
Equal
UID: ID01, TimeStamp: 03/09/2010 00:51:12, Description: Record 01 modified=False
UID: ID01, TimeStamp: 03/09/2010 00:51:12, Description: Record 01 modified=False
UID: ID02, TimeStamp: 03/09/2010 02:23:07, Description: Record 02 modified=False
UID: ID02, TimeStamp: 03/09/2010 02:23:07, Description: Record 02 modified=False
UID: ID03, TimeStamp: 03/09/2010 02:14:23, Description: Record 03 modified=False
UID: ID03, TimeStamp: 03/09/2010 02:14:23, Description: Record 03 modified=False
UID: ID09, TimeStamp: 03/09/2010 02:45:18, Description: Record 09 modified=False
UID: ID09, TimeStamp: 03/09/2010 02:45:18, Description: Record 09 modified=False
UID: ID11, TimeStamp: 03/09/2010 01:56:32, Description: Record 11 modified=False
UID: ID11, TimeStamp: 03/09/2010 01:56:32, Description: Record 11 modified=False
OnlyInA
UID: ID15, TimeStamp: 03/09/2010 00:00:00, Description: Record 15 modified=False
UID: ID16, TimeStamp: 03/09/2010 00:00:00, Description: Record 16 modified=False
UID: ID17, TimeStamp: 03/09/2010 00:00:00, Description: Record 17 modified=False
OnlyInB
UID: ID08, TimeStamp: 03/09/2010 01:25:43, Description: Record 08 modified=False
UID: ID12, TimeStamp: 03/09/2010 00:00:00, Description: Record 12 modified=False
UID: ID13, TimeStamp: 03/09/2010 00:00:00, Description: Record 13 modified=False
UID: ID18, TimeStamp: 03/09/2010 00:00:00, Description: Record 18 modified=False
NewerInA
a:UID: ID07, TimeStamp: 03/09/2010 00:40:45, Description: Record 07 modified=True
b:UID: ID07, TimeStamp: 03/09/2010 00:00:00, Description: Record 07 modified=False
NewerInB
a:UID: ID05, TimeStamp: 03/09/2010 01:27:42, Description: Record 05 modified=True
b:UID: ID05, TimeStamp: 03/09/2010 01:55:16, Description: Record 05 modified=True
";
            Assert.AreEqual(
                expected.NormalizeResult(),
                actual.NormalizeResult(),
                "Expecting random changes."
            );
            { }

            var builderPrevA = new List<MockDatabaseRecord>();
            builderPrevA.AddRange(
                reconciled
                .Equal
                .OfType<Tuple<MockDatabaseRecord, MockDatabaseRecord>>()
                .Select(_ => _.Item1));
            builderPrevA.AddRange(
                reconciled
                .OnlyInA);
            builderPrevA.AddRange(
                reconciled
                .NewerInA);
            // Don't forget to count NewerInB because it's still in A!!
            builderPrevA.AddRange(
                reconciled
                .NewerInB);

            using (DHOST_APPLYCONTEXT.GetToken(ReconciliationMode.TakeA))
            {
                reconciled++;
            }

            actual = reconciled.ToString();
            actual.ToClipboard();
            actual.ToClipboardAssert("Expecting removal of 4 records leaving 10 equal.");
            { }

            expected = @" 
Equal
UID: ID01, TimeStamp: 03/09/2010 00:51:12, Description: Record 01 modified=False
UID: ID01, TimeStamp: 03/09/2010 00:51:12, Description: Record 01 modified=False
UID: ID02, TimeStamp: 03/09/2010 02:23:07, Description: Record 02 modified=False
UID: ID02, TimeStamp: 03/09/2010 02:23:07, Description: Record 02 modified=False
UID: ID03, TimeStamp: 03/09/2010 02:14:23, Description: Record 03 modified=False
UID: ID03, TimeStamp: 03/09/2010 02:14:23, Description: Record 03 modified=False
UID: ID05, TimeStamp: 03/09/2010 01:27:42, Description: Record 05 modified=False
UID: ID05, TimeStamp: 03/09/2010 01:27:42, Description: Record 05 modified=False
UID: ID07, TimeStamp: 03/09/2010 00:40:45, Description: Record 07 modified=False
UID: ID07, TimeStamp: 03/09/2010 00:40:45, Description: Record 07 modified=False
UID: ID09, TimeStamp: 03/09/2010 02:45:18, Description: Record 09 modified=False
UID: ID09, TimeStamp: 03/09/2010 02:45:18, Description: Record 09 modified=False
UID: ID11, TimeStamp: 03/09/2010 01:56:32, Description: Record 11 modified=False
UID: ID11, TimeStamp: 03/09/2010 01:56:32, Description: Record 11 modified=False
UID: ID15, TimeStamp: 03/09/2010 00:00:00, Description: Record 15 modified=False
UID: ID15, TimeStamp: 03/09/2010 00:00:00, Description: Record 15 modified=False
UID: ID16, TimeStamp: 03/09/2010 00:00:00, Description: Record 16 modified=False
UID: ID16, TimeStamp: 03/09/2010 00:00:00, Description: Record 16 modified=False
UID: ID17, TimeStamp: 03/09/2010 00:00:00, Description: Record 17 modified=False
UID: ID17, TimeStamp: 03/09/2010 00:00:00, Description: Record 17 modified=False
OnlyInA

OnlyInB

NewerInA

NewerInB

";
            Assert.AreEqual(
                expected.NormalizeResult(),
                actual.NormalizeResult(),
                "Expecting removal of 4 records leaving 10 equal."
            );

            var builderCurrentA = new List<MockDatabaseRecord>();
            builderCurrentA.AddRange(
                reconciled
                .Equal
                .OfType<Tuple<MockDatabaseRecord, MockDatabaseRecord>>()
                .Select(_ => _.Item1));
            builderCurrentA.AddRange(
                reconciled
                .OnlyInA);
            builderCurrentA.AddRange(
                reconciled
                .NewerInA);
            // Don't forget to count NewerInB because it's still in A!!
            builderPrevA.AddRange(
                reconciled
                .NewerInB);

            Assert.AreEqual(
                builderPrevA.Count(),
                builderCurrentA.Count(),
                $"Expecting the before-and-after A count to jibe.");

            Assert.AreEqual(10, reconciled.Equal.Count());
            Assert.AreEqual(0, reconciled.OnlyInA.Count());
            Assert.AreEqual(0, reconciled.OnlyInB.Count());
            Assert.AreEqual(0, reconciled.NewerInA.Count());
            Assert.AreEqual(0, reconciled.NewerInB.Count());

            // ^^^^^^^^^^^^
            // ============



            target = listA;
            builder = new List<string>();
            listA.Add(internalGetNewRecord());
            listA.Add(internalGetNewRecord());
            listA.Add(internalGetNewRecord());
            builder.Add($"{internalRemoveRandom(target)}");
            builder.Add($"{internalRemoveRandom(target)}");
            builder.Add($"{internalRemoveRandom(target)}");
            builder.Add($"{internalModifyRandom(target)}");
            builder.Add($"{internalModifyRandom(target)}");

            target = listB;
            listB.Add(internalGetNewRecord());
            builder.Add($"{internalRemoveRandom(target)}");
            builder.Add($"{internalModifyRandom(target)}");

            actual = string.Join(Environment.NewLine, builder);
            actual.ToClipboard();
            actual.ToClipboardAssert("Expecting consistent pseudorando index values.");
            { }

            expected = @" 
ID09
ID02
ID21
(ID01, 3/9/2010 3:33:28 AM)
(ID11, 3/9/2010 3:43:02 AM)
ID01
(ID02, 3/9/2010 2:58:55 AM)";
            Assert.AreEqual(
                expected.NormalizeResult(),
                actual.NormalizeResult(),
                "Expecting consistent pseudorando index values."
            );

            reconciled = DefaultExec();

            actual = reconciled.ToString();
            actual.ToClipboard();
            actual.ToClipboardAssert("Expecting random changes.");
            { }
            expected = @" 
Equal
UID: ID03, TimeStamp: 03/09/2010 02:14:23, Description: Record 03 modified=False
UID: ID03, TimeStamp: 03/09/2010 02:14:23, Description: Record 03 modified=False
UID: ID05, TimeStamp: 03/09/2010 01:27:42, Description: Record 05 modified=False
UID: ID05, TimeStamp: 03/09/2010 01:27:42, Description: Record 05 modified=False
UID: ID07, TimeStamp: 03/09/2010 00:40:45, Description: Record 07 modified=False
UID: ID07, TimeStamp: 03/09/2010 00:40:45, Description: Record 07 modified=False
UID: ID15, TimeStamp: 03/09/2010 00:00:00, Description: Record 15 modified=False
UID: ID15, TimeStamp: 03/09/2010 00:00:00, Description: Record 15 modified=False
UID: ID16, TimeStamp: 03/09/2010 00:00:00, Description: Record 16 modified=False
UID: ID16, TimeStamp: 03/09/2010 00:00:00, Description: Record 16 modified=False
UID: ID17, TimeStamp: 03/09/2010 00:00:00, Description: Record 17 modified=False
UID: ID17, TimeStamp: 03/09/2010 00:00:00, Description: Record 17 modified=False
OnlyInA
UID: ID19, TimeStamp: 03/09/2010 00:00:00, Description: Record 19 modified=False
UID: ID20, TimeStamp: 03/09/2010 00:00:00, Description: Record 20 modified=False
OnlyInB
UID: ID02, TimeStamp: 03/09/2010 02:58:55, Description: Record 02 modified=True
UID: ID09, TimeStamp: 03/09/2010 02:45:18, Description: Record 09 modified=False
UID: ID22, TimeStamp: 03/09/2010 00:00:00, Description: Record 22 modified=False
NewerInA
a:UID: ID01, TimeStamp: 03/09/2010 03:33:28, Description: Record 01 modified=True
b:UID: ID01, TimeStamp: 03/09/2010 00:51:12, Description: Record 01 modified=False
a:UID: ID11, TimeStamp: 03/09/2010 03:43:02, Description: Record 11 modified=True
b:UID: ID11, TimeStamp: 03/09/2010 01:56:32, Description: Record 11 modified=False
NewerInB

";
            Assert.AreEqual(
                expected.NormalizeResult(),
                actual.NormalizeResult(),
                "Expecting random changes."
            );


            var builderPrevB = new List<MockDatabaseRecord>();
            builderPrevB.AddRange(
                reconciled
                .Equal
                .OfType<Tuple<MockDatabaseRecord, MockDatabaseRecord>>()
                .Select(_ => _.Item1));
            builderPrevB.AddRange(
                reconciled
                .OnlyInB);
            builderPrevB.AddRange(
                reconciled
                .NewerInB);
            // Don't forget to count NewerInA because it's still in B!!
            builderPrevB.AddRange(
                reconciled
                .NewerInB);

            using (DHOST_APPLYCONTEXT.GetToken(ReconciliationMode.TakeA))
            {
                reconciled++;
            }

            actual = reconciled.ToString();
            actual.ToClipboard();
            actual.ToClipboardAssert("Expecting removal of 4 records leaving 10 equal.");
            { }

            expected = @" 
Equal
UID: ID01, TimeStamp: 03/09/2010 03:33:28, Description: Record 01 modified=False
UID: ID01, TimeStamp: 03/09/2010 03:33:28, Description: Record 01 modified=False
UID: ID03, TimeStamp: 03/09/2010 02:14:23, Description: Record 03 modified=False
UID: ID03, TimeStamp: 03/09/2010 02:14:23, Description: Record 03 modified=False
UID: ID05, TimeStamp: 03/09/2010 01:27:42, Description: Record 05 modified=False
UID: ID05, TimeStamp: 03/09/2010 01:27:42, Description: Record 05 modified=False
UID: ID07, TimeStamp: 03/09/2010 00:40:45, Description: Record 07 modified=False
UID: ID07, TimeStamp: 03/09/2010 00:40:45, Description: Record 07 modified=False
UID: ID11, TimeStamp: 03/09/2010 03:43:02, Description: Record 11 modified=False
UID: ID11, TimeStamp: 03/09/2010 03:43:02, Description: Record 11 modified=False
UID: ID15, TimeStamp: 03/09/2010 00:00:00, Description: Record 15 modified=False
UID: ID15, TimeStamp: 03/09/2010 00:00:00, Description: Record 15 modified=False
UID: ID16, TimeStamp: 03/09/2010 00:00:00, Description: Record 16 modified=False
UID: ID16, TimeStamp: 03/09/2010 00:00:00, Description: Record 16 modified=False
UID: ID17, TimeStamp: 03/09/2010 00:00:00, Description: Record 17 modified=False
UID: ID17, TimeStamp: 03/09/2010 00:00:00, Description: Record 17 modified=False
UID: ID19, TimeStamp: 03/09/2010 00:00:00, Description: Record 19 modified=False
UID: ID19, TimeStamp: 03/09/2010 00:00:00, Description: Record 19 modified=False
UID: ID20, TimeStamp: 03/09/2010 00:00:00, Description: Record 20 modified=False
UID: ID20, TimeStamp: 03/09/2010 00:00:00, Description: Record 20 modified=False
OnlyInA

OnlyInB

NewerInA

NewerInB

";
            Assert.AreEqual(
                expected.NormalizeResult(),
                actual.NormalizeResult(),
                "Expecting removal of 4 records leaving 10 equal."
            );


            var builderCurrentB = new List<MockDatabaseRecord>();
            builderCurrentB.AddRange(
                reconciled
                .Equal
                .OfType<Tuple<MockDatabaseRecord, MockDatabaseRecord>>()
                .Select(_ => _.Item1));
            builderCurrentB.AddRange(
                reconciled
                .OnlyInB);
            builderCurrentB.AddRange(
                reconciled
                .NewerInB);
            // Don't forget to count NewerInA because it's still in B!!
            builderPrevB.AddRange(
                reconciled
                .NewerInB);

            Assert.AreEqual(
                builderPrevA.Count(),
                builderCurrentA.Count(),
                $"Expecting the before-and-after A count to jibe.");
        }  

        /// <summary>
        /// Calls the V2 signature with DiffMode.Disabled.
        /// This should produce identical output as the V1 test.
        /// </summary>
        [TestMethod]
        public void TestReconcileMethod_V2_WithToString_V2DiffModeDisabled()
        {
            Reconciled<MockDatabaseRecord>.DefaultExec = () => Reconcile(
                    listA,
                    listB,
                    diffMode: DiffReportMode.Disabled);

            Reconciled<MockDatabaseRecord> reconciled;

            reconciled = DefaultExec();

            // Initial comparison of listA and empty listB
            actual = reconciled.ToString();
            actual.ToClipboard();
            actual.ToClipboardAssert("Expecting initialized 12 OnlyInA values with default Epoch.");
            { }

            expected = @" 
Equal

OnlyInA
UID: ID01, TimeStamp: 03/09/2010 00:00:00, Description: Record 01 modified=False
UID: ID02, TimeStamp: 03/09/2010 00:00:00, Description: Record 02 modified=False
UID: ID03, TimeStamp: 03/09/2010 00:00:00, Description: Record 03 modified=False
UID: ID04, TimeStamp: 03/09/2010 00:00:00, Description: Record 04 modified=False
UID: ID05, TimeStamp: 03/09/2010 00:00:00, Description: Record 05 modified=False
UID: ID06, TimeStamp: 03/09/2010 00:00:00, Description: Record 06 modified=False
UID: ID07, TimeStamp: 03/09/2010 00:00:00, Description: Record 07 modified=False
UID: ID08, TimeStamp: 03/09/2010 00:00:00, Description: Record 08 modified=False
UID: ID09, TimeStamp: 03/09/2010 00:00:00, Description: Record 09 modified=False
UID: ID10, TimeStamp: 03/09/2010 00:00:00, Description: Record 10 modified=False
UID: ID11, TimeStamp: 03/09/2010 00:00:00, Description: Record 11 modified=False
UID: ID12, TimeStamp: 03/09/2010 00:00:00, Description: Record 12 modified=False
OnlyInB

NewerInA

NewerInB

";
            Assert.AreEqual(
                expected.NormalizeResult(),
                actual.NormalizeResult(),
                "Expecting 12 OnlyInA values with default Epoch."
            );

            // Adding cloned records from listA to listB, making them equal.
            reconciled++;

            actual = reconciled.ToString();
            actual.ToClipboard();
            actual.ToClipboardAssert("Expecting records are Equal, based on the comparers (not on Equals)");
            { }
            expected = @" 
Equal
UID: ID01, TimeStamp: 03/09/2010 00:00:00, Description: Record 01 modified=False
UID: ID01, TimeStamp: 03/09/2010 00:00:00, Description: Record 01 modified=False
UID: ID02, TimeStamp: 03/09/2010 00:00:00, Description: Record 02 modified=False
UID: ID02, TimeStamp: 03/09/2010 00:00:00, Description: Record 02 modified=False
UID: ID03, TimeStamp: 03/09/2010 00:00:00, Description: Record 03 modified=False
UID: ID03, TimeStamp: 03/09/2010 00:00:00, Description: Record 03 modified=False
UID: ID04, TimeStamp: 03/09/2010 00:00:00, Description: Record 04 modified=False
UID: ID04, TimeStamp: 03/09/2010 00:00:00, Description: Record 04 modified=False
UID: ID05, TimeStamp: 03/09/2010 00:00:00, Description: Record 05 modified=False
UID: ID05, TimeStamp: 03/09/2010 00:00:00, Description: Record 05 modified=False
UID: ID06, TimeStamp: 03/09/2010 00:00:00, Description: Record 06 modified=False
UID: ID06, TimeStamp: 03/09/2010 00:00:00, Description: Record 06 modified=False
UID: ID07, TimeStamp: 03/09/2010 00:00:00, Description: Record 07 modified=False
UID: ID07, TimeStamp: 03/09/2010 00:00:00, Description: Record 07 modified=False
UID: ID08, TimeStamp: 03/09/2010 00:00:00, Description: Record 08 modified=False
UID: ID08, TimeStamp: 03/09/2010 00:00:00, Description: Record 08 modified=False
UID: ID09, TimeStamp: 03/09/2010 00:00:00, Description: Record 09 modified=False
UID: ID09, TimeStamp: 03/09/2010 00:00:00, Description: Record 09 modified=False
UID: ID10, TimeStamp: 03/09/2010 00:00:00, Description: Record 10 modified=False
UID: ID10, TimeStamp: 03/09/2010 00:00:00, Description: Record 10 modified=False
UID: ID11, TimeStamp: 03/09/2010 00:00:00, Description: Record 11 modified=False
UID: ID11, TimeStamp: 03/09/2010 00:00:00, Description: Record 11 modified=False
UID: ID12, TimeStamp: 03/09/2010 00:00:00, Description: Record 12 modified=False
UID: ID12, TimeStamp: 03/09/2010 00:00:00, Description: Record 12 modified=False
OnlyInA

OnlyInB

NewerInA

NewerInB

";

            Assert.AreEqual(
                expected.NormalizeResult(),
                actual.NormalizeResult(),
                "Expecting records are Equal, based on the comparers (not on Equals)"
            );

            // Applying random modifications to both lists
            localMakeWeightedRandomChanges();

            reconciled = DefaultExec();
            actual = reconciled.ToString();
            actual.ToClipboard();
            actual.ToClipboardAssert("Expecting to see changes in each.");
            { }
            // This output demonstrates where Version1 falls a little short perhaps,
            // because ID02, ID09, and ID11 have all received possibly conflicting
            // modifications, but are reported in NeweInX without complaint.
            expected = @" 
Equal
UID: ID04, TimeStamp: 03/09/2010 00:00:00, Description: Record 04 modified=False
UID: ID04, TimeStamp: 03/09/2010 00:00:00, Description: Record 04 modified=False
UID: ID05, TimeStamp: 03/09/2010 00:00:00, Description: Record 05 modified=False
UID: ID05, TimeStamp: 03/09/2010 00:00:00, Description: Record 05 modified=False
UID: ID06, TimeStamp: 03/09/2010 00:00:00, Description: Record 06 modified=False
UID: ID06, TimeStamp: 03/09/2010 00:00:00, Description: Record 06 modified=False
UID: ID07, TimeStamp: 03/09/2010 00:00:00, Description: Record 07 modified=False
UID: ID07, TimeStamp: 03/09/2010 00:00:00, Description: Record 07 modified=False
UID: ID12, TimeStamp: 03/09/2010 00:00:00, Description: Record 12 modified=False
UID: ID12, TimeStamp: 03/09/2010 00:00:00, Description: Record 12 modified=False
OnlyInA

OnlyInB

NewerInA
a:UID: ID01, TimeStamp: 03/09/2010 00:51:12, Description: Record 01 modified=True
b:UID: ID01, TimeStamp: 03/09/2010 00:00:00, Description: Record 01 modified=False
a:UID: ID02, TimeStamp: 03/09/2010 02:23:07, Description: Record 02 modified=True
b:UID: ID02, TimeStamp: 03/09/2010 00:10:25, Description: Record 02 modified=True
a:UID: ID10, TimeStamp: 03/09/2010 01:59:27, Description: Record 10 modified=True
b:UID: ID10, TimeStamp: 03/09/2010 00:00:00, Description: Record 10 modified=False
a:UID: ID11, TimeStamp: 03/09/2010 01:56:32, Description: Record 11 modified=True
b:UID: ID11, TimeStamp: 03/09/2010 01:27:32, Description: Record 11 modified=True
NewerInB
a:UID: ID03, TimeStamp: 03/09/2010 00:00:00, Description: Record 03 modified=False
b:UID: ID03, TimeStamp: 03/09/2010 02:14:23, Description: Record 03 modified=True
a:UID: ID08, TimeStamp: 03/09/2010 00:00:00, Description: Record 08 modified=False
b:UID: ID08, TimeStamp: 03/09/2010 01:25:43, Description: Record 08 modified=True
a:UID: ID09, TimeStamp: 03/09/2010 00:02:16, Description: Record 09 modified=True
b:UID: ID09, TimeStamp: 03/09/2010 02:45:18, Description: Record 09 modified=True
";

            Assert.AreEqual(
                expected.NormalizeResult(),
                actual.NormalizeResult(),
                "Expecting to see changes in each."
            );

            // Adding and removing records from both lists
            reconciled++;

            actual = reconciled.ToString();
            actual.ToClipboard();
            actual.ToClipboardAssert("Expecting reconciled lists.");
            { }
            expected = @" 
Equal
UID: ID01, TimeStamp: 03/09/2010 00:51:12, Description: Record 01 modified=False
UID: ID01, TimeStamp: 03/09/2010 00:51:12, Description: Record 01 modified=False
UID: ID02, TimeStamp: 03/09/2010 02:23:07, Description: Record 02 modified=False
UID: ID02, TimeStamp: 03/09/2010 02:23:07, Description: Record 02 modified=False
UID: ID03, TimeStamp: 03/09/2010 02:14:23, Description: Record 03 modified=False
UID: ID03, TimeStamp: 03/09/2010 02:14:23, Description: Record 03 modified=False
UID: ID04, TimeStamp: 03/09/2010 00:00:00, Description: Record 04 modified=False
UID: ID04, TimeStamp: 03/09/2010 00:00:00, Description: Record 04 modified=False
UID: ID05, TimeStamp: 03/09/2010 00:00:00, Description: Record 05 modified=False
UID: ID05, TimeStamp: 03/09/2010 00:00:00, Description: Record 05 modified=False
UID: ID06, TimeStamp: 03/09/2010 00:00:00, Description: Record 06 modified=False
UID: ID06, TimeStamp: 03/09/2010 00:00:00, Description: Record 06 modified=False
UID: ID07, TimeStamp: 03/09/2010 00:00:00, Description: Record 07 modified=False
UID: ID07, TimeStamp: 03/09/2010 00:00:00, Description: Record 07 modified=False
UID: ID08, TimeStamp: 03/09/2010 01:25:43, Description: Record 08 modified=False
UID: ID08, TimeStamp: 03/09/2010 01:25:43, Description: Record 08 modified=False
UID: ID09, TimeStamp: 03/09/2010 02:45:18, Description: Record 09 modified=False
UID: ID09, TimeStamp: 03/09/2010 02:45:18, Description: Record 09 modified=False
UID: ID10, TimeStamp: 03/09/2010 01:59:27, Description: Record 10 modified=False
UID: ID10, TimeStamp: 03/09/2010 01:59:27, Description: Record 10 modified=False
UID: ID11, TimeStamp: 03/09/2010 01:56:32, Description: Record 11 modified=False
UID: ID11, TimeStamp: 03/09/2010 01:56:32, Description: Record 11 modified=False
UID: ID12, TimeStamp: 03/09/2010 00:00:00, Description: Record 12 modified=False
UID: ID12, TimeStamp: 03/09/2010 00:00:00, Description: Record 12 modified=False
OnlyInA

OnlyInB

NewerInA

NewerInB

";
            Assert.AreEqual(
                expected.NormalizeResult(),
                actual.NormalizeResult(),
                "Expecting reconciled lists."
            );

            listA.Add(internalGetNewRecord());

            reconciled = DefaultExec();

            actual = reconciled.ToString();
            actual.ToClipboard();
            actual.ToClipboardAssert("Expecting new item in OnlyInA.");
            { }
            expected = @" 
Equal
UID: ID01, TimeStamp: 03/09/2010 00:51:12, Description: Record 01 modified=False
UID: ID01, TimeStamp: 03/09/2010 00:51:12, Description: Record 01 modified=False
UID: ID02, TimeStamp: 03/09/2010 02:23:07, Description: Record 02 modified=False
UID: ID02, TimeStamp: 03/09/2010 02:23:07, Description: Record 02 modified=False
UID: ID03, TimeStamp: 03/09/2010 02:14:23, Description: Record 03 modified=False
UID: ID03, TimeStamp: 03/09/2010 02:14:23, Description: Record 03 modified=False
UID: ID04, TimeStamp: 03/09/2010 00:00:00, Description: Record 04 modified=False
UID: ID04, TimeStamp: 03/09/2010 00:00:00, Description: Record 04 modified=False
UID: ID05, TimeStamp: 03/09/2010 00:00:00, Description: Record 05 modified=False
UID: ID05, TimeStamp: 03/09/2010 00:00:00, Description: Record 05 modified=False
UID: ID06, TimeStamp: 03/09/2010 00:00:00, Description: Record 06 modified=False
UID: ID06, TimeStamp: 03/09/2010 00:00:00, Description: Record 06 modified=False
UID: ID07, TimeStamp: 03/09/2010 00:00:00, Description: Record 07 modified=False
UID: ID07, TimeStamp: 03/09/2010 00:00:00, Description: Record 07 modified=False
UID: ID08, TimeStamp: 03/09/2010 01:25:43, Description: Record 08 modified=False
UID: ID08, TimeStamp: 03/09/2010 01:25:43, Description: Record 08 modified=False
UID: ID09, TimeStamp: 03/09/2010 02:45:18, Description: Record 09 modified=False
UID: ID09, TimeStamp: 03/09/2010 02:45:18, Description: Record 09 modified=False
UID: ID10, TimeStamp: 03/09/2010 01:59:27, Description: Record 10 modified=False
UID: ID10, TimeStamp: 03/09/2010 01:59:27, Description: Record 10 modified=False
UID: ID11, TimeStamp: 03/09/2010 01:56:32, Description: Record 11 modified=False
UID: ID11, TimeStamp: 03/09/2010 01:56:32, Description: Record 11 modified=False
UID: ID12, TimeStamp: 03/09/2010 00:00:00, Description: Record 12 modified=False
UID: ID12, TimeStamp: 03/09/2010 00:00:00, Description: Record 12 modified=False
OnlyInA
UID: ID13, TimeStamp: 03/09/2010 00:00:00, Description: Record 13 modified=False
OnlyInB

NewerInA

NewerInB

";
            Assert.AreEqual(
                expected.NormalizeResult(),
                actual.NormalizeResult(),
                "Expecting new item in OnlyInA."
            );
            reconciled++;

            actual = reconciled.ToString();
            actual.ToClipboard();
            actual.ToClipboardAssert("Expecting new record 13 is unmodified equal");
            { }
            expected = @" 
Equal
UID: ID01, TimeStamp: 03/09/2010 00:51:12, Description: Record 01 modified=False
UID: ID01, TimeStamp: 03/09/2010 00:51:12, Description: Record 01 modified=False
UID: ID02, TimeStamp: 03/09/2010 02:23:07, Description: Record 02 modified=False
UID: ID02, TimeStamp: 03/09/2010 02:23:07, Description: Record 02 modified=False
UID: ID03, TimeStamp: 03/09/2010 02:14:23, Description: Record 03 modified=False
UID: ID03, TimeStamp: 03/09/2010 02:14:23, Description: Record 03 modified=False
UID: ID04, TimeStamp: 03/09/2010 00:00:00, Description: Record 04 modified=False
UID: ID04, TimeStamp: 03/09/2010 00:00:00, Description: Record 04 modified=False
UID: ID05, TimeStamp: 03/09/2010 00:00:00, Description: Record 05 modified=False
UID: ID05, TimeStamp: 03/09/2010 00:00:00, Description: Record 05 modified=False
UID: ID06, TimeStamp: 03/09/2010 00:00:00, Description: Record 06 modified=False
UID: ID06, TimeStamp: 03/09/2010 00:00:00, Description: Record 06 modified=False
UID: ID07, TimeStamp: 03/09/2010 00:00:00, Description: Record 07 modified=False
UID: ID07, TimeStamp: 03/09/2010 00:00:00, Description: Record 07 modified=False
UID: ID08, TimeStamp: 03/09/2010 01:25:43, Description: Record 08 modified=False
UID: ID08, TimeStamp: 03/09/2010 01:25:43, Description: Record 08 modified=False
UID: ID09, TimeStamp: 03/09/2010 02:45:18, Description: Record 09 modified=False
UID: ID09, TimeStamp: 03/09/2010 02:45:18, Description: Record 09 modified=False
UID: ID10, TimeStamp: 03/09/2010 01:59:27, Description: Record 10 modified=False
UID: ID10, TimeStamp: 03/09/2010 01:59:27, Description: Record 10 modified=False
UID: ID11, TimeStamp: 03/09/2010 01:56:32, Description: Record 11 modified=False
UID: ID11, TimeStamp: 03/09/2010 01:56:32, Description: Record 11 modified=False
UID: ID12, TimeStamp: 03/09/2010 00:00:00, Description: Record 12 modified=False
UID: ID12, TimeStamp: 03/09/2010 00:00:00, Description: Record 12 modified=False
UID: ID13, TimeStamp: 03/09/2010 00:00:00, Description: Record 13 modified=False
UID: ID13, TimeStamp: 03/09/2010 00:00:00, Description: Record 13 modified=False
OnlyInA

OnlyInB

NewerInA

NewerInB

";
            Assert.AreEqual(
                expected.NormalizeResult(),
                actual.NormalizeResult(),
                "Expecting new record 13 is unmodified equal"
            );
            { }


            listB.Add(internalGetNewRecord());

            reconciled = DefaultExec();

            actual = reconciled.ToString();
            actual.ToClipboard();
            actual.ToClipboardAssert("Expecting new item in OnlyInB.");
            { }

            expected = @" 
Equal
UID: ID01, TimeStamp: 03/09/2010 00:51:12, Description: Record 01 modified=False
UID: ID01, TimeStamp: 03/09/2010 00:51:12, Description: Record 01 modified=False
UID: ID02, TimeStamp: 03/09/2010 02:23:07, Description: Record 02 modified=False
UID: ID02, TimeStamp: 03/09/2010 02:23:07, Description: Record 02 modified=False
UID: ID03, TimeStamp: 03/09/2010 02:14:23, Description: Record 03 modified=False
UID: ID03, TimeStamp: 03/09/2010 02:14:23, Description: Record 03 modified=False
UID: ID04, TimeStamp: 03/09/2010 00:00:00, Description: Record 04 modified=False
UID: ID04, TimeStamp: 03/09/2010 00:00:00, Description: Record 04 modified=False
UID: ID05, TimeStamp: 03/09/2010 00:00:00, Description: Record 05 modified=False
UID: ID05, TimeStamp: 03/09/2010 00:00:00, Description: Record 05 modified=False
UID: ID06, TimeStamp: 03/09/2010 00:00:00, Description: Record 06 modified=False
UID: ID06, TimeStamp: 03/09/2010 00:00:00, Description: Record 06 modified=False
UID: ID07, TimeStamp: 03/09/2010 00:00:00, Description: Record 07 modified=False
UID: ID07, TimeStamp: 03/09/2010 00:00:00, Description: Record 07 modified=False
UID: ID08, TimeStamp: 03/09/2010 01:25:43, Description: Record 08 modified=False
UID: ID08, TimeStamp: 03/09/2010 01:25:43, Description: Record 08 modified=False
UID: ID09, TimeStamp: 03/09/2010 02:45:18, Description: Record 09 modified=False
UID: ID09, TimeStamp: 03/09/2010 02:45:18, Description: Record 09 modified=False
UID: ID10, TimeStamp: 03/09/2010 01:59:27, Description: Record 10 modified=False
UID: ID10, TimeStamp: 03/09/2010 01:59:27, Description: Record 10 modified=False
UID: ID11, TimeStamp: 03/09/2010 01:56:32, Description: Record 11 modified=False
UID: ID11, TimeStamp: 03/09/2010 01:56:32, Description: Record 11 modified=False
UID: ID12, TimeStamp: 03/09/2010 00:00:00, Description: Record 12 modified=False
UID: ID12, TimeStamp: 03/09/2010 00:00:00, Description: Record 12 modified=False
UID: ID13, TimeStamp: 03/09/2010 00:00:00, Description: Record 13 modified=False
UID: ID13, TimeStamp: 03/09/2010 00:00:00, Description: Record 13 modified=False
OnlyInA

OnlyInB
UID: ID14, TimeStamp: 03/09/2010 00:00:00, Description: Record 14 modified=False
NewerInA

NewerInB

";
            Assert.AreEqual(
                expected.NormalizeResult(),
                actual.NormalizeResult(),
                "Expecting new item in OnlyInB."
            );

            reconciled++;

            actual = reconciled.ToString();
            actual.ToClipboard();
            actual.ToClipboardAssert("Expecting new record 14 is unmodified equal");
            { }
            expected = @" 
Equal
UID: ID01, TimeStamp: 03/09/2010 00:51:12, Description: Record 01 modified=False
UID: ID01, TimeStamp: 03/09/2010 00:51:12, Description: Record 01 modified=False
UID: ID02, TimeStamp: 03/09/2010 02:23:07, Description: Record 02 modified=False
UID: ID02, TimeStamp: 03/09/2010 02:23:07, Description: Record 02 modified=False
UID: ID03, TimeStamp: 03/09/2010 02:14:23, Description: Record 03 modified=False
UID: ID03, TimeStamp: 03/09/2010 02:14:23, Description: Record 03 modified=False
UID: ID04, TimeStamp: 03/09/2010 00:00:00, Description: Record 04 modified=False
UID: ID04, TimeStamp: 03/09/2010 00:00:00, Description: Record 04 modified=False
UID: ID05, TimeStamp: 03/09/2010 00:00:00, Description: Record 05 modified=False
UID: ID05, TimeStamp: 03/09/2010 00:00:00, Description: Record 05 modified=False
UID: ID06, TimeStamp: 03/09/2010 00:00:00, Description: Record 06 modified=False
UID: ID06, TimeStamp: 03/09/2010 00:00:00, Description: Record 06 modified=False
UID: ID07, TimeStamp: 03/09/2010 00:00:00, Description: Record 07 modified=False
UID: ID07, TimeStamp: 03/09/2010 00:00:00, Description: Record 07 modified=False
UID: ID08, TimeStamp: 03/09/2010 01:25:43, Description: Record 08 modified=False
UID: ID08, TimeStamp: 03/09/2010 01:25:43, Description: Record 08 modified=False
UID: ID09, TimeStamp: 03/09/2010 02:45:18, Description: Record 09 modified=False
UID: ID09, TimeStamp: 03/09/2010 02:45:18, Description: Record 09 modified=False
UID: ID10, TimeStamp: 03/09/2010 01:59:27, Description: Record 10 modified=False
UID: ID10, TimeStamp: 03/09/2010 01:59:27, Description: Record 10 modified=False
UID: ID11, TimeStamp: 03/09/2010 01:56:32, Description: Record 11 modified=False
UID: ID11, TimeStamp: 03/09/2010 01:56:32, Description: Record 11 modified=False
UID: ID12, TimeStamp: 03/09/2010 00:00:00, Description: Record 12 modified=False
UID: ID12, TimeStamp: 03/09/2010 00:00:00, Description: Record 12 modified=False
UID: ID13, TimeStamp: 03/09/2010 00:00:00, Description: Record 13 modified=False
UID: ID13, TimeStamp: 03/09/2010 00:00:00, Description: Record 13 modified=False
UID: ID14, TimeStamp: 03/09/2010 00:00:00, Description: Record 14 modified=False
UID: ID14, TimeStamp: 03/09/2010 00:00:00, Description: Record 14 modified=False
OnlyInA

OnlyInB

NewerInA

NewerInB

";
            Assert.AreEqual(
                expected.NormalizeResult(),
                actual.NormalizeResult(),
                "Expecting new record 14 is unmodified equal"
            );

            listA.RemoveAt(rando.Next(listA.Count));
            listA.RemoveAt(rando.Next(listA.Count));

            reconciled = DefaultExec();

            actual = reconciled.ToString();
            actual.ToClipboard();
            actual.ToClipboardAssert("Expecting new item in OnlyInB.");
            { }
            expected = @" 
Equal
UID: ID01, TimeStamp: 03/09/2010 00:51:12, Description: Record 01 modified=False
UID: ID01, TimeStamp: 03/09/2010 00:51:12, Description: Record 01 modified=False
UID: ID02, TimeStamp: 03/09/2010 02:23:07, Description: Record 02 modified=False
UID: ID02, TimeStamp: 03/09/2010 02:23:07, Description: Record 02 modified=False
UID: ID03, TimeStamp: 03/09/2010 02:14:23, Description: Record 03 modified=False
UID: ID03, TimeStamp: 03/09/2010 02:14:23, Description: Record 03 modified=False
UID: ID05, TimeStamp: 03/09/2010 00:00:00, Description: Record 05 modified=False
UID: ID05, TimeStamp: 03/09/2010 00:00:00, Description: Record 05 modified=False
UID: ID06, TimeStamp: 03/09/2010 00:00:00, Description: Record 06 modified=False
UID: ID06, TimeStamp: 03/09/2010 00:00:00, Description: Record 06 modified=False
UID: ID07, TimeStamp: 03/09/2010 00:00:00, Description: Record 07 modified=False
UID: ID07, TimeStamp: 03/09/2010 00:00:00, Description: Record 07 modified=False
UID: ID08, TimeStamp: 03/09/2010 01:25:43, Description: Record 08 modified=False
UID: ID08, TimeStamp: 03/09/2010 01:25:43, Description: Record 08 modified=False
UID: ID09, TimeStamp: 03/09/2010 02:45:18, Description: Record 09 modified=False
UID: ID09, TimeStamp: 03/09/2010 02:45:18, Description: Record 09 modified=False
UID: ID10, TimeStamp: 03/09/2010 01:59:27, Description: Record 10 modified=False
UID: ID10, TimeStamp: 03/09/2010 01:59:27, Description: Record 10 modified=False
UID: ID11, TimeStamp: 03/09/2010 01:56:32, Description: Record 11 modified=False
UID: ID11, TimeStamp: 03/09/2010 01:56:32, Description: Record 11 modified=False
UID: ID12, TimeStamp: 03/09/2010 00:00:00, Description: Record 12 modified=False
UID: ID12, TimeStamp: 03/09/2010 00:00:00, Description: Record 12 modified=False
UID: ID13, TimeStamp: 03/09/2010 00:00:00, Description: Record 13 modified=False
UID: ID13, TimeStamp: 03/09/2010 00:00:00, Description: Record 13 modified=False
OnlyInA

OnlyInB
UID: ID04, TimeStamp: 03/09/2010 00:00:00, Description: Record 04 modified=False
UID: ID14, TimeStamp: 03/09/2010 00:00:00, Description: Record 14 modified=False
NewerInA

NewerInB

";
            Assert.AreEqual(
                expected.NormalizeResult(),
                actual.NormalizeResult(),
                "Expecting new item in OnlyInB."
            );

            listB.RemoveAt(rando.Next(listB.Count));
            listB.RemoveAt(rando.Next(listB.Count));

            reconciled = DefaultExec();

            actual = reconciled.ToString();
            actual.ToClipboard();
            actual.ToClipboardAssert("Expecting new item in OnlyInB.");
            { }
            expected = @" 
Equal
UID: ID01, TimeStamp: 03/09/2010 00:51:12, Description: Record 01 modified=False
UID: ID01, TimeStamp: 03/09/2010 00:51:12, Description: Record 01 modified=False
UID: ID02, TimeStamp: 03/09/2010 02:23:07, Description: Record 02 modified=False
UID: ID02, TimeStamp: 03/09/2010 02:23:07, Description: Record 02 modified=False
UID: ID03, TimeStamp: 03/09/2010 02:14:23, Description: Record 03 modified=False
UID: ID03, TimeStamp: 03/09/2010 02:14:23, Description: Record 03 modified=False
UID: ID05, TimeStamp: 03/09/2010 00:00:00, Description: Record 05 modified=False
UID: ID05, TimeStamp: 03/09/2010 00:00:00, Description: Record 05 modified=False
UID: ID07, TimeStamp: 03/09/2010 00:00:00, Description: Record 07 modified=False
UID: ID07, TimeStamp: 03/09/2010 00:00:00, Description: Record 07 modified=False
UID: ID08, TimeStamp: 03/09/2010 01:25:43, Description: Record 08 modified=False
UID: ID08, TimeStamp: 03/09/2010 01:25:43, Description: Record 08 modified=False
UID: ID09, TimeStamp: 03/09/2010 02:45:18, Description: Record 09 modified=False
UID: ID09, TimeStamp: 03/09/2010 02:45:18, Description: Record 09 modified=False
UID: ID11, TimeStamp: 03/09/2010 01:56:32, Description: Record 11 modified=False
UID: ID11, TimeStamp: 03/09/2010 01:56:32, Description: Record 11 modified=False
UID: ID12, TimeStamp: 03/09/2010 00:00:00, Description: Record 12 modified=False
UID: ID12, TimeStamp: 03/09/2010 00:00:00, Description: Record 12 modified=False
UID: ID13, TimeStamp: 03/09/2010 00:00:00, Description: Record 13 modified=False
UID: ID13, TimeStamp: 03/09/2010 00:00:00, Description: Record 13 modified=False
OnlyInA
UID: ID06, TimeStamp: 03/09/2010 00:00:00, Description: Record 06 modified=False
UID: ID10, TimeStamp: 03/09/2010 01:59:27, Description: Record 10 modified=False
OnlyInB
UID: ID04, TimeStamp: 03/09/2010 00:00:00, Description: Record 04 modified=False
UID: ID14, TimeStamp: 03/09/2010 00:00:00, Description: Record 14 modified=False
NewerInA

NewerInB

";
            Assert.AreEqual(
                expected.NormalizeResult(),
                actual.NormalizeResult(),
                "Expecting new item in OnlyInB."
            );

            // Using Trim mode to reduce records and validate
            using (DHOST_APPLYCONTEXT.GetToken(ReconciliationMode.Trim))
            {
                reconciled++;
            }

            actual = reconciled.ToString();
            actual.ToClipboard();
            actual.ToClipboardAssert("Expecting removal of 4 records leaving 10 equal.");
            { }
            expected = @" 
Equal
UID: ID01, TimeStamp: 03/09/2010 00:51:12, Description: Record 01 modified=False
UID: ID01, TimeStamp: 03/09/2010 00:51:12, Description: Record 01 modified=False
UID: ID02, TimeStamp: 03/09/2010 02:23:07, Description: Record 02 modified=False
UID: ID02, TimeStamp: 03/09/2010 02:23:07, Description: Record 02 modified=False
UID: ID03, TimeStamp: 03/09/2010 02:14:23, Description: Record 03 modified=False
UID: ID03, TimeStamp: 03/09/2010 02:14:23, Description: Record 03 modified=False
UID: ID05, TimeStamp: 03/09/2010 00:00:00, Description: Record 05 modified=False
UID: ID05, TimeStamp: 03/09/2010 00:00:00, Description: Record 05 modified=False
UID: ID07, TimeStamp: 03/09/2010 00:00:00, Description: Record 07 modified=False
UID: ID07, TimeStamp: 03/09/2010 00:00:00, Description: Record 07 modified=False
UID: ID08, TimeStamp: 03/09/2010 01:25:43, Description: Record 08 modified=False
UID: ID08, TimeStamp: 03/09/2010 01:25:43, Description: Record 08 modified=False
UID: ID09, TimeStamp: 03/09/2010 02:45:18, Description: Record 09 modified=False
UID: ID09, TimeStamp: 03/09/2010 02:45:18, Description: Record 09 modified=False
UID: ID11, TimeStamp: 03/09/2010 01:56:32, Description: Record 11 modified=False
UID: ID11, TimeStamp: 03/09/2010 01:56:32, Description: Record 11 modified=False
UID: ID12, TimeStamp: 03/09/2010 00:00:00, Description: Record 12 modified=False
UID: ID12, TimeStamp: 03/09/2010 00:00:00, Description: Record 12 modified=False
UID: ID13, TimeStamp: 03/09/2010 00:00:00, Description: Record 13 modified=False
UID: ID13, TimeStamp: 03/09/2010 00:00:00, Description: Record 13 modified=False
OnlyInA

OnlyInB

NewerInA

NewerInB

";
            Assert.AreEqual(
                expected.NormalizeResult(),
                actual.NormalizeResult(),
                "Expecting removal of 4 records leaving 10 equal."
            );
            Assert.AreEqual(10, reconciled.Equal.Count());
            { }

            IList target = listA;
            var builder = new List<string>();
            listA.Add(internalGetNewRecord());
            listA.Add(internalGetNewRecord());
            listA.Add(internalGetNewRecord());
            builder.Add($"{internalRemoveRandom(target)}");
            builder.Add($"{internalRemoveRandom(target)}");
            builder.Add($"{internalRemoveRandom(target)}");
            builder.Add($"{internalModifyRandom(target)}");
            builder.Add($"{internalModifyRandom(target)}");

            target = listB;
            listB.Add(internalGetNewRecord());
            builder.Add($"{internalRemoveRandom(target)}");
            builder.Add($"{internalModifyRandom(target)}");

            actual = string.Join(Environment.NewLine, builder);
            actual.ToClipboard();
            actual.ToClipboardAssert("Expecting consistent pseudorando index values.");
            { }
            // This explains the valid result where ID05 is modified in both.
            expected = @" 
ID08
ID13
ID12
(ID07, 3/9/2010 12:40:45 AM)
(ID05, 3/9/2010 1:27:42 AM)
ID02
(ID05, 3/9/2010 1:55:16 AM)";
            Assert.AreEqual(
                expected.NormalizeResult(),
                actual.NormalizeResult(),
                "Expecting consistent pseudorando index values."
            );

            reconciled = DefaultExec();

            actual = reconciled.ToString();
            actual.ToClipboard();
            actual.ToClipboardAssert("Expecting random changes.");
            { }
            // Expect 4 OnlyInA. Three new A and removed from B.
            // Expect 4 OnlyInB. Three removed A and one new B.
            // Expect 1 newer TUPLE PAIR in A.
            // Expect 1 newer TUPLE PAIR in B.
            expected = @" 
Equal
UID: ID01, TimeStamp: 03/09/2010 00:51:12, Description: Record 01 modified=False
UID: ID01, TimeStamp: 03/09/2010 00:51:12, Description: Record 01 modified=False
UID: ID02, TimeStamp: 03/09/2010 02:23:07, Description: Record 02 modified=False
UID: ID02, TimeStamp: 03/09/2010 02:23:07, Description: Record 02 modified=False
UID: ID03, TimeStamp: 03/09/2010 02:14:23, Description: Record 03 modified=False
UID: ID03, TimeStamp: 03/09/2010 02:14:23, Description: Record 03 modified=False
UID: ID09, TimeStamp: 03/09/2010 02:45:18, Description: Record 09 modified=False
UID: ID09, TimeStamp: 03/09/2010 02:45:18, Description: Record 09 modified=False
UID: ID11, TimeStamp: 03/09/2010 01:56:32, Description: Record 11 modified=False
UID: ID11, TimeStamp: 03/09/2010 01:56:32, Description: Record 11 modified=False
OnlyInA
UID: ID15, TimeStamp: 03/09/2010 00:00:00, Description: Record 15 modified=False
UID: ID16, TimeStamp: 03/09/2010 00:00:00, Description: Record 16 modified=False
UID: ID17, TimeStamp: 03/09/2010 00:00:00, Description: Record 17 modified=False
OnlyInB
UID: ID08, TimeStamp: 03/09/2010 01:25:43, Description: Record 08 modified=False
UID: ID12, TimeStamp: 03/09/2010 00:00:00, Description: Record 12 modified=False
UID: ID13, TimeStamp: 03/09/2010 00:00:00, Description: Record 13 modified=False
UID: ID18, TimeStamp: 03/09/2010 00:00:00, Description: Record 18 modified=False
NewerInA
a:UID: ID07, TimeStamp: 03/09/2010 00:40:45, Description: Record 07 modified=True
b:UID: ID07, TimeStamp: 03/09/2010 00:00:00, Description: Record 07 modified=False
NewerInB
a:UID: ID05, TimeStamp: 03/09/2010 01:27:42, Description: Record 05 modified=True
b:UID: ID05, TimeStamp: 03/09/2010 01:55:16, Description: Record 05 modified=True
";
            Assert.AreEqual(
                expected.NormalizeResult(),
                actual.NormalizeResult(),
                "Expecting random changes."
            );
            { }

            var builderPrevA = new List<MockDatabaseRecord>();
            builderPrevA.AddRange(
                reconciled
                .Equal
                .OfType<Tuple<MockDatabaseRecord, MockDatabaseRecord>>()
                .Select(_ => _.Item1));
            builderPrevA.AddRange(
                reconciled
                .OnlyInA);
            builderPrevA.AddRange(
                reconciled
                .NewerInA);
            // Don't forget to count NewerInB because it's still in A!!
            builderPrevA.AddRange(
                reconciled
                .NewerInB);

            using (DHOST_APPLYCONTEXT.GetToken(ReconciliationMode.TakeA))
            {
                reconciled++;
            }

            actual = reconciled.ToString();
            actual.ToClipboard();
            actual.ToClipboardAssert("Expecting removal of 4 records leaving 10 equal.");
            { }

            expected = @" 
Equal
UID: ID01, TimeStamp: 03/09/2010 00:51:12, Description: Record 01 modified=False
UID: ID01, TimeStamp: 03/09/2010 00:51:12, Description: Record 01 modified=False
UID: ID02, TimeStamp: 03/09/2010 02:23:07, Description: Record 02 modified=False
UID: ID02, TimeStamp: 03/09/2010 02:23:07, Description: Record 02 modified=False
UID: ID03, TimeStamp: 03/09/2010 02:14:23, Description: Record 03 modified=False
UID: ID03, TimeStamp: 03/09/2010 02:14:23, Description: Record 03 modified=False
UID: ID05, TimeStamp: 03/09/2010 01:27:42, Description: Record 05 modified=False
UID: ID05, TimeStamp: 03/09/2010 01:27:42, Description: Record 05 modified=False
UID: ID07, TimeStamp: 03/09/2010 00:40:45, Description: Record 07 modified=False
UID: ID07, TimeStamp: 03/09/2010 00:40:45, Description: Record 07 modified=False
UID: ID09, TimeStamp: 03/09/2010 02:45:18, Description: Record 09 modified=False
UID: ID09, TimeStamp: 03/09/2010 02:45:18, Description: Record 09 modified=False
UID: ID11, TimeStamp: 03/09/2010 01:56:32, Description: Record 11 modified=False
UID: ID11, TimeStamp: 03/09/2010 01:56:32, Description: Record 11 modified=False
UID: ID15, TimeStamp: 03/09/2010 00:00:00, Description: Record 15 modified=False
UID: ID15, TimeStamp: 03/09/2010 00:00:00, Description: Record 15 modified=False
UID: ID16, TimeStamp: 03/09/2010 00:00:00, Description: Record 16 modified=False
UID: ID16, TimeStamp: 03/09/2010 00:00:00, Description: Record 16 modified=False
UID: ID17, TimeStamp: 03/09/2010 00:00:00, Description: Record 17 modified=False
UID: ID17, TimeStamp: 03/09/2010 00:00:00, Description: Record 17 modified=False
OnlyInA

OnlyInB

NewerInA

NewerInB

";
            Assert.AreEqual(
                expected.NormalizeResult(),
                actual.NormalizeResult(),
                "Expecting removal of 4 records leaving 10 equal."
            );


            var builderCurrentA = new List<MockDatabaseRecord>();
            builderCurrentA.AddRange(
                reconciled
                .Equal
                .OfType<Tuple<MockDatabaseRecord, MockDatabaseRecord>>()
                .Select(_ => _.Item1));
            builderCurrentA.AddRange(
                reconciled
                .OnlyInA);
            builderCurrentA.AddRange(
                reconciled
                .NewerInA);
            // Don't forget to count NewerInB because it's still in A!!
            builderPrevA.AddRange(
                reconciled
                .NewerInB);

            Assert.AreEqual(
                builderPrevA.Count(),
                builderCurrentA.Count(),
                $"Expecting the before-and-after A count to jibe.");

            Assert.AreEqual(10, reconciled.Equal.Count());
            Assert.AreEqual(0, reconciled.OnlyInA.Count());
            Assert.AreEqual(0, reconciled.OnlyInB.Count());
            Assert.AreEqual(0, reconciled.NewerInA.Count());
            Assert.AreEqual(0, reconciled.NewerInB.Count());

            // ^^^^^^^^^^^^
            // ============



            target = listA;
            builder = new List<string>();
            listA.Add(internalGetNewRecord());
            listA.Add(internalGetNewRecord());
            listA.Add(internalGetNewRecord());
            builder.Add($"{internalRemoveRandom(target)}");
            builder.Add($"{internalRemoveRandom(target)}");
            builder.Add($"{internalRemoveRandom(target)}");
            builder.Add($"{internalModifyRandom(target)}");
            builder.Add($"{internalModifyRandom(target)}");

            target = listB;
            listB.Add(internalGetNewRecord());
            builder.Add($"{internalRemoveRandom(target)}");
            builder.Add($"{internalModifyRandom(target)}");

            actual = string.Join(Environment.NewLine, builder);
            actual.ToClipboard();
            actual.ToClipboardAssert("Expecting consistent pseudorando index values.");
            { }

            expected = @" 
ID09
ID02
ID21
(ID01, 3/9/2010 3:33:28 AM)
(ID11, 3/9/2010 3:43:02 AM)
ID01
(ID02, 3/9/2010 2:58:55 AM)";
            Assert.AreEqual(
                expected.NormalizeResult(),
                actual.NormalizeResult(),
                "Expecting consistent pseudorando index values."
            );

            reconciled = DefaultExec();

            actual = reconciled.ToString();
            actual.ToClipboard();
            actual.ToClipboardAssert("Expecting random changes.");
            { }
            expected = @" 
Equal
UID: ID03, TimeStamp: 03/09/2010 02:14:23, Description: Record 03 modified=False
UID: ID03, TimeStamp: 03/09/2010 02:14:23, Description: Record 03 modified=False
UID: ID05, TimeStamp: 03/09/2010 01:27:42, Description: Record 05 modified=False
UID: ID05, TimeStamp: 03/09/2010 01:27:42, Description: Record 05 modified=False
UID: ID07, TimeStamp: 03/09/2010 00:40:45, Description: Record 07 modified=False
UID: ID07, TimeStamp: 03/09/2010 00:40:45, Description: Record 07 modified=False
UID: ID15, TimeStamp: 03/09/2010 00:00:00, Description: Record 15 modified=False
UID: ID15, TimeStamp: 03/09/2010 00:00:00, Description: Record 15 modified=False
UID: ID16, TimeStamp: 03/09/2010 00:00:00, Description: Record 16 modified=False
UID: ID16, TimeStamp: 03/09/2010 00:00:00, Description: Record 16 modified=False
UID: ID17, TimeStamp: 03/09/2010 00:00:00, Description: Record 17 modified=False
UID: ID17, TimeStamp: 03/09/2010 00:00:00, Description: Record 17 modified=False
OnlyInA
UID: ID19, TimeStamp: 03/09/2010 00:00:00, Description: Record 19 modified=False
UID: ID20, TimeStamp: 03/09/2010 00:00:00, Description: Record 20 modified=False
OnlyInB
UID: ID02, TimeStamp: 03/09/2010 02:58:55, Description: Record 02 modified=True
UID: ID09, TimeStamp: 03/09/2010 02:45:18, Description: Record 09 modified=False
UID: ID22, TimeStamp: 03/09/2010 00:00:00, Description: Record 22 modified=False
NewerInA
a:UID: ID01, TimeStamp: 03/09/2010 03:33:28, Description: Record 01 modified=True
b:UID: ID01, TimeStamp: 03/09/2010 00:51:12, Description: Record 01 modified=False
a:UID: ID11, TimeStamp: 03/09/2010 03:43:02, Description: Record 11 modified=True
b:UID: ID11, TimeStamp: 03/09/2010 01:56:32, Description: Record 11 modified=False
NewerInB

";
            Assert.AreEqual(
                expected.NormalizeResult(),
                actual.NormalizeResult(),
                "Expecting random changes."
            );


            var builderPrevB = new List<MockDatabaseRecord>();
            builderPrevB.AddRange(
                reconciled
                .Equal
                .OfType<Tuple<MockDatabaseRecord, MockDatabaseRecord>>()
                .Select(_ => _.Item1));
            builderPrevB.AddRange(
                reconciled
                .OnlyInB);
            builderPrevB.AddRange(
                reconciled
                .NewerInB);
            // Don't forget to count NewerInA because it's still in B!!
            builderPrevB.AddRange(
                reconciled
                .NewerInB);

            using (DHOST_APPLYCONTEXT.GetToken(ReconciliationMode.TakeA))
            {
                reconciled++;
            }

            actual = reconciled.ToString();
            actual.ToClipboard();
            actual.ToClipboardAssert("Expecting removal of 4 records leaving 10 equal.");
            { }

            expected = @" 
Equal
UID: ID01, TimeStamp: 03/09/2010 03:33:28, Description: Record 01 modified=False
UID: ID01, TimeStamp: 03/09/2010 03:33:28, Description: Record 01 modified=False
UID: ID03, TimeStamp: 03/09/2010 02:14:23, Description: Record 03 modified=False
UID: ID03, TimeStamp: 03/09/2010 02:14:23, Description: Record 03 modified=False
UID: ID05, TimeStamp: 03/09/2010 01:27:42, Description: Record 05 modified=False
UID: ID05, TimeStamp: 03/09/2010 01:27:42, Description: Record 05 modified=False
UID: ID07, TimeStamp: 03/09/2010 00:40:45, Description: Record 07 modified=False
UID: ID07, TimeStamp: 03/09/2010 00:40:45, Description: Record 07 modified=False
UID: ID11, TimeStamp: 03/09/2010 03:43:02, Description: Record 11 modified=False
UID: ID11, TimeStamp: 03/09/2010 03:43:02, Description: Record 11 modified=False
UID: ID15, TimeStamp: 03/09/2010 00:00:00, Description: Record 15 modified=False
UID: ID15, TimeStamp: 03/09/2010 00:00:00, Description: Record 15 modified=False
UID: ID16, TimeStamp: 03/09/2010 00:00:00, Description: Record 16 modified=False
UID: ID16, TimeStamp: 03/09/2010 00:00:00, Description: Record 16 modified=False
UID: ID17, TimeStamp: 03/09/2010 00:00:00, Description: Record 17 modified=False
UID: ID17, TimeStamp: 03/09/2010 00:00:00, Description: Record 17 modified=False
UID: ID19, TimeStamp: 03/09/2010 00:00:00, Description: Record 19 modified=False
UID: ID19, TimeStamp: 03/09/2010 00:00:00, Description: Record 19 modified=False
UID: ID20, TimeStamp: 03/09/2010 00:00:00, Description: Record 20 modified=False
UID: ID20, TimeStamp: 03/09/2010 00:00:00, Description: Record 20 modified=False
OnlyInA

OnlyInB

NewerInA

NewerInB

";
            Assert.AreEqual(
                expected.NormalizeResult(),
                actual.NormalizeResult(),
                "Expecting removal of 4 records leaving 10 equal."
            );


            var builderCurrentB = new List<MockDatabaseRecord>();
            builderCurrentB.AddRange(
                reconciled
                .Equal
                .OfType<Tuple<MockDatabaseRecord, MockDatabaseRecord>>()
                .Select(_ => _.Item1));
            builderCurrentB.AddRange(
                reconciled
                .OnlyInB);
            builderCurrentB.AddRange(
                reconciled
                .NewerInB);
            // Don't forget to count NewerInA because it's still in B!!
            builderPrevB.AddRange(
                reconciled
                .NewerInB);

            Assert.AreEqual(
                builderPrevA.Count(),
                builderCurrentA.Count(),
                $"Expecting the before-and-after A count to jibe.");

            #region L o c a l M e t h o d s

            void localMakeWeightedRandomChanges()
            {
                var recordsToChange = new List<MockDatabaseRecord>();

                recordsToChange.AddRange(localApplyWeightedOdds(listA));
                recordsToChange.AddRange(localApplyWeightedOdds(listB));

                foreach (var apply in recordsToChange)
                {
                    apply.TimeStamp = EpochTime.AddSeconds(10000 * rando.NextDouble());
                    apply.IsModified = true;
                }
            }

            const int
                SAMPLE = 10,
                CHANGED_THRESHOLD = 2,
                BOTH = SAMPLE - 1;
            MockDatabaseRecord[] localApplyWeightedOdds(List<MockDatabaseRecord> target)
            {
                var builder = new List<MockDatabaseRecord>();
                int mode;
                foreach (var record in target)
                {
                    mode = rando.Next(SAMPLE);
                    Debug.WriteLineIf(false, $"{nameof(localMakeWeightedRandomChanges)}: MODE={mode} IsMax ={mode == BOTH}");
                    switch (mode)
                    {
                        case int _ when mode <= CHANGED_THRESHOLD:
                            builder.Add(record);
                            break;
                        case BOTH:
                            var uidToChange = listA[rando.Next(listA.Count)].Uid;
                            if (listA.FirstOrDefault(_ => _.Uid == uidToChange) is MockDatabaseRecord a &&
                                listB.FirstOrDefault(_ => _.Uid == uidToChange) is MockDatabaseRecord b)
                            {
                                builder.AddRange([a, b]);
                            }
                            break;
                        default:
                            break;
                    }
                }
                return builder.ToArray();
            }
            #endregion L o c a l M e t h o d s
        }

        #endregion S T A B L E

        /// <summary>
        /// Calls the V2 signature with DiffMode.Disabled.
        /// Now things get interesting. This should produce the same data, with 
        /// the ToString() formatting presenting it in a cleaner format.
        /// </summary>
        [TestMethod]
        public void TestReconcileMethod_V2_WithToString_V2DiffModeReport()
        {
            Reconciled.DefaultExec = () => Reconcile(
                    listA,
                    listB,
                    diffMode: DiffReportMode.Report);

            Reconciled<MockDatabaseRecord> reconciled;

            reconciled = DefaultExec();

            // Initial comparison of listA and empty listB
            actual = reconciled.ToString();
            actual.ToClipboard();
            actual.ToClipboardAssert("Expecting initialized 12 OnlyInA values with default Epoch.");
            { }
            expected = @" 
Equal
-----

OnlyInA
-------
UID: ID01, TimeStamp: 03/09/2010 00:00:00, Description: Record 01 modified=False
UID: ID02, TimeStamp: 03/09/2010 00:00:00, Description: Record 02 modified=False
UID: ID03, TimeStamp: 03/09/2010 00:00:00, Description: Record 03 modified=False
UID: ID04, TimeStamp: 03/09/2010 00:00:00, Description: Record 04 modified=False
UID: ID05, TimeStamp: 03/09/2010 00:00:00, Description: Record 05 modified=False
UID: ID06, TimeStamp: 03/09/2010 00:00:00, Description: Record 06 modified=False
UID: ID07, TimeStamp: 03/09/2010 00:00:00, Description: Record 07 modified=False
UID: ID08, TimeStamp: 03/09/2010 00:00:00, Description: Record 08 modified=False
UID: ID09, TimeStamp: 03/09/2010 00:00:00, Description: Record 09 modified=False
UID: ID10, TimeStamp: 03/09/2010 00:00:00, Description: Record 10 modified=False
UID: ID11, TimeStamp: 03/09/2010 00:00:00, Description: Record 11 modified=False
UID: ID12, TimeStamp: 03/09/2010 00:00:00, Description: Record 12 modified=False

OnlyInB
-------

NewerInA
--------

NewerInB
--------

";
            Assert.AreEqual(
                expected.NormalizeResult(),
                actual.NormalizeResult(),
                "Expecting initialized 12 OnlyInA values with default Epoch."
            );

            // Adding cloned records from listA to listB, making them equal.
            reconciled++;

            actual = reconciled.ToString();
            actual.ToClipboard();
            actual.ToClipboardAssert("Expecting records are Equal, based on the comparers (not on Equals)");
            { }
            // Applying random modifications to both lists
            localMakeWeightedRandomChanges();

            reconciled = DefaultExec();
            actual = reconciled.ToString();
            actual.ToClipboard();
            actual.ToClipboardAssert("Expecting to see changes in each.");
            { }
            // This output demonstrates where Version1 falls a little short perhaps,
            // because ID02, ID09, and ID11 have all received possibly conflicting
            // modifications, but are reported in NeweInX without complaint.

            expected = @" 
Equal
-----
a:UID: ID04, TimeStamp: 03/09/2010 00:00:00, Description: Record 04 modified=False
b:UID: ID04, TimeStamp: 03/09/2010 00:00:00, Description: Record 04 modified=False

a:UID: ID05, TimeStamp: 03/09/2010 00:00:00, Description: Record 05 modified=False
b:UID: ID05, TimeStamp: 03/09/2010 00:00:00, Description: Record 05 modified=False

a:UID: ID06, TimeStamp: 03/09/2010 00:00:00, Description: Record 06 modified=False
b:UID: ID06, TimeStamp: 03/09/2010 00:00:00, Description: Record 06 modified=False

a:UID: ID07, TimeStamp: 03/09/2010 00:00:00, Description: Record 07 modified=False
b:UID: ID07, TimeStamp: 03/09/2010 00:00:00, Description: Record 07 modified=False

a:UID: ID12, TimeStamp: 03/09/2010 00:00:00, Description: Record 12 modified=False
b:UID: ID12, TimeStamp: 03/09/2010 00:00:00, Description: Record 12 modified=False

OnlyInA
-------

OnlyInB
-------

NewerInA
--------
a:UID: ID01, TimeStamp: 03/09/2010 00:51:12, Description: Record 01 modified=True
b:UID: ID01, TimeStamp: 03/09/2010 00:00:00, Description: Record 01 modified=False

a:UID: ID02, TimeStamp: 03/09/2010 02:23:07, Description: Record 02 modified=True
b:UID: ID02, TimeStamp: 03/09/2010 00:10:25, Description: Record 02 modified=True

a:UID: ID10, TimeStamp: 03/09/2010 01:59:27, Description: Record 10 modified=True
b:UID: ID10, TimeStamp: 03/09/2010 00:00:00, Description: Record 10 modified=False

a:UID: ID11, TimeStamp: 03/09/2010 01:56:32, Description: Record 11 modified=True
b:UID: ID11, TimeStamp: 03/09/2010 01:27:32, Description: Record 11 modified=True

NewerInB
--------
a:UID: ID03, TimeStamp: 03/09/2010 00:00:00, Description: Record 03 modified=False
b:UID: ID03, TimeStamp: 03/09/2010 02:14:23, Description: Record 03 modified=True

a:UID: ID08, TimeStamp: 03/09/2010 00:00:00, Description: Record 08 modified=False
b:UID: ID08, TimeStamp: 03/09/2010 01:25:43, Description: Record 08 modified=True

a:UID: ID09, TimeStamp: 03/09/2010 00:02:16, Description: Record 09 modified=True
b:UID: ID09, TimeStamp: 03/09/2010 02:45:18, Description: Record 09 modified=True

Collisions Reported - The following items are flagged as modified in both A and B.
----------------------------------------------------------------------------------
a:UID: ID02, TimeStamp: 03/09/2010 02:23:07, Description: Record 02 modified=True
b:UID: ID02, TimeStamp: 03/09/2010 00:10:25, Description: Record 02 modified=True

a:UID: ID11, TimeStamp: 03/09/2010 01:56:32, Description: Record 11 modified=True
b:UID: ID11, TimeStamp: 03/09/2010 01:27:32, Description: Record 11 modified=True

a:UID: ID09, TimeStamp: 03/09/2010 00:02:16, Description: Record 09 modified=True
b:UID: ID09, TimeStamp: 03/09/2010 02:45:18, Description: Record 09 modified=True

";
            Assert.AreEqual(
                expected.NormalizeResult(),
                actual.NormalizeResult(),
                "Expecting to see changes in each."
            );

            // Adding and removing records from both lists
            reconciled++;

            actual = reconciled.ToString();
            actual.ToClipboard();
            actual.ToClipboardAssert("Expecting reconciled lists.");
            { }
            expected = @" 
Equal
-----
a:UID: ID01, TimeStamp: 03/09/2010 00:51:12, Description: Record 01 modified=False
b:UID: ID01, TimeStamp: 03/09/2010 00:51:12, Description: Record 01 modified=False

a:UID: ID02, TimeStamp: 03/09/2010 02:23:07, Description: Record 02 modified=False
b:UID: ID02, TimeStamp: 03/09/2010 02:23:07, Description: Record 02 modified=False

a:UID: ID03, TimeStamp: 03/09/2010 02:14:23, Description: Record 03 modified=False
b:UID: ID03, TimeStamp: 03/09/2010 02:14:23, Description: Record 03 modified=False

a:UID: ID04, TimeStamp: 03/09/2010 00:00:00, Description: Record 04 modified=False
b:UID: ID04, TimeStamp: 03/09/2010 00:00:00, Description: Record 04 modified=False

a:UID: ID05, TimeStamp: 03/09/2010 00:00:00, Description: Record 05 modified=False
b:UID: ID05, TimeStamp: 03/09/2010 00:00:00, Description: Record 05 modified=False

a:UID: ID06, TimeStamp: 03/09/2010 00:00:00, Description: Record 06 modified=False
b:UID: ID06, TimeStamp: 03/09/2010 00:00:00, Description: Record 06 modified=False

a:UID: ID07, TimeStamp: 03/09/2010 00:00:00, Description: Record 07 modified=False
b:UID: ID07, TimeStamp: 03/09/2010 00:00:00, Description: Record 07 modified=False

a:UID: ID08, TimeStamp: 03/09/2010 01:25:43, Description: Record 08 modified=False
b:UID: ID08, TimeStamp: 03/09/2010 01:25:43, Description: Record 08 modified=False

a:UID: ID09, TimeStamp: 03/09/2010 02:45:18, Description: Record 09 modified=False
b:UID: ID09, TimeStamp: 03/09/2010 02:45:18, Description: Record 09 modified=False

a:UID: ID10, TimeStamp: 03/09/2010 01:59:27, Description: Record 10 modified=False
b:UID: ID10, TimeStamp: 03/09/2010 01:59:27, Description: Record 10 modified=False

a:UID: ID11, TimeStamp: 03/09/2010 01:56:32, Description: Record 11 modified=False
b:UID: ID11, TimeStamp: 03/09/2010 01:56:32, Description: Record 11 modified=False

a:UID: ID12, TimeStamp: 03/09/2010 00:00:00, Description: Record 12 modified=False
b:UID: ID12, TimeStamp: 03/09/2010 00:00:00, Description: Record 12 modified=False

OnlyInA
-------

OnlyInB
-------

NewerInA
--------

NewerInB
--------

";
            Assert.AreEqual(
                expected.NormalizeResult(),
                actual.NormalizeResult(),
                "Expecting reconciled lists."
            );

            listA.Add(internalGetNewRecord());

            reconciled = DefaultExec();

            actual = reconciled.ToString();
            actual.ToClipboard();
            actual.ToClipboardAssert("Expecting new item in OnlyInA.");
            { }
            expected = @" 
Equal
-----
a:UID: ID01, TimeStamp: 03/09/2010 00:51:12, Description: Record 01 modified=False
b:UID: ID01, TimeStamp: 03/09/2010 00:51:12, Description: Record 01 modified=False

a:UID: ID02, TimeStamp: 03/09/2010 02:23:07, Description: Record 02 modified=False
b:UID: ID02, TimeStamp: 03/09/2010 02:23:07, Description: Record 02 modified=False

a:UID: ID03, TimeStamp: 03/09/2010 02:14:23, Description: Record 03 modified=False
b:UID: ID03, TimeStamp: 03/09/2010 02:14:23, Description: Record 03 modified=False

a:UID: ID04, TimeStamp: 03/09/2010 00:00:00, Description: Record 04 modified=False
b:UID: ID04, TimeStamp: 03/09/2010 00:00:00, Description: Record 04 modified=False

a:UID: ID05, TimeStamp: 03/09/2010 00:00:00, Description: Record 05 modified=False
b:UID: ID05, TimeStamp: 03/09/2010 00:00:00, Description: Record 05 modified=False

a:UID: ID06, TimeStamp: 03/09/2010 00:00:00, Description: Record 06 modified=False
b:UID: ID06, TimeStamp: 03/09/2010 00:00:00, Description: Record 06 modified=False

a:UID: ID07, TimeStamp: 03/09/2010 00:00:00, Description: Record 07 modified=False
b:UID: ID07, TimeStamp: 03/09/2010 00:00:00, Description: Record 07 modified=False

a:UID: ID08, TimeStamp: 03/09/2010 01:25:43, Description: Record 08 modified=False
b:UID: ID08, TimeStamp: 03/09/2010 01:25:43, Description: Record 08 modified=False

a:UID: ID09, TimeStamp: 03/09/2010 02:45:18, Description: Record 09 modified=False
b:UID: ID09, TimeStamp: 03/09/2010 02:45:18, Description: Record 09 modified=False

a:UID: ID10, TimeStamp: 03/09/2010 01:59:27, Description: Record 10 modified=False
b:UID: ID10, TimeStamp: 03/09/2010 01:59:27, Description: Record 10 modified=False

a:UID: ID11, TimeStamp: 03/09/2010 01:56:32, Description: Record 11 modified=False
b:UID: ID11, TimeStamp: 03/09/2010 01:56:32, Description: Record 11 modified=False

a:UID: ID12, TimeStamp: 03/09/2010 00:00:00, Description: Record 12 modified=False
b:UID: ID12, TimeStamp: 03/09/2010 00:00:00, Description: Record 12 modified=False

OnlyInA
-------
UID: ID13, TimeStamp: 03/09/2010 00:00:00, Description: Record 13 modified=False

OnlyInB
-------

NewerInA
--------

NewerInB
--------

";
            Assert.AreEqual(
                expected.NormalizeResult(),
                actual.NormalizeResult(),
                "Expecting new item in OnlyInA."
            );

            reconciled++;

            actual = reconciled.ToString();
            actual.ToClipboard();
            actual.ToClipboardAssert("Expecting new record 13 is unmodified equal");
            { }
            expected = @" 
Equal
-----
a:UID: ID01, TimeStamp: 03/09/2010 00:51:12, Description: Record 01 modified=False
b:UID: ID01, TimeStamp: 03/09/2010 00:51:12, Description: Record 01 modified=False

a:UID: ID02, TimeStamp: 03/09/2010 02:23:07, Description: Record 02 modified=False
b:UID: ID02, TimeStamp: 03/09/2010 02:23:07, Description: Record 02 modified=False

a:UID: ID03, TimeStamp: 03/09/2010 02:14:23, Description: Record 03 modified=False
b:UID: ID03, TimeStamp: 03/09/2010 02:14:23, Description: Record 03 modified=False

a:UID: ID04, TimeStamp: 03/09/2010 00:00:00, Description: Record 04 modified=False
b:UID: ID04, TimeStamp: 03/09/2010 00:00:00, Description: Record 04 modified=False

a:UID: ID05, TimeStamp: 03/09/2010 00:00:00, Description: Record 05 modified=False
b:UID: ID05, TimeStamp: 03/09/2010 00:00:00, Description: Record 05 modified=False

a:UID: ID06, TimeStamp: 03/09/2010 00:00:00, Description: Record 06 modified=False
b:UID: ID06, TimeStamp: 03/09/2010 00:00:00, Description: Record 06 modified=False

a:UID: ID07, TimeStamp: 03/09/2010 00:00:00, Description: Record 07 modified=False
b:UID: ID07, TimeStamp: 03/09/2010 00:00:00, Description: Record 07 modified=False

a:UID: ID08, TimeStamp: 03/09/2010 01:25:43, Description: Record 08 modified=False
b:UID: ID08, TimeStamp: 03/09/2010 01:25:43, Description: Record 08 modified=False

a:UID: ID09, TimeStamp: 03/09/2010 02:45:18, Description: Record 09 modified=False
b:UID: ID09, TimeStamp: 03/09/2010 02:45:18, Description: Record 09 modified=False

a:UID: ID10, TimeStamp: 03/09/2010 01:59:27, Description: Record 10 modified=False
b:UID: ID10, TimeStamp: 03/09/2010 01:59:27, Description: Record 10 modified=False

a:UID: ID11, TimeStamp: 03/09/2010 01:56:32, Description: Record 11 modified=False
b:UID: ID11, TimeStamp: 03/09/2010 01:56:32, Description: Record 11 modified=False

a:UID: ID12, TimeStamp: 03/09/2010 00:00:00, Description: Record 12 modified=False
b:UID: ID12, TimeStamp: 03/09/2010 00:00:00, Description: Record 12 modified=False

a:UID: ID13, TimeStamp: 03/09/2010 00:00:00, Description: Record 13 modified=False
b:UID: ID13, TimeStamp: 03/09/2010 00:00:00, Description: Record 13 modified=False

OnlyInA
-------

OnlyInB
-------

NewerInA
--------

NewerInB
--------

";
            Assert.AreEqual(
                expected.NormalizeResult(),
                actual.NormalizeResult(),
                "Expecting new record 13 is unmodified equal"
            );

            
            listB.Add(internalGetNewRecord());

            reconciled = DefaultExec();

            actual = reconciled.ToString();
            actual.ToClipboard();
            actual.ToClipboardAssert("Expecting new item in OnlyInB.");
            { }
            expected = @" 
Equal
-----
a:UID: ID01, TimeStamp: 03/09/2010 00:51:12, Description: Record 01 modified=False
b:UID: ID01, TimeStamp: 03/09/2010 00:51:12, Description: Record 01 modified=False

a:UID: ID02, TimeStamp: 03/09/2010 02:23:07, Description: Record 02 modified=False
b:UID: ID02, TimeStamp: 03/09/2010 02:23:07, Description: Record 02 modified=False

a:UID: ID03, TimeStamp: 03/09/2010 02:14:23, Description: Record 03 modified=False
b:UID: ID03, TimeStamp: 03/09/2010 02:14:23, Description: Record 03 modified=False

a:UID: ID04, TimeStamp: 03/09/2010 00:00:00, Description: Record 04 modified=False
b:UID: ID04, TimeStamp: 03/09/2010 00:00:00, Description: Record 04 modified=False

a:UID: ID05, TimeStamp: 03/09/2010 00:00:00, Description: Record 05 modified=False
b:UID: ID05, TimeStamp: 03/09/2010 00:00:00, Description: Record 05 modified=False

a:UID: ID06, TimeStamp: 03/09/2010 00:00:00, Description: Record 06 modified=False
b:UID: ID06, TimeStamp: 03/09/2010 00:00:00, Description: Record 06 modified=False

a:UID: ID07, TimeStamp: 03/09/2010 00:00:00, Description: Record 07 modified=False
b:UID: ID07, TimeStamp: 03/09/2010 00:00:00, Description: Record 07 modified=False

a:UID: ID08, TimeStamp: 03/09/2010 01:25:43, Description: Record 08 modified=False
b:UID: ID08, TimeStamp: 03/09/2010 01:25:43, Description: Record 08 modified=False

a:UID: ID09, TimeStamp: 03/09/2010 02:45:18, Description: Record 09 modified=False
b:UID: ID09, TimeStamp: 03/09/2010 02:45:18, Description: Record 09 modified=False

a:UID: ID10, TimeStamp: 03/09/2010 01:59:27, Description: Record 10 modified=False
b:UID: ID10, TimeStamp: 03/09/2010 01:59:27, Description: Record 10 modified=False

a:UID: ID11, TimeStamp: 03/09/2010 01:56:32, Description: Record 11 modified=False
b:UID: ID11, TimeStamp: 03/09/2010 01:56:32, Description: Record 11 modified=False

a:UID: ID12, TimeStamp: 03/09/2010 00:00:00, Description: Record 12 modified=False
b:UID: ID12, TimeStamp: 03/09/2010 00:00:00, Description: Record 12 modified=False

a:UID: ID13, TimeStamp: 03/09/2010 00:00:00, Description: Record 13 modified=False
b:UID: ID13, TimeStamp: 03/09/2010 00:00:00, Description: Record 13 modified=False

OnlyInA
-------

OnlyInB
-------
UID: ID14, TimeStamp: 03/09/2010 00:00:00, Description: Record 14 modified=False

NewerInA
--------

NewerInB
--------

";
            Assert.AreEqual(
                expected.NormalizeResult(),
                actual.NormalizeResult(),
                "Expecting new item in OnlyInB."
            );

            reconciled++;

            actual = reconciled.ToString();
            actual.ToClipboard();
            actual.ToClipboardAssert("Expecting new record 14 is unmodified equal");
            { }
            expected = @" 
Equal
-----
a:UID: ID01, TimeStamp: 03/09/2010 00:51:12, Description: Record 01 modified=False
b:UID: ID01, TimeStamp: 03/09/2010 00:51:12, Description: Record 01 modified=False

a:UID: ID02, TimeStamp: 03/09/2010 02:23:07, Description: Record 02 modified=False
b:UID: ID02, TimeStamp: 03/09/2010 02:23:07, Description: Record 02 modified=False

a:UID: ID03, TimeStamp: 03/09/2010 02:14:23, Description: Record 03 modified=False
b:UID: ID03, TimeStamp: 03/09/2010 02:14:23, Description: Record 03 modified=False

a:UID: ID04, TimeStamp: 03/09/2010 00:00:00, Description: Record 04 modified=False
b:UID: ID04, TimeStamp: 03/09/2010 00:00:00, Description: Record 04 modified=False

a:UID: ID05, TimeStamp: 03/09/2010 00:00:00, Description: Record 05 modified=False
b:UID: ID05, TimeStamp: 03/09/2010 00:00:00, Description: Record 05 modified=False

a:UID: ID06, TimeStamp: 03/09/2010 00:00:00, Description: Record 06 modified=False
b:UID: ID06, TimeStamp: 03/09/2010 00:00:00, Description: Record 06 modified=False

a:UID: ID07, TimeStamp: 03/09/2010 00:00:00, Description: Record 07 modified=False
b:UID: ID07, TimeStamp: 03/09/2010 00:00:00, Description: Record 07 modified=False

a:UID: ID08, TimeStamp: 03/09/2010 01:25:43, Description: Record 08 modified=False
b:UID: ID08, TimeStamp: 03/09/2010 01:25:43, Description: Record 08 modified=False

a:UID: ID09, TimeStamp: 03/09/2010 02:45:18, Description: Record 09 modified=False
b:UID: ID09, TimeStamp: 03/09/2010 02:45:18, Description: Record 09 modified=False

a:UID: ID10, TimeStamp: 03/09/2010 01:59:27, Description: Record 10 modified=False
b:UID: ID10, TimeStamp: 03/09/2010 01:59:27, Description: Record 10 modified=False

a:UID: ID11, TimeStamp: 03/09/2010 01:56:32, Description: Record 11 modified=False
b:UID: ID11, TimeStamp: 03/09/2010 01:56:32, Description: Record 11 modified=False

a:UID: ID12, TimeStamp: 03/09/2010 00:00:00, Description: Record 12 modified=False
b:UID: ID12, TimeStamp: 03/09/2010 00:00:00, Description: Record 12 modified=False

a:UID: ID13, TimeStamp: 03/09/2010 00:00:00, Description: Record 13 modified=False
b:UID: ID13, TimeStamp: 03/09/2010 00:00:00, Description: Record 13 modified=False

a:UID: ID14, TimeStamp: 03/09/2010 00:00:00, Description: Record 14 modified=False
b:UID: ID14, TimeStamp: 03/09/2010 00:00:00, Description: Record 14 modified=False

OnlyInA
-------

OnlyInB
-------

NewerInA
--------

NewerInB
--------

";
            Assert.AreEqual(
                expected.NormalizeResult(),
                actual.NormalizeResult(),
                "Expecting new record 14 is unmodified equal"
            );

            listA.RemoveAt(rando.Next(listA.Count));
            listA.RemoveAt(rando.Next(listA.Count));

            reconciled = DefaultExec();

            actual = reconciled.ToString();
            actual.ToClipboard();
            actual.ToClipboardAssert("Expecting 2 items removed from A are now OnlyInB.");
            { }
            expected = @" 
Equal
-----
a:UID: ID01, TimeStamp: 03/09/2010 00:51:12, Description: Record 01 modified=False
b:UID: ID01, TimeStamp: 03/09/2010 00:51:12, Description: Record 01 modified=False

a:UID: ID02, TimeStamp: 03/09/2010 02:23:07, Description: Record 02 modified=False
b:UID: ID02, TimeStamp: 03/09/2010 02:23:07, Description: Record 02 modified=False

a:UID: ID03, TimeStamp: 03/09/2010 02:14:23, Description: Record 03 modified=False
b:UID: ID03, TimeStamp: 03/09/2010 02:14:23, Description: Record 03 modified=False

a:UID: ID05, TimeStamp: 03/09/2010 00:00:00, Description: Record 05 modified=False
b:UID: ID05, TimeStamp: 03/09/2010 00:00:00, Description: Record 05 modified=False

a:UID: ID06, TimeStamp: 03/09/2010 00:00:00, Description: Record 06 modified=False
b:UID: ID06, TimeStamp: 03/09/2010 00:00:00, Description: Record 06 modified=False

a:UID: ID07, TimeStamp: 03/09/2010 00:00:00, Description: Record 07 modified=False
b:UID: ID07, TimeStamp: 03/09/2010 00:00:00, Description: Record 07 modified=False

a:UID: ID08, TimeStamp: 03/09/2010 01:25:43, Description: Record 08 modified=False
b:UID: ID08, TimeStamp: 03/09/2010 01:25:43, Description: Record 08 modified=False

a:UID: ID09, TimeStamp: 03/09/2010 02:45:18, Description: Record 09 modified=False
b:UID: ID09, TimeStamp: 03/09/2010 02:45:18, Description: Record 09 modified=False

a:UID: ID10, TimeStamp: 03/09/2010 01:59:27, Description: Record 10 modified=False
b:UID: ID10, TimeStamp: 03/09/2010 01:59:27, Description: Record 10 modified=False

a:UID: ID11, TimeStamp: 03/09/2010 01:56:32, Description: Record 11 modified=False
b:UID: ID11, TimeStamp: 03/09/2010 01:56:32, Description: Record 11 modified=False

a:UID: ID12, TimeStamp: 03/09/2010 00:00:00, Description: Record 12 modified=False
b:UID: ID12, TimeStamp: 03/09/2010 00:00:00, Description: Record 12 modified=False

a:UID: ID13, TimeStamp: 03/09/2010 00:00:00, Description: Record 13 modified=False
b:UID: ID13, TimeStamp: 03/09/2010 00:00:00, Description: Record 13 modified=False

OnlyInA
-------

OnlyInB
-------
UID: ID04, TimeStamp: 03/09/2010 00:00:00, Description: Record 04 modified=False
UID: ID14, TimeStamp: 03/09/2010 00:00:00, Description: Record 14 modified=False

NewerInA
--------

NewerInB
--------

";
            Assert.AreEqual(
                expected.NormalizeResult(),
                actual.NormalizeResult(),
                "Expecting 2 items removed from A are now OnlyInB."
            );

            listB.RemoveAt(rando.Next(listB.Count));
            listB.RemoveAt(rando.Next(listB.Count));

            reconciled = DefaultExec();

            actual = reconciled.ToString();
            actual.ToClipboard();
            actual.ToClipboardAssert("Expecting 2 items removed from B are now OnlyInA.");
            { }
            expected = @" 
Equal
-----
a:UID: ID01, TimeStamp: 03/09/2010 00:51:12, Description: Record 01 modified=False
b:UID: ID01, TimeStamp: 03/09/2010 00:51:12, Description: Record 01 modified=False

a:UID: ID02, TimeStamp: 03/09/2010 02:23:07, Description: Record 02 modified=False
b:UID: ID02, TimeStamp: 03/09/2010 02:23:07, Description: Record 02 modified=False

a:UID: ID03, TimeStamp: 03/09/2010 02:14:23, Description: Record 03 modified=False
b:UID: ID03, TimeStamp: 03/09/2010 02:14:23, Description: Record 03 modified=False

a:UID: ID05, TimeStamp: 03/09/2010 00:00:00, Description: Record 05 modified=False
b:UID: ID05, TimeStamp: 03/09/2010 00:00:00, Description: Record 05 modified=False

a:UID: ID07, TimeStamp: 03/09/2010 00:00:00, Description: Record 07 modified=False
b:UID: ID07, TimeStamp: 03/09/2010 00:00:00, Description: Record 07 modified=False

a:UID: ID08, TimeStamp: 03/09/2010 01:25:43, Description: Record 08 modified=False
b:UID: ID08, TimeStamp: 03/09/2010 01:25:43, Description: Record 08 modified=False

a:UID: ID09, TimeStamp: 03/09/2010 02:45:18, Description: Record 09 modified=False
b:UID: ID09, TimeStamp: 03/09/2010 02:45:18, Description: Record 09 modified=False

a:UID: ID11, TimeStamp: 03/09/2010 01:56:32, Description: Record 11 modified=False
b:UID: ID11, TimeStamp: 03/09/2010 01:56:32, Description: Record 11 modified=False

a:UID: ID12, TimeStamp: 03/09/2010 00:00:00, Description: Record 12 modified=False
b:UID: ID12, TimeStamp: 03/09/2010 00:00:00, Description: Record 12 modified=False

a:UID: ID13, TimeStamp: 03/09/2010 00:00:00, Description: Record 13 modified=False
b:UID: ID13, TimeStamp: 03/09/2010 00:00:00, Description: Record 13 modified=False

OnlyInA
-------
UID: ID06, TimeStamp: 03/09/2010 00:00:00, Description: Record 06 modified=False
UID: ID10, TimeStamp: 03/09/2010 01:59:27, Description: Record 10 modified=False

OnlyInB
-------
UID: ID04, TimeStamp: 03/09/2010 00:00:00, Description: Record 04 modified=False
UID: ID14, TimeStamp: 03/09/2010 00:00:00, Description: Record 14 modified=False

NewerInA
--------

NewerInB
--------

";
            Assert.AreEqual(
                expected.NormalizeResult(),
                actual.NormalizeResult(),
                "Expecting 2 items removed from A are now OnlyInB."
            );

            // Using Trim mode to reduce records and validate
            using (DHOST_APPLYCONTEXT.GetToken(ReconciliationMode.Trim))
            {
                reconciled++;
            }

            actual = reconciled.ToString();
            actual.ToClipboard();
            actual.ToClipboardAssert("Expecting removal of 4 records leaving 10 equal.");
            { }
            expected = @" 
Equal
-----
a:UID: ID01, TimeStamp: 03/09/2010 00:51:12, Description: Record 01 modified=False
b:UID: ID01, TimeStamp: 03/09/2010 00:51:12, Description: Record 01 modified=False

a:UID: ID02, TimeStamp: 03/09/2010 02:23:07, Description: Record 02 modified=False
b:UID: ID02, TimeStamp: 03/09/2010 02:23:07, Description: Record 02 modified=False

a:UID: ID03, TimeStamp: 03/09/2010 02:14:23, Description: Record 03 modified=False
b:UID: ID03, TimeStamp: 03/09/2010 02:14:23, Description: Record 03 modified=False

a:UID: ID05, TimeStamp: 03/09/2010 00:00:00, Description: Record 05 modified=False
b:UID: ID05, TimeStamp: 03/09/2010 00:00:00, Description: Record 05 modified=False

a:UID: ID07, TimeStamp: 03/09/2010 00:00:00, Description: Record 07 modified=False
b:UID: ID07, TimeStamp: 03/09/2010 00:00:00, Description: Record 07 modified=False

a:UID: ID08, TimeStamp: 03/09/2010 01:25:43, Description: Record 08 modified=False
b:UID: ID08, TimeStamp: 03/09/2010 01:25:43, Description: Record 08 modified=False

a:UID: ID09, TimeStamp: 03/09/2010 02:45:18, Description: Record 09 modified=False
b:UID: ID09, TimeStamp: 03/09/2010 02:45:18, Description: Record 09 modified=False

a:UID: ID11, TimeStamp: 03/09/2010 01:56:32, Description: Record 11 modified=False
b:UID: ID11, TimeStamp: 03/09/2010 01:56:32, Description: Record 11 modified=False

a:UID: ID12, TimeStamp: 03/09/2010 00:00:00, Description: Record 12 modified=False
b:UID: ID12, TimeStamp: 03/09/2010 00:00:00, Description: Record 12 modified=False

a:UID: ID13, TimeStamp: 03/09/2010 00:00:00, Description: Record 13 modified=False
b:UID: ID13, TimeStamp: 03/09/2010 00:00:00, Description: Record 13 modified=False

OnlyInA
-------

OnlyInB
-------

NewerInA
--------

NewerInB
--------

";
            Assert.AreEqual(
                expected.NormalizeResult(),
                actual.NormalizeResult(),
                "Expecting removal of 4 records leaving 10 equal."
            );
            Assert.AreEqual(10, reconciled.Equal.Count());
            { }

            IList target = listA;
            var builder = new List<string>();
            listA.Add(internalGetNewRecord());
            listA.Add(internalGetNewRecord());
            listA.Add(internalGetNewRecord());
            builder.Add($"{internalRemoveRandom(target)}");
            builder.Add($"{internalRemoveRandom(target)}");
            builder.Add($"{internalRemoveRandom(target)}");
            builder.Add($"{internalModifyRandom(target)}");
            builder.Add($"{internalModifyRandom(target)}");

            target = listB;
            listB.Add(internalGetNewRecord());
            builder.Add($"{internalRemoveRandom(target)}");
            builder.Add($"{internalModifyRandom(target)}");

            actual = string.Join(Environment.NewLine, builder);
            actual.ToClipboard();
            actual.ToClipboardAssert("Expecting consistent pseudorando index values.");
            { }
            // This explains the valid result where ID05 is modified in both.
            expected = @" 
ID08
ID13
ID12
(ID07, 3/9/2010 12:40:45 AM)
(ID05, 3/9/2010 1:27:42 AM)
ID02
(ID05, 3/9/2010 1:55:16 AM)";
            Assert.AreEqual(
                expected.NormalizeResult(),
                actual.NormalizeResult(),
                "Expecting consistent pseudorando index values."
            );

            reconciled = DefaultExec();

            actual = reconciled.ToString();
            actual.ToClipboard();
            actual.ToClipboardAssert("Expecting random changes resulting in ID05 collision.");
            { }
            // Expect 4 OnlyInA. Three new A and removed from B.
            // Expect 4 OnlyInB. Three removed A and one new B.
            // Expect 1 newer TUPLE PAIR in A.
            // Expect 1 newer TUPLE PAIR in B.

            expected = @" 
Equal
-----
a:UID: ID01, TimeStamp: 03/09/2010 00:51:12, Description: Record 01 modified=False
b:UID: ID01, TimeStamp: 03/09/2010 00:51:12, Description: Record 01 modified=False

a:UID: ID02, TimeStamp: 03/09/2010 02:23:07, Description: Record 02 modified=False
b:UID: ID02, TimeStamp: 03/09/2010 02:23:07, Description: Record 02 modified=False

a:UID: ID03, TimeStamp: 03/09/2010 02:14:23, Description: Record 03 modified=False
b:UID: ID03, TimeStamp: 03/09/2010 02:14:23, Description: Record 03 modified=False

a:UID: ID09, TimeStamp: 03/09/2010 02:45:18, Description: Record 09 modified=False
b:UID: ID09, TimeStamp: 03/09/2010 02:45:18, Description: Record 09 modified=False

a:UID: ID11, TimeStamp: 03/09/2010 01:56:32, Description: Record 11 modified=False
b:UID: ID11, TimeStamp: 03/09/2010 01:56:32, Description: Record 11 modified=False

OnlyInA
-------
UID: ID15, TimeStamp: 03/09/2010 00:00:00, Description: Record 15 modified=False
UID: ID16, TimeStamp: 03/09/2010 00:00:00, Description: Record 16 modified=False
UID: ID17, TimeStamp: 03/09/2010 00:00:00, Description: Record 17 modified=False

OnlyInB
-------
UID: ID08, TimeStamp: 03/09/2010 01:25:43, Description: Record 08 modified=False
UID: ID12, TimeStamp: 03/09/2010 00:00:00, Description: Record 12 modified=False
UID: ID13, TimeStamp: 03/09/2010 00:00:00, Description: Record 13 modified=False
UID: ID18, TimeStamp: 03/09/2010 00:00:00, Description: Record 18 modified=False

NewerInA
--------
a:UID: ID07, TimeStamp: 03/09/2010 00:40:45, Description: Record 07 modified=True
b:UID: ID07, TimeStamp: 03/09/2010 00:00:00, Description: Record 07 modified=False

NewerInB
--------
a:UID: ID05, TimeStamp: 03/09/2010 01:27:42, Description: Record 05 modified=True
b:UID: ID05, TimeStamp: 03/09/2010 01:55:16, Description: Record 05 modified=True

Collisions Reported - The following items are flagged as modified in both A and B.
----------------------------------------------------------------------------------
a:UID: ID05, TimeStamp: 03/09/2010 01:27:42, Description: Record 05 modified=True
b:UID: ID05, TimeStamp: 03/09/2010 01:55:16, Description: Record 05 modified=True

";
            Assert.AreEqual(
                expected.NormalizeResult(),
                actual.NormalizeResult(),
                "Expecting random changes."
            );
            { }

            var builderPrevA = new List<MockDatabaseRecord>();
            builderPrevA.AddRange(
                reconciled
                .Equal
                .OfType<Tuple<MockDatabaseRecord, MockDatabaseRecord>>()
                .Select(_ => _.Item1));
            builderPrevA.AddRange(
                reconciled
                .OnlyInA);
            builderPrevA.AddRange(
                reconciled
                .NewerInA);
            // Don't forget to count NewerInB because it's still in A!!
            builderPrevA.AddRange(
                reconciled
                .NewerInB);

            using (DHOST_APPLYCONTEXT.GetToken(ReconciliationMode.TakeA))
            {
                reconciled++;
            }

            actual = reconciled.ToString();
            actual.ToClipboard();
            actual.ToClipboardAssert("Expecting removal of 4 records leaving 10 equal.");
            { }
            expected = @" 
Equal
-----
a:UID: ID01, TimeStamp: 03/09/2010 00:51:12, Description: Record 01 modified=False
b:UID: ID01, TimeStamp: 03/09/2010 00:51:12, Description: Record 01 modified=False

a:UID: ID02, TimeStamp: 03/09/2010 02:23:07, Description: Record 02 modified=False
b:UID: ID02, TimeStamp: 03/09/2010 02:23:07, Description: Record 02 modified=False

a:UID: ID03, TimeStamp: 03/09/2010 02:14:23, Description: Record 03 modified=False
b:UID: ID03, TimeStamp: 03/09/2010 02:14:23, Description: Record 03 modified=False

a:UID: ID05, TimeStamp: 03/09/2010 01:27:42, Description: Record 05 modified=False
b:UID: ID05, TimeStamp: 03/09/2010 01:27:42, Description: Record 05 modified=False

a:UID: ID07, TimeStamp: 03/09/2010 00:40:45, Description: Record 07 modified=False
b:UID: ID07, TimeStamp: 03/09/2010 00:40:45, Description: Record 07 modified=False

a:UID: ID09, TimeStamp: 03/09/2010 02:45:18, Description: Record 09 modified=False
b:UID: ID09, TimeStamp: 03/09/2010 02:45:18, Description: Record 09 modified=False

a:UID: ID11, TimeStamp: 03/09/2010 01:56:32, Description: Record 11 modified=False
b:UID: ID11, TimeStamp: 03/09/2010 01:56:32, Description: Record 11 modified=False

a:UID: ID15, TimeStamp: 03/09/2010 00:00:00, Description: Record 15 modified=False
b:UID: ID15, TimeStamp: 03/09/2010 00:00:00, Description: Record 15 modified=False

a:UID: ID16, TimeStamp: 03/09/2010 00:00:00, Description: Record 16 modified=False
b:UID: ID16, TimeStamp: 03/09/2010 00:00:00, Description: Record 16 modified=False

a:UID: ID17, TimeStamp: 03/09/2010 00:00:00, Description: Record 17 modified=False
b:UID: ID17, TimeStamp: 03/09/2010 00:00:00, Description: Record 17 modified=False

OnlyInA
-------

OnlyInB
-------

NewerInA
--------

NewerInB
--------

";
            Assert.AreEqual(
                expected.NormalizeResult(),
                actual.NormalizeResult(),
                "Expecting removal of 4 records leaving 10 equal."
            );


            var builderCurrentA = new List<MockDatabaseRecord>();
            builderCurrentA.AddRange(
                reconciled
                .Equal
                .OfType<Tuple<MockDatabaseRecord, MockDatabaseRecord>>()
                .Select(_ => _.Item1));
            builderCurrentA.AddRange(
                reconciled
                .OnlyInA);
            builderCurrentA.AddRange(
                reconciled
                .NewerInA);
            // Don't forget to count NewerInB because it's still in A!!
            builderPrevA.AddRange(
                reconciled
                .NewerInB);

            Assert.AreEqual(
                builderPrevA.Count(),
                builderCurrentA.Count(),
                $"Expecting the before-and-after A count to jibe.");

            Assert.AreEqual(10, reconciled.Equal.Count());
            Assert.AreEqual(0, reconciled.OnlyInA.Count());
            Assert.AreEqual(0, reconciled.OnlyInB.Count());
            Assert.AreEqual(0, reconciled.NewerInA.Count());
            Assert.AreEqual(0, reconciled.NewerInB.Count());
            { }

            target = listA;
            builder = new List<string>();
            listA.Add(internalGetNewRecord());
            listA.Add(internalGetNewRecord());
            listA.Add(internalGetNewRecord());
            builder.Add($"{internalRemoveRandom(target)}");
            builder.Add($"{internalRemoveRandom(target)}");
            builder.Add($"{internalRemoveRandom(target)}");
            builder.Add($"{internalModifyRandom(target)}");
            builder.Add($"{internalModifyRandom(target)}");

            target = listB;
            listB.Add(internalGetNewRecord());
            builder.Add($"{internalRemoveRandom(target)}");
            builder.Add($"{internalModifyRandom(target)}");

            actual = string.Join(Environment.NewLine, builder);
            actual.ToClipboard();
            actual.ToClipboardAssert("Expecting consistent pseudorando index values.");
            { }

            expected = @" 
ID09
ID02
ID21
(ID01, 3/9/2010 3:33:28 AM)
(ID11, 3/9/2010 3:43:02 AM)
ID01
(ID02, 3/9/2010 2:58:55 AM)";
            Assert.AreEqual(
                expected.NormalizeResult(),
                actual.NormalizeResult(),
                "Expecting consistent pseudorando index values."
            );

            reconciled = DefaultExec();

            actual = reconciled.ToString();
            actual.ToClipboard();
            actual.ToClipboardAssert("Expecting random changes but no collisions.");
            { }
            expected = @" 
Equal
-----
a:UID: ID03, TimeStamp: 03/09/2010 02:14:23, Description: Record 03 modified=False
b:UID: ID03, TimeStamp: 03/09/2010 02:14:23, Description: Record 03 modified=False

a:UID: ID05, TimeStamp: 03/09/2010 01:27:42, Description: Record 05 modified=False
b:UID: ID05, TimeStamp: 03/09/2010 01:27:42, Description: Record 05 modified=False

a:UID: ID07, TimeStamp: 03/09/2010 00:40:45, Description: Record 07 modified=False
b:UID: ID07, TimeStamp: 03/09/2010 00:40:45, Description: Record 07 modified=False

a:UID: ID15, TimeStamp: 03/09/2010 00:00:00, Description: Record 15 modified=False
b:UID: ID15, TimeStamp: 03/09/2010 00:00:00, Description: Record 15 modified=False

a:UID: ID16, TimeStamp: 03/09/2010 00:00:00, Description: Record 16 modified=False
b:UID: ID16, TimeStamp: 03/09/2010 00:00:00, Description: Record 16 modified=False

a:UID: ID17, TimeStamp: 03/09/2010 00:00:00, Description: Record 17 modified=False
b:UID: ID17, TimeStamp: 03/09/2010 00:00:00, Description: Record 17 modified=False

OnlyInA
-------
UID: ID19, TimeStamp: 03/09/2010 00:00:00, Description: Record 19 modified=False
UID: ID20, TimeStamp: 03/09/2010 00:00:00, Description: Record 20 modified=False

OnlyInB
-------
UID: ID02, TimeStamp: 03/09/2010 02:58:55, Description: Record 02 modified=True
UID: ID09, TimeStamp: 03/09/2010 02:45:18, Description: Record 09 modified=False
UID: ID22, TimeStamp: 03/09/2010 00:00:00, Description: Record 22 modified=False

NewerInA
--------
a:UID: ID01, TimeStamp: 03/09/2010 03:33:28, Description: Record 01 modified=True
b:UID: ID01, TimeStamp: 03/09/2010 00:51:12, Description: Record 01 modified=False

a:UID: ID11, TimeStamp: 03/09/2010 03:43:02, Description: Record 11 modified=True
b:UID: ID11, TimeStamp: 03/09/2010 01:56:32, Description: Record 11 modified=False

NewerInB
--------

";
            Assert.AreEqual(
                expected.NormalizeResult(),
                actual.NormalizeResult(),
                "Expecting random changes."
            );


            var builderPrevB = new List<MockDatabaseRecord>();
            builderPrevB.AddRange(
                reconciled
                .Equal
                .OfType<Tuple<MockDatabaseRecord, MockDatabaseRecord>>()
                .Select(_ => _.Item1));
            builderPrevB.AddRange(
                reconciled
                .OnlyInB);
            builderPrevB.AddRange(
                reconciled
                .NewerInB);
            // Don't forget to count NewerInA because it's still in B!!
            builderPrevB.AddRange(
                reconciled
                .NewerInB);

            using (DHOST_APPLYCONTEXT.GetToken(ReconciliationMode.TakeA))
            {
                reconciled++;
            }

            actual = reconciled.ToString();
            actual.ToClipboard();
            actual.ToClipboardAssert("Expecting removal of 4 records leaving 10 equal.");
            { }
            expected = @" 
Equal
-----
a:UID: ID01, TimeStamp: 03/09/2010 03:33:28, Description: Record 01 modified=False
b:UID: ID01, TimeStamp: 03/09/2010 03:33:28, Description: Record 01 modified=False

a:UID: ID03, TimeStamp: 03/09/2010 02:14:23, Description: Record 03 modified=False
b:UID: ID03, TimeStamp: 03/09/2010 02:14:23, Description: Record 03 modified=False

a:UID: ID05, TimeStamp: 03/09/2010 01:27:42, Description: Record 05 modified=False
b:UID: ID05, TimeStamp: 03/09/2010 01:27:42, Description: Record 05 modified=False

a:UID: ID07, TimeStamp: 03/09/2010 00:40:45, Description: Record 07 modified=False
b:UID: ID07, TimeStamp: 03/09/2010 00:40:45, Description: Record 07 modified=False

a:UID: ID11, TimeStamp: 03/09/2010 03:43:02, Description: Record 11 modified=False
b:UID: ID11, TimeStamp: 03/09/2010 03:43:02, Description: Record 11 modified=False

a:UID: ID15, TimeStamp: 03/09/2010 00:00:00, Description: Record 15 modified=False
b:UID: ID15, TimeStamp: 03/09/2010 00:00:00, Description: Record 15 modified=False

a:UID: ID16, TimeStamp: 03/09/2010 00:00:00, Description: Record 16 modified=False
b:UID: ID16, TimeStamp: 03/09/2010 00:00:00, Description: Record 16 modified=False

a:UID: ID17, TimeStamp: 03/09/2010 00:00:00, Description: Record 17 modified=False
b:UID: ID17, TimeStamp: 03/09/2010 00:00:00, Description: Record 17 modified=False

a:UID: ID19, TimeStamp: 03/09/2010 00:00:00, Description: Record 19 modified=False
b:UID: ID19, TimeStamp: 03/09/2010 00:00:00, Description: Record 19 modified=False

a:UID: ID20, TimeStamp: 03/09/2010 00:00:00, Description: Record 20 modified=False
b:UID: ID20, TimeStamp: 03/09/2010 00:00:00, Description: Record 20 modified=False

OnlyInA
-------

OnlyInB
-------

NewerInA
--------

NewerInB
--------

";
            Assert.AreEqual(
                expected.NormalizeResult(),
                actual.NormalizeResult(),
                "Expecting removal of 4 records leaving 10 equal."
            );

            var builderCurrentB = new List<MockDatabaseRecord>();
            builderCurrentB.AddRange(
                reconciled
                .Equal
                .OfType<Tuple<MockDatabaseRecord, MockDatabaseRecord>>()
                .Select(_ => _.Item1));
            builderCurrentB.AddRange(
                reconciled
                .OnlyInB);
            builderCurrentB.AddRange(
                reconciled
                .NewerInB);
            // Don't forget to count NewerInA because it's still in B!!
            builderPrevB.AddRange(
                reconciled
                .NewerInB);

            Assert.AreEqual(
                builderPrevA.Count(),
                builderCurrentA.Count(),
                $"Expecting the before-and-after A count to jibe.");

            #region L o c a l M e t h o d s

            void localMakeWeightedRandomChanges()
            {
                var recordsToChange = new List<MockDatabaseRecord>();

                recordsToChange.AddRange(localApplyWeightedOdds(listA));
                recordsToChange.AddRange(localApplyWeightedOdds(listB));

                foreach (var apply in recordsToChange)
                {
                    apply.TimeStamp = EpochTime.AddSeconds(10000 * rando.NextDouble());
                    apply.IsModified = true;
                }
            }

            const int
                SAMPLE = 10,
                CHANGED_THRESHOLD = 2,
                BOTH = SAMPLE - 1;
            MockDatabaseRecord[] localApplyWeightedOdds(List<MockDatabaseRecord> target)
            {
                var builder = new List<MockDatabaseRecord>();
                int mode;
                foreach (var record in target)
                {
                    mode = rando.Next(SAMPLE);
                    Debug.WriteLineIf(false, $"{nameof(localMakeWeightedRandomChanges)}: MODE={mode} IsMax ={mode == BOTH}");
                    switch (mode)
                    {
                        case int _ when mode <= CHANGED_THRESHOLD:
                            builder.Add(record);
                            break;
                        case BOTH:
                            var uidToChange = listA[rando.Next(listA.Count)].Uid;
                            if (listA.FirstOrDefault(_ => _.Uid == uidToChange) is MockDatabaseRecord a &&
                                listB.FirstOrDefault(_ => _.Uid == uidToChange) is MockDatabaseRecord b)
                            {
                                builder.AddRange([a, b]);
                            }
                            break;
                        default:
                            break;
                    }
                }
                return builder.ToArray();
            }
            #endregion L o c a l M e t h o d s
        }

        [TestMethod]
        public void TestReconcileMethod_V2_WithToString_V2DiffModeMove()
        {
            Reconciled.DefaultExec = () => Reconcile(
                    listA,
                    listB,
                    diffMode: DiffHandleMode.Report); // Starting out with Report, not Move, IS INTENTIONAL and ok.

            Reconciled<MockDatabaseRecord> reconciled;

#if false && USE_PREDICATE_AS_ARGUMENT
            reconciled = Reconcile(
                listA,
                listB,
                uidSorter: (a, b) => (CompareUIDResult) a.Uid.CompareTo(b.Uid),
                versionComparer: (a, b) => (CompareVersionResult)a.TimeStamp.CompareTo(b.TimeStamp),
                isModified: (t)=>t.IsModified
            );
#endif
            reconciled = DefaultExec();

            actual = reconciled.ToString();
            actual.ToClipboard();
            actual.ToClipboardAssert("Expecting 12 OnlyInA values with default Epoch.");
            { }
            expected = @" 
Equal
-----

OnlyInA
-------
UID: ID01, TimeStamp: 03/09/2010 00:00:00, Description: Record 01 modified=False
UID: ID02, TimeStamp: 03/09/2010 00:00:00, Description: Record 02 modified=False
UID: ID03, TimeStamp: 03/09/2010 00:00:00, Description: Record 03 modified=False
UID: ID04, TimeStamp: 03/09/2010 00:00:00, Description: Record 04 modified=False
UID: ID05, TimeStamp: 03/09/2010 00:00:00, Description: Record 05 modified=False
UID: ID06, TimeStamp: 03/09/2010 00:00:00, Description: Record 06 modified=False
UID: ID07, TimeStamp: 03/09/2010 00:00:00, Description: Record 07 modified=False
UID: ID08, TimeStamp: 03/09/2010 00:00:00, Description: Record 08 modified=False
UID: ID09, TimeStamp: 03/09/2010 00:00:00, Description: Record 09 modified=False
UID: ID10, TimeStamp: 03/09/2010 00:00:00, Description: Record 10 modified=False
UID: ID11, TimeStamp: 03/09/2010 00:00:00, Description: Record 11 modified=False
UID: ID12, TimeStamp: 03/09/2010 00:00:00, Description: Record 12 modified=False

OnlyInB
-------

NewerInA
--------

NewerInB
--------
";

            Assert.AreEqual(
                expected.NormalizeResult(),
                actual.NormalizeResult(),
                "Expecting 12 OnlyInA values with default Epoch."
            );

            reconciled++;

            reconciled = DefaultExec();

            actual = reconciled.ToString();
            actual.ToClipboard();
            actual.ToClipboardAssert("Expecting records are Equal, based on the comparers (not on Equals)");
            { }
            expected = @" 
Equal
-----
a:UID: ID01, TimeStamp: 03/09/2010 00:00:00, Description: Record 01 modified=False
b:UID: ID01, TimeStamp: 03/09/2010 00:00:00, Description: Record 01 modified=False

a:UID: ID02, TimeStamp: 03/09/2010 00:00:00, Description: Record 02 modified=False
b:UID: ID02, TimeStamp: 03/09/2010 00:00:00, Description: Record 02 modified=False

a:UID: ID03, TimeStamp: 03/09/2010 00:00:00, Description: Record 03 modified=False
b:UID: ID03, TimeStamp: 03/09/2010 00:00:00, Description: Record 03 modified=False

a:UID: ID04, TimeStamp: 03/09/2010 00:00:00, Description: Record 04 modified=False
b:UID: ID04, TimeStamp: 03/09/2010 00:00:00, Description: Record 04 modified=False

a:UID: ID05, TimeStamp: 03/09/2010 00:00:00, Description: Record 05 modified=False
b:UID: ID05, TimeStamp: 03/09/2010 00:00:00, Description: Record 05 modified=False

a:UID: ID06, TimeStamp: 03/09/2010 00:00:00, Description: Record 06 modified=False
b:UID: ID06, TimeStamp: 03/09/2010 00:00:00, Description: Record 06 modified=False

a:UID: ID07, TimeStamp: 03/09/2010 00:00:00, Description: Record 07 modified=False
b:UID: ID07, TimeStamp: 03/09/2010 00:00:00, Description: Record 07 modified=False

a:UID: ID08, TimeStamp: 03/09/2010 00:00:00, Description: Record 08 modified=False
b:UID: ID08, TimeStamp: 03/09/2010 00:00:00, Description: Record 08 modified=False

a:UID: ID09, TimeStamp: 03/09/2010 00:00:00, Description: Record 09 modified=False
b:UID: ID09, TimeStamp: 03/09/2010 00:00:00, Description: Record 09 modified=False

a:UID: ID10, TimeStamp: 03/09/2010 00:00:00, Description: Record 10 modified=False
b:UID: ID10, TimeStamp: 03/09/2010 00:00:00, Description: Record 10 modified=False

a:UID: ID11, TimeStamp: 03/09/2010 00:00:00, Description: Record 11 modified=False
b:UID: ID11, TimeStamp: 03/09/2010 00:00:00, Description: Record 11 modified=False

a:UID: ID12, TimeStamp: 03/09/2010 00:00:00, Description: Record 12 modified=False
b:UID: ID12, TimeStamp: 03/09/2010 00:00:00, Description: Record 12 modified=False

OnlyInA
-------

OnlyInB
-------

NewerInA
--------

NewerInB
--------
";

            Assert.AreEqual(
                expected.NormalizeResult(),
                actual.NormalizeResult(),
                "Expecting records are Equal, based on the comparers (not on Equals)"
            );

            localMakeRandomChanges();

            // DEFAULT:
            // Originally this test had collisionHandling: CollisionHandlingMode.Report
            reconciled = DefaultExec();

            actual = reconciled.ToString();
            actual.ToClipboard();
            actual.ToClipboardAssert("Expecting to see changes in each.");
            // REPORT mode.
            // This output highlights Version2 differences.
            // - RESPECTS the V1 contract, e.g. reporting NewerInA even if conflict exists.
            // - Reports collisions in a new collection named ModifiedInBoth.
            // - When ModifiedInBoth results exist, those records are KEPT in
            //   determinate categories e.g. NewerInA.
            { }
            expected = @" 
Equal
-----
a:UID: ID04, TimeStamp: 03/09/2010 00:00:00, Description: Record 04 modified=False
b:UID: ID04, TimeStamp: 03/09/2010 00:00:00, Description: Record 04 modified=False

a:UID: ID05, TimeStamp: 03/09/2010 00:00:00, Description: Record 05 modified=False
b:UID: ID05, TimeStamp: 03/09/2010 00:00:00, Description: Record 05 modified=False

a:UID: ID06, TimeStamp: 03/09/2010 00:00:00, Description: Record 06 modified=False
b:UID: ID06, TimeStamp: 03/09/2010 00:00:00, Description: Record 06 modified=False

a:UID: ID07, TimeStamp: 03/09/2010 00:00:00, Description: Record 07 modified=False
b:UID: ID07, TimeStamp: 03/09/2010 00:00:00, Description: Record 07 modified=False

a:UID: ID12, TimeStamp: 03/09/2010 00:00:00, Description: Record 12 modified=False
b:UID: ID12, TimeStamp: 03/09/2010 00:00:00, Description: Record 12 modified=False

OnlyInA
-------

OnlyInB
-------

NewerInA
--------
a:UID: ID01, TimeStamp: 03/09/2010 00:51:12, Description: Record 01 modified=True
b:UID: ID01, TimeStamp: 03/09/2010 00:00:00, Description: Record 01 modified=False

a:UID: ID02, TimeStamp: 03/09/2010 02:23:07, Description: Record 02 modified=True
b:UID: ID02, TimeStamp: 03/09/2010 00:10:25, Description: Record 02 modified=True

a:UID: ID10, TimeStamp: 03/09/2010 01:59:27, Description: Record 10 modified=True
b:UID: ID10, TimeStamp: 03/09/2010 00:00:00, Description: Record 10 modified=False

a:UID: ID11, TimeStamp: 03/09/2010 01:56:32, Description: Record 11 modified=True
b:UID: ID11, TimeStamp: 03/09/2010 01:27:32, Description: Record 11 modified=True

NewerInB
--------
a:UID: ID03, TimeStamp: 03/09/2010 00:00:00, Description: Record 03 modified=False
b:UID: ID03, TimeStamp: 03/09/2010 02:14:23, Description: Record 03 modified=True

a:UID: ID08, TimeStamp: 03/09/2010 00:00:00, Description: Record 08 modified=False
b:UID: ID08, TimeStamp: 03/09/2010 01:25:43, Description: Record 08 modified=True

a:UID: ID09, TimeStamp: 03/09/2010 00:02:16, Description: Record 09 modified=True
b:UID: ID09, TimeStamp: 03/09/2010 02:45:18, Description: Record 09 modified=True

Collisions Reported - The following items are flagged as modified in both A and B.
----------------------------------------------------------------------------------
a:UID: ID02, TimeStamp: 03/09/2010 02:23:07, Description: Record 02 modified=True
b:UID: ID02, TimeStamp: 03/09/2010 00:10:25, Description: Record 02 modified=True

a:UID: ID11, TimeStamp: 03/09/2010 01:56:32, Description: Record 11 modified=True
b:UID: ID11, TimeStamp: 03/09/2010 01:27:32, Description: Record 11 modified=True

a:UID: ID09, TimeStamp: 03/09/2010 00:02:16, Description: Record 09 modified=True
b:UID: ID09, TimeStamp: 03/09/2010 02:45:18, Description: Record 09 modified=True

";

            Assert.AreEqual(
                expected.NormalizeResult(),
                actual.NormalizeResult(),
                "Expecting to see changes in each."
            );
            Assert.AreEqual(5, reconciled.Equal.Length);
            Assert.AreEqual(0, reconciled.OnlyInA.Length);
            Assert.AreEqual(0, reconciled.OnlyInB.Length);
            Assert.AreEqual(4, reconciled.NewerInA.Length);
            Assert.AreEqual(3, reconciled.NewerInB.Length);
            Assert.AreEqual(3, reconciled.Diffs.Length);


            // 241114 ADDED:
            // Ways of verifying the switch to MOVE.
            // First of all, don't get confused and start believing tha
            Assert.AreEqual(ReconciliationMode.Append, Reconciled.DHostApplyContext.DefaultMode);



            // Keeping the SAME random changes, reconcile
            // again, this time exercising the Move option.
            Reconciled.DefaultExec = () => Reconcile(
                    listA,
                    listB,
                    diffMode: DiffHandleMode.Move); 
            reconciled = DefaultExec();

            actual = reconciled.ToString();
            actual.ToClipboard();
            actual.ToClipboardAssert("Expecting to see changes in each.");

            // MOVE mode.
            // This output highlights Version2 differences.
            // - BREAKS the V1 contract, e.g. NO reporting NewerInA even if conflict exists.
            // - Reports collisions in a new collection named ModifiedInBoth.
            // - When ModifiedInBoth results exist, those records are REMOVED from
            //   determinate categories e.g. NewerInA.
            { }
            expected = @" 
Equal
-----
a:UID: ID04, TimeStamp: 03/09/2010 00:00:00, Description: Record 04 modified=False
b:UID: ID04, TimeStamp: 03/09/2010 00:00:00, Description: Record 04 modified=False

a:UID: ID05, TimeStamp: 03/09/2010 00:00:00, Description: Record 05 modified=False
b:UID: ID05, TimeStamp: 03/09/2010 00:00:00, Description: Record 05 modified=False

a:UID: ID06, TimeStamp: 03/09/2010 00:00:00, Description: Record 06 modified=False
b:UID: ID06, TimeStamp: 03/09/2010 00:00:00, Description: Record 06 modified=False

a:UID: ID07, TimeStamp: 03/09/2010 00:00:00, Description: Record 07 modified=False
b:UID: ID07, TimeStamp: 03/09/2010 00:00:00, Description: Record 07 modified=False

a:UID: ID12, TimeStamp: 03/09/2010 00:00:00, Description: Record 12 modified=False
b:UID: ID12, TimeStamp: 03/09/2010 00:00:00, Description: Record 12 modified=False

OnlyInA
-------

OnlyInB
-------

NewerInA
--------
a:UID: ID01, TimeStamp: 03/09/2010 00:51:12, Description: Record 01 modified=True
b:UID: ID01, TimeStamp: 03/09/2010 00:00:00, Description: Record 01 modified=False

a:UID: ID10, TimeStamp: 03/09/2010 01:59:27, Description: Record 10 modified=True
b:UID: ID10, TimeStamp: 03/09/2010 00:00:00, Description: Record 10 modified=False

NewerInB
--------
a:UID: ID03, TimeStamp: 03/09/2010 00:00:00, Description: Record 03 modified=False
b:UID: ID03, TimeStamp: 03/09/2010 02:14:23, Description: Record 03 modified=True

a:UID: ID08, TimeStamp: 03/09/2010 00:00:00, Description: Record 08 modified=False
b:UID: ID08, TimeStamp: 03/09/2010 01:25:43, Description: Record 08 modified=True

Collisions Reported - The following items are flagged as modified in both A and B.
----------------------------------------------------------------------------------
a:UID: ID02, TimeStamp: 03/09/2010 02:23:07, Description: Record 02 modified=True
b:UID: ID02, TimeStamp: 03/09/2010 00:10:25, Description: Record 02 modified=True

a:UID: ID11, TimeStamp: 03/09/2010 01:56:32, Description: Record 11 modified=True
b:UID: ID11, TimeStamp: 03/09/2010 01:27:32, Description: Record 11 modified=True

a:UID: ID09, TimeStamp: 03/09/2010 00:02:16, Description: Record 09 modified=True
b:UID: ID09, TimeStamp: 03/09/2010 02:45:18, Description: Record 09 modified=True

";

            Assert.AreEqual(
                expected.NormalizeResult(),
                actual.NormalizeResult(),
                "Expecting to see changes in each."
            );
            Assert.AreEqual(5, reconciled.Equal.Length);
            Assert.AreEqual(0, reconciled.OnlyInA.Length);
            Assert.AreEqual(0, reconciled.OnlyInB.Length);
            Assert.AreEqual(
                2,
                reconciled.NewerInA.Length,
                "Expecting this to see the 'override' count not the 'base' count");
            Assert.AreEqual(
                2,
                reconciled.NewerInB.Length,
                "Expecting this to see the 'override' count not the 'base' count");
            //Assert.AreEqual(
            //    3,
            //    reconciled.Diffs.Length,
            //    "Expecting this to see the 'override' count not the 'base' count");
            //{ }



            #region L o c a l M e t h o d s
            void localMakeRandomChanges()
            {
                var recordsToChange = new List<MockDatabaseRecord>();

                recordsToChange.AddRange(localApplyWeightedOdds(listA));
                recordsToChange.AddRange(localApplyWeightedOdds(listB));

                foreach (var apply in recordsToChange)
                {
                    apply.TimeStamp = EpochTime.AddSeconds(10000 * rando.NextDouble());
                    apply.IsModified = true;
                }
            }

            const int
                SAMPLE = 10,
                CHANGED_THRESHOLD = 2,
                BOTH = SAMPLE - 1;
            MockDatabaseRecord[] localApplyWeightedOdds(List<MockDatabaseRecord> target)
            {
                var builder = new List<MockDatabaseRecord>();
                int mode;
                foreach (var record in target)
                {
                    mode = rando.Next(SAMPLE);
                    Debug.WriteLineIf(false, $"{nameof(localMakeRandomChanges)}: MODE={mode} IsMax ={mode == BOTH}");
                    switch (mode)
                    {
                        case int _ when mode <= CHANGED_THRESHOLD:
                            builder.Add(record);
                            break;
                        case BOTH:
                            var uidToChange = listA[rando.Next(listA.Count)].Uid;
                            if (listA.FirstOrDefault(_ => _.Uid == uidToChange) is MockDatabaseRecord a &&
                                listB.FirstOrDefault(_ => _.Uid == uidToChange) is MockDatabaseRecord b)
                            {
                                builder.AddRange([a, b]);
                            }
                            break;
                        default:
                            break;
                    }
                }
                return builder.ToArray();
            }
            #endregion L o c a l M e t h o d s
        }
    }
}
