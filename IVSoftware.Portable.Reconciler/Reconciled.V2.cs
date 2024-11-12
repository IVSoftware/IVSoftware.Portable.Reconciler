using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;

namespace IVSoftware.Portable
{
    #region S T A B L E
    /// <summary>
    /// Provides functionality for reconciling data records by comparing and updating lists of records.
    /// </summary>
    partial class Reconciler
    {
        /// <summary>
        /// A generic class that represents a reconciled state for a specific type of record.
        /// Contains methods and properties for performing reconciliation operations.
        /// </summary>
        /// <typeparam name="T">The type of records being reconciled.</typeparam>
        partial class Reconciled<T> : IReconciled
        {
            /// <summary>
            /// Gets an enumerable collection of differences between two data sets.
            /// </summary>
            public IEnumerable Diffs => throw new NotImplementedException();

            IEnumerable IReconciled.Equal => Equal;
            IEnumerable IReconciled.OnlyInA => OnlyInA;
            IEnumerable IReconciled.OnlyInB => OnlyInB;
            IEnumerable IReconciled.NewerInA => NewerInA;
            IEnumerable IReconciled.NewerInB => NewerInB;
            IEnumerable IReconciled.Not => Not;

            /// <summary>
            /// Gets a context object for managing reconciliation operations between hosts.
            /// Each instantiation of <see cref="Reconciled{T}"/> creates a unique <see cref="DisposableApplyContext"/>.
            /// </summary>
            public static DisposableApplyContext DHostApplyContext { get; } = new DisposableApplyContext();

            /// <summary>
            /// Applies the reconciliation changes between two data sets using the current apply context settings.
            /// </summary>
            public void Apply()
            {
                ApplyReconciled(DHostApplyContext.A, DHostApplyContext.B, DHostApplyContext.Mode);
            }

            /// <summary>
            /// Applies reconciliation changes with an optional custom execution loopback.
            /// </summary>
            /// <param name="customExec">A function to execute custom reconciliation behavior. If null, <see cref="DefaultExec"/> is used.</param>
            /// <returns>An <see cref="IReconciled"/> object representing the applied state.</returns>
            public IReconciled ApplyWithLoopback(Func<IReconciled> customExec = null)
            {
                ApplyReconciled(DHostApplyContext.A, DHostApplyContext.B, DHostApplyContext.Mode);
                return customExec?.Invoke() ?? DefaultExec?.Invoke();
            }

            /// <summary>
            /// Applies reconciliation changes based on the specified mode, updating lists <paramref name="a"/> and <paramref name="b"/>.
            /// </summary>
            /// <param name="a">The first list of records to be reconciled.</param>
            /// <param name="b">The second list of records to be reconciled.</param>
            /// <param name="primaryMode">The mode in which reconciliation is performed (e.g., Append, Trim, etc.).</param>
            public void ApplyReconciled(IList a, IList b, ReconciliationMode primaryMode)
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
                        Debug.Fail(nameof(NotImplementedException));
                        break;
                    case ReconciliationMode.TakeB:
                        Debug.Fail(nameof(NotImplementedException));
                        break;
                    default:
                        Debug.Fail(nameof(NotImplementedException));
                        break;
                }
                localUpdate();

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
                void localUpdate()
                {
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
                }
                #endregion L o c a l  M e t h o d s
            }

            /// <summary>
            /// Gets or sets the default execution function for applying reconciliation with loopback.
            /// </summary>
            public static Func<IReconciled> DefaultExec { get; set; }

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
        }
    }
    #endregion S T A B L E
}
