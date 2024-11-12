using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;
using static IVSoftware.Portable.Reconciler;

namespace IVSoftware.Portable
{
    partial class Reconciler
    {
        partial class Reconciled<T> : IReconciled
        {
            public IEnumerable Diffs => throw new NotImplementedException();

            IEnumerable IReconciled.Equal => Equal;

            IEnumerable IReconciled.OnlyInA => OnlyInA;

            IEnumerable IReconciled.OnlyInB => OnlyInB;

            IEnumerable IReconciled.NewerInA => NewerInA;

            IEnumerable IReconciled.NewerInB => NewerInB;

            IEnumerable IReconciled.Not => Not;

            public static DisposableExecutionHost DHostApplyContext { get; } = new DisposableExecutionHost();
            public void Apply()
            {
                if (DHostApplyContext?.IsZero() == false)
                {
                    ApplyReconciled(DHostApplyContext.A, DHostApplyContext.B, DHostApplyContext.Mode);
                }
                else throw new InvalidOperationException($"{nameof(DHostApplyContext)} required.");
            }
            public IReconciled ApplyWithLoopback(Func<IReconciled> customExec = null)
            {
                if (DHostApplyContext?.IsZero() == false)
                {
                    ApplyReconciled(DHostApplyContext.A, DHostApplyContext.B, DHostApplyContext.Mode);
                    if (customExec is null)
                    {
                        return DefaultExec?.Invoke();
                    }
                    else
                    {
                        return customExec();
                    }
                }
                else throw new InvalidOperationException($"{nameof(DHostApplyContext)} required.");
            }
            public void ApplyReconciled(IList a, IList b, ReconciliationMode primaryMode)
            {
                switch (primaryMode)
                {
                    case ReconciliationMode.Append:
                        localAppend();
                        break;
                    case ReconciliationMode.Trim:
                        Debug.Fail(nameof(NotImplementedException));
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
                foreach (T record in NewerInA)
                {
                    if (Not is Dictionary<T, T> not)
                    {
                        if(not[record] is IReconcilable<T> updatable)
                        {
                            updatable.UpdateFrom(record);
                        }
                    }
                }
                foreach (T record in NewerInB)
                {
                    if (Not is Dictionary<T, T> not)
                    {
                        if(not[record] is IReconcilable<T> updatable)
                        {
                            updatable.UpdateFrom(record);
                        }
                    }
                }
                #endregion L o c a l  M e t h o d s
            }

            public static Func<IReconciled> DefaultExec { get; set; }

            public static Reconciled<T> operator ++(Reconciled<T> original)
            {
                original.Apply();
                if(DefaultExec() is Reconciled<T> reconciled)
                {
                    return reconciled;
                }
                else throw new InvalidCastException();
            }
        }
    }
}
