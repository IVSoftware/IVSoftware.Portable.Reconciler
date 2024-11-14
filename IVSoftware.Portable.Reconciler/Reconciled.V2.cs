using IVSoftware.Portable.Disposable;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Data;
using System.Diagnostics;
using System.Linq;
using System.Runtime.CompilerServices;

namespace IVSoftware.Portable
{
    /// <summary>
    /// Provides functionality for reconciling data records by comparing and updating lists of records.
    /// </summary>
    partial class Reconciler
    {
        #region S T A B L E

        public class Reconciled
        {
            internal static DisposableApplyContext DHostExecContext { get; } = new DisposableApplyContext();

            /// <summary>
            /// Gets a context object for managing reconciliation operations between hosts.
            /// Each instantiation of <see cref="Reconciled{T}"/> creates a unique <see cref="DisposableApplyContext"/>.
            /// </summary>
            public static DisposableApplyContext DHostApplyContext { get; } = new DisposableApplyContext();

            // These are set from static Reconciled method.
            internal IEnumerable _srceA
            {
                get => __srceA;
                set
                {
                    if (!Equals(__srceA, value))
                    {
                        __srceA = value;
                    }
                }
            }
            static IEnumerable __srceA = default;
            internal IEnumerable _srceB
            {
                get => __srceB;
                set
                {
                    if (!Equals(__srceB, value))
                    {
                        __srceB = value;
                    }
                }
            }
            static IEnumerable __srceB = default;

            /// <summary>
            /// Gets or sets the default execution function for applying reconciliation with loopback.
            /// </summary>
#if DEBUG
            public static Func<IReconciled> DefaultExec
            {
                get
                {
                    if (localIsIReconcilable(__srceA) && localIsIReconcilable(__srceB))
                    {
                        // Can 'wing it'!
                    }
                    else
                    {

                    }

                    #region L o c a l M e t h o d s
                    bool localIsIReconcilable(IEnumerable unk)
                    {
                        if (unk == null) return false;

                        var ienumerableType =
                            unk
                            .GetType()
                            .GetInterfaces()
                            .FirstOrDefault(_ => _.IsGenericType && _.GetGenericTypeDefinition() == typeof(IEnumerable<>));

                        if (ienumerableType == null) return false;

                        Type itemType = ienumerableType.GetGenericArguments()[0];
                        return typeof(IReconcilable).IsAssignableFrom(itemType);
                    }
                    #endregion L o c a l M e t h o d s
                    return _defaultExec;
                }
                set
                {
                    if (!Equals(_defaultExec, value))
                    {
                        _defaultExec = value;
                    }
                }
            }
            static Func<IReconciled> _defaultExec = default;

#else
            public static Func<IReconciled> DefaultExec { get; set; }
#endif

            /// <summary>
            /// The 'default of the default' is V1 Compatiple.
            /// - ToString() uses V1 format which does not report collisions.
            /// - Tuple collections like Equal, NewerInA and NewerAndB continue
            ///   to tolerate pairs where both versions have been modified.
            /// </summary>
            public Enum DefaultDiffMode { get; set; } = DiffHandleMode.Disabled;
        }

        /// <summary>
        /// A generic class that represents a reconciled state for a specific type of record.
        /// Contains methods and properties for performing reconciliation operations.
        /// </summary>
        /// <typeparam name="T">The type of records being reconciled.</typeparam>
        partial class Reconciled<T> : Reconciled, IReconciled
        {
            // This is set from signature interceptors.
            public Enum DiffMode
            {
                get => _diffMode;
                set
                {
                    if (!Equals(_diffMode, value))
                    {
                        _diffMode = value;
                        // If it's a DiffReportMode, cast it.
                        switch ((DiffHandleMode)_diffMode)
                        {
                            case DiffHandleMode.Report:
                            case DiffHandleMode.Move:
                                var cMe = this.ToString();
                                { }
                                var diffs = new List<DiffDescriptor>();
                                var equals = Equal.ToList();
                                var newerInA = NewerInA.ToList();
                                var newerInB = NewerInB.ToList();
                                bool
                                    equalChanged = false,
                                    newerInAChanged = false,
                                    newerInBChanged = false;
                                foreach (var tuple in Equal)
                                {
                                    if (localIsModifiedInBoth(tuple.Item1, tuple.Item2, out IReconcilableDiffs tA, out IReconcilableDiffs tB))
                                    {
                                        diffs.Add(new DiffDescriptor(tA, tB));
                                        if (Equals(_diffMode, DiffHandleMode.Move))
                                        {
                                            equals.Remove(tuple);
                                            equalChanged = true;
                                        }
                                    }
                                }
                                foreach (var t in NewerInA)
                                {
                                    if (
                                        t is IReconcilableDiffs tA &&
                                        tA.IsModified &&
                                        Not[t] is IReconcilableDiffs tB &&
                                        tB.IsModified)
                                    {
                                        diffs.Add(new DiffDescriptor(tA, tB));
                                        if (Equals(_diffMode, DiffHandleMode.Move))
                                        {
                                            newerInA.Remove(t);
                                            newerInAChanged = true;
                                        }
                                    }
                                }
                                foreach (var t in NewerInB)
                                {
                                    if (
                                        Not[t] is IReconcilableDiffs tA &&
                                        tA.IsModified &&
                                        t is IReconcilableDiffs tB &&
                                        tB.IsModified)
                                    {
                                        diffs.Add(new DiffDescriptor(tA, tB));
                                        if (Equals(_diffMode, DiffHandleMode.Move))
                                        {
                                            newerInB.Remove(t);
                                            newerInBChanged = true;
                                        }
                                    }
                                }
                                // Final update.
                                if (Equals(_diffMode, DiffHandleMode.Move))
                                {
                                    if (equalChanged)
                                    {
                                        Equal = equals.ToArray();
                                    }
                                    if (newerInAChanged)
                                    {
                                        NewerInA = newerInA.ToArray();
                                    }
                                    if (newerInBChanged)
                                    {
                                        NewerInB = newerInB.ToArray();
                                    }
                                }
                                // This is unconditional and intentionally outside of any switch.
                                if (diffs.Any())
                                {
                                    Diffs = diffs.ToArray();
                                }
                                break;
                            default:
                                break;
                        }
                        bool localIsModifiedInBoth(T unkA, T unkB, out IReconcilableDiffs a, out IReconcilableDiffs b)
                        {
                            a = unkA as IReconcilableDiffs;
                            b = unkA as IReconcilableDiffs;
                            return (a?.IsModified ?? false) && (b?.IsModified ?? false);
                        }
                    }
                }
            }
            Enum _diffMode = default;


            /// <summary>
            /// Enumeration where IsModifiedProperty is true in both A and B.
            /// </summary>
            /// <remarks>
            /// By implementing IReconcilableDiffs explicitly, the predicate boolean
            /// property can be, but doesn't have to be, a simple bool. For example, it
            /// could be a selector that depends on some other state that routes it.
            /// </remarks>
            public DiffDescriptor[] Diffs { get; private set; }

            #region I N T E R F A C E
            IEnumerable IReconciled.Collisions => Diffs;
            IEnumerable IReconciled.Equal => Equal;
            IEnumerable IReconciled.OnlyInA => OnlyInA;
            IEnumerable IReconciled.OnlyInB => OnlyInB;
            IEnumerable IReconciled.NewerInA => NewerInA;
            IEnumerable IReconciled.NewerInB => NewerInB;
            IEnumerable IReconciled.Not => Not;
            #endregion I N T E R F A C E

            /// <summary>
            /// Applies the reconciliation changes between two data sets using the current apply context settings.
            /// </summary>
            public void Apply()
            {
                var srceA = DHostApplyContext.SrceA ?? _srceA;
                var srceB = DHostApplyContext.SrceB ?? _srceB;
                if (srceA is null || srceB is null)
                {
                    Debug.WriteLine($"ADVISORY: Should not be null any time after 'the first' call to Reconcile)");
                }
                else
                {
                    ApplyReconciled(srceA, srceB, DHostApplyContext.Mode);
                }
            }

            /// <summary>
            /// Applies reconciliation changes with an optional custom execution loopback.
            /// </summary>
            /// <param name="customExec">A function to execute custom reconciliation behavior. If null, <see cref="DefaultExec"/> is used.</param>
            /// <returns>An <see cref="IReconciled"/> object representing the applied state.</returns>
            public IReconciled ApplyWithLoopback(Func<IReconciled> customExec = null)
            {
                var srceA = DHostApplyContext.SrceA ?? _srceA;
                var srceB = DHostApplyContext.SrceB ?? _srceB;
                if (srceA is null || srceB is null)
                {
                    Debug.WriteLine($"ADVISORY: Should not be null any time after 'the first' call to Reconcile)");
                }
                else
                {
                    ApplyReconciled(srceA, srceB, DHostApplyContext.Mode);
                }
                return customExec?.Invoke() ?? DefaultExec?.Invoke();
            }

            /// <summary>
            /// Applies reconciliation changes based on the specified mode, updating lists <paramref name="a"/> and <paramref name="b"/>.
            /// </summary>
            /// <param name="a">The first list of records to be reconciled.</param>
            /// <param name="b">The second list of records to be reconciled.</param>
            /// <param name="primaryMode">The mode in which reconciliation is performed (e.g., Append, Trim, etc.).</param>
            public void ApplyReconciled(IEnumerable unkA, IEnumerable unkB, ReconciliationMode primaryMode)
            {
                if (unkA is IList a && unkB is IList b)
                {
                    switch (primaryMode)
                    {
                        case ReconciliationMode.Append:
                            localAppend();
                            break;
                        case ReconciliationMode.Trim:
                            localTrim();
                            break;
                        case ReconciliationMode.TakeA:
                            localTakeA();
                            break;
                        case ReconciliationMode.TakeB:
                            localTakeB();
                            break;
                        default:
                            Debug.Fail(nameof(NotImplementedException));
                            break;
                    }
                    localUpdate();
                }
                else throw new InvalidOperationException(
                    $"Srce is {unkA.GetType().FullName}, but in order to apply reconciiation, it must be assignable to {nameof(IList)}");

                #region L o c a l M e t h o d s
                void localAppend()
                {
                    foreach (ICloneable record in OnlyInA)
                    {
                        b.Add(record.Clone());
                    }
                    foreach (ICloneable record in OnlyInB)
                    {
                        a.Add(record.Clone());
                    }
                }
                void localTrim()
                {
                    foreach (ICloneable record in OnlyInA)
                    {
                        a.Remove(record);
                    }
                    foreach (ICloneable record in OnlyInB)
                    {
                        b.Remove(record);
                    }
                }
                void localTakeA()
                {
                    foreach (ICloneable record in OnlyInA)
                    {
                        b.Add(record.Clone());
                    }
                    foreach (ICloneable record in OnlyInB)
                    {
                        b.Remove(record);
                    }
                }
                void localTakeB()
                {
                    foreach (ICloneable record in OnlyInA)
                    {
                        a.Remove(record);
                    }
                    foreach (ICloneable record in OnlyInB)
                    {
                        a.Add(record.Clone());
                    }
                }
                void localUpdate()
                {
                    switch (primaryMode)
                    {
                        case ReconciliationMode.Append:
                        case ReconciliationMode.Trim:
                            foreach (T record in NewerInA)
                            {
                                if (Not is Dictionary<T, T> not)
                                {
                                    if (not[record] is IReconcilable<T> updatable)
                                    {
                                        updatable.UpdateFrom(record);
                                    }
                                }
                            }
                            foreach (T record in NewerInB)
                            {
                                if (Not is Dictionary<T, T> not)
                                {
                                    if (not[record] is IReconcilable<T> updatable)
                                    {
                                        updatable.UpdateFrom(record);
                                    }
                                }
                            }
                            break;
                        case ReconciliationMode.TakeA:
                            foreach (T record in NewerInA)
                            {
                                if (Not is Dictionary<T, T> not)
                                {
                                    if (not[record] is IReconcilable<T> updatable)
                                    {
                                        updatable.UpdateFrom(record);
                                    }
                                }
                            }
                            foreach (T record in NewerInB)
                            {
                                if (Not is Dictionary<T, T> not)
                                {
                                    if (record is IReconcilable<T> overwrite && not[record] is T takeA)
                                    {
                                        overwrite.UpdateFrom(takeA);
                                    }
                                }
                            }
                            break;
                        case ReconciliationMode.TakeB:
                            foreach (T record in NewerInA)
                            {
                                if (Not is Dictionary<T, T> not)
                                {
                                    if (record is IReconcilable<T> overwrite && not[record] is T takeB)
                                    {
                                        overwrite.UpdateFrom(takeB);
                                    }
                                }
                            }
                            foreach (T record in NewerInB)
                            {
                                if (Not is Dictionary<T, T> not)
                                {
                                    if (not[record] is IReconcilable<T> updatable)
                                    {
                                        updatable.UpdateFrom(record);
                                    }
                                }
                            }
                            break;
                        default:
                            throw new NotImplementedException();
                    }
                }
                #endregion L o c a l  M e t h o d s
            }


            /// <summary>
            /// Operator to apply reconciliation changes and retrieve the updated <see cref="Reconciled{T}"/>.
            /// </summary>
            /// <param name="original">The original <see cref="Reconciled{T}"/> instance.</param>
            /// <returns>A new <see cref="Reconciled{T}"/> instance with changes applied.</returns>
            /// <exception cref="InvalidCastException">Thrown if <see cref="DefaultExec"/> does not return a valid <see cref="Reconciled{T}"/> instance.</exception>
            public static Reconciled<T> operator ++(Reconciled<T> original)
            {
                original.Apply();
                if (DefaultExec() is Reconciled<T> reconciled)
                {
                    return reconciled;
                }
                else throw new InvalidCastException();
            }

            private string ToStringV2()
            {
                var builder = new List<string>();

                var term = nameof(Equal);
                builder.Add(term);
                builder.Add(string.Join(string.Empty, Enumerable.Repeat('-', term.Length)));
                if (Equal.Any()) builder.Add(string.Join(
                    $"{Environment.NewLine}{Environment.NewLine}",
                    Equal.Select(e => $"a:{e.Item1}{Environment.NewLine}b:{e.Item2}")));
                builder.Add(string.Empty);

                term = nameof(OnlyInA);
                builder.Add(term);
                builder.Add(string.Join(string.Empty, Enumerable.Repeat('-', term.Length)));
                if (OnlyInA.Any()) builder.Add(string.Join(Environment.NewLine, OnlyInA.Select(a => $"{a}")));
                builder.Add(string.Empty);

                term = nameof(OnlyInB);
                builder.Add(term);
                builder.Add(string.Join(string.Empty, Enumerable.Repeat('-', term.Length)));
                if (OnlyInB.Any()) builder.Add(string.Join(Environment.NewLine, OnlyInB.Select(b => $"{b}")));
                builder.Add(string.Empty);

                term = nameof(NewerInA);
                builder.Add(term);
                builder.Add(string.Join(string.Empty, Enumerable.Repeat('-', term.Length)));
                if (NewerInA.Any()) builder.Add(string.Join(
                    $"{Environment.NewLine}{Environment.NewLine}",
                    NewerInA.Select(a => $"a:{a}{Environment.NewLine}b:{Not[a]}")));
                builder.Add(string.Empty);

                term = nameof(NewerInB);
                builder.Add(term);
                builder.Add(string.Join(string.Empty, Enumerable.Repeat('-', term.Length)));
                if (NewerInB.Any()) builder.Add(string.Join(
                    $"{Environment.NewLine}{Environment.NewLine}",
                    NewerInB.Select(b => $"a:{Not[b]}{Environment.NewLine}b:{b}")));
                builder.Add(string.Empty);

                if (Diffs?.Any() == true)
                {
                    term = $"{nameof(IReconciled.Collisions)} Reported - The following items are flagged as modified in both A and B.";
                    builder.Add(term);
                    builder.Add(string.Join(string.Empty, Enumerable.Repeat('-', term.Length)));
                    builder.Add(string.Join(
                        $"{Environment.NewLine}{Environment.NewLine}",
                        Diffs.Select(_ => $"a:{_.A}{Environment.NewLine}b:{_.B}")));
                    builder.Add(string.Empty);
                }
                builder.Add(string.Empty);
                return string.Join(Environment.NewLine, builder);
            }
        }
        #endregion S T A B L E
    }
}
