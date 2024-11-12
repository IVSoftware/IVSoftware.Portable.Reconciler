using IVSoftware.Portable.Disposable;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading;

namespace IVSoftware.Portable
{

    public class DisposableExecutionHost : DisposableHost
    {
        public IDisposable GetToken(ReconciliationMode mode, IList a = null, IList b = null)
        {
            if (a != null) A = a;
            if (b != null) B = b;
            if (A is null || B is null)
            {
                throw new ArgumentNullException($"Requires non-null {nameof(IList)} arguments to {nameof(SetTargets)}() for the first time.");
            }
            Mode = mode;
            return base.GetToken();
        }
        public DisposableExecutionHost(IList a = null, IList b = null)
        {
            A = a;
            B = b;
        }
        public void SetTargets(IList a, IList b)
        {
            A = a; 
            B = b;
        }
        public void SetReconciliationMode(ReconciliationMode mode) 
            => Mode = mode;
        public IList A { get; private set; }
        public IList B { get; private set; }
        public ReconciliationMode Mode { get; private set; }

        public new IDisposable GetToken(object sender = null, Dictionary<string, object> properties = null) =>
            throw new InvalidOperationException($"{nameof(DisposableExecutionHost)} requires {nameof(ReconciliationMode)} arg for {nameof(GetToken)}()");

        public IDisposable GetToken(string key, object value) =>
            throw new InvalidOperationException($"{nameof(DisposableExecutionHost)} requires {nameof(ReconciliationMode)} arg for {nameof(GetToken)}()");

        public IDisposable GetToken(object sender, string key, object value) =>
            throw new InvalidOperationException($"{nameof(DisposableExecutionHost)} requires {nameof(ReconciliationMode)} arg for {nameof(GetToken)}()");
    }
}
