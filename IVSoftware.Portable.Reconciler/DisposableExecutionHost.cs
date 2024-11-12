using IVSoftware.Portable.Disposable;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading;

namespace IVSoftware.Portable
{
    #region S T A B L E
    public class DisposableExecutionHost : DisposableHost
    {
        public DisposableExecutionHost()
        {
            CountChanged += (sender, e) =>
            {
                switch (e.Count.CompareTo(_stackMode.Count))
                {
                    case -1:
                        _stackMode.Pop();
                        break;
                    default:
                        // Value has been pushed.
                        break;
                    case +1:
                        // Something has gone catastrophically wrong here,
                        throw new InvalidOperationException(
                            "By design, _stackMode.Count should NEVER be > Count");
                }
            };
        }
        private static int _prevCount;

        public IDisposable GetToken(ReconciliationMode mode)
        {
            lock (_lock)
            {
                _stackMode.Push(mode);
            }
            return base.GetToken();
        }
        public ReconciliationMode DefaultMode { get; } = ReconciliationMode.Append;
        public ReconciliationMode Mode
        {
            get
            {
                ReconciliationMode mode;
                lock (_lock)
                {
                    mode =
                        _stackMode.Any() ?
                            _stackMode.Peek() :
                            DefaultMode;
                }
                return mode;
            }
        }
        private readonly static object _lock = new object();

        private readonly static Stack<ReconciliationMode> _stackMode = new Stack<ReconciliationMode>();

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
        public IList A { get; private set; }
        public IList B { get; private set; }

        public new IDisposable GetToken(object sender = null, Dictionary<string, object> properties = null) =>
            throw new InvalidOperationException($"{nameof(DisposableExecutionHost)} requires {nameof(ReconciliationMode)} arg for {nameof(GetToken)}()");

        public IDisposable GetToken(string key, object value) =>
            throw new InvalidOperationException($"{nameof(DisposableExecutionHost)} requires {nameof(ReconciliationMode)} arg for {nameof(GetToken)}()");

        public IDisposable GetToken(object sender, string key, object value) =>
            throw new InvalidOperationException($"{nameof(DisposableExecutionHost)} requires {nameof(ReconciliationMode)} arg for {nameof(GetToken)}()");
    }
    #endregion S T A B L E
}
