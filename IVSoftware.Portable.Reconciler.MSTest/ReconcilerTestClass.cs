using IVSoftware.Portable.Disposable;
using IVSoftware.WinOS.MSTest.Extensions;
using Newtonsoft.Json;
using SQLite;
using System.Diagnostics;
using static IVSoftware.Portable.Reconciler;
using static IVSoftware.WinOS.MSTest.Extensions.Static;

namespace IVSoftware.Portable.Static.Reconciler.MSTest
{
    [TestClass]
    public class ReconcilerTestClass
    {

        #region S T A B L E
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
                }
            };
        }

        [ClassInitialize]
        public static void Initialize(TestContext context)
        {
        }
        private static readonly DateTime EpochTime = new DateTime(2010, 3, 9, 0, 0, 0);
        internal Random rando { get; set; } = new Random(1);
        static int autoIncrement { get; set; } = 1;

        string? actual, expected, expr;

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
            Reconciled<MockDatabaseRecord>.DHostApplyContext.SetTargets(listA, listB);
        }

        [TestCleanup]
        public void Cleanup()
        {
        }

        [TestMethod]
        public void TestDisposableExecutionHost()
        {
            var uut = Reconciled<MockDatabaseRecord>.DHostApplyContext;
            Assert.IsNotNull(uut.A, $"Expecting this to be set in {nameof(OnInitTest)}()");
            Assert.IsNotNull(uut.B, $"Expecting this to be set in {nameof(OnInitTest)}()");
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
        }

        [TestMethod]
        public void TestReconcileMethod_V1()
        {
            Reconciled<MockDatabaseRecord>.DefaultExec = () => Reconcile(
                    listA,
                    listB,
                    uidSorter: (a, b) => (CompareUIDResult)a.Uid.CompareTo(b.Uid),
                    versionComparer: (a, b) => (CompareVersionResult)a.TimeStamp.CompareTo(b.TimeStamp));

            Reconciled<MockDatabaseRecord> reconciled;

            reconciled = DefaultExecMDR();

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

            localMakeRandomChanges();
            reconciled = DefaultExecMDR();
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

            { }
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

            reconciled = Reconcile(
                listA,
                listB,
                uidSorter: (a, b) => (CompareUIDResult)a.Uid.CompareTo(b.Uid),
                versionComparer: (a, b) => (CompareVersionResult)a.TimeStamp.CompareTo(b.TimeStamp)
            );

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

            reconciled = Reconcile(
                listA,
                listB,
                uidSorter: (a, b) => (CompareUIDResult)a.Uid.CompareTo(b.Uid),
                versionComparer: (a, b) => (CompareVersionResult)a.TimeStamp.CompareTo(b.TimeStamp)
            );

            actual = reconciled.ToString();
            actual.ToClipboard();
            actual.ToClipboardAssert("Expecting new item in OnlyInB.");
            { }
            reconciled++;

            actual = reconciled.ToString();
            actual.ToClipboard();
            actual.ToClipboardAssert("Expecting new record 14 is unmodified equal");
            { }

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

        private Reconciled<MockDatabaseRecord> DefaultExecMDR()
        {
            if (Reconciled<MockDatabaseRecord>.DefaultExec() is Reconciled<MockDatabaseRecord> reconciled)
            {
                return reconciled;
            }
            else throw new InvalidCastException();
        }
        #endregion S T A B L E
    }
}
