using IVSoftware.Portable.Disposable;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;

namespace IVSoftware.Portable
{
    #region S T A B L E
    public class DisposableApplyContext : DisposableHost
    {
        public DisposableApplyContext()
        {
            CountChanged += (sender, e) =>
            {
                switch (e.Count.CompareTo(_stackMode.Count))
                {
                    case -1:
                        _stackMode.Pop();
                        _stackSrceA.Pop();
                        _stackSrceB.Pop();
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
            lock(_lock)
            {
                _stackMode.Push(mode);
                _stackSrceA.Push(_srceA);
                _stackSrceB.Push(_srceB);
            }
            return base.GetToken();
        }

        public IDisposable GetToken(IEnumerable srceA, IEnumerable srceB)
        {
            lock (_lock)
            {
                _stackMode.Push(Mode);
                _stackSrceA.Push(srceA);
                _stackSrceB.Push(srceB);
            }
            return base.GetToken();
        }

        public IDisposable GetToken(IEnumerable srceA, IEnumerable srceB, ReconciliationMode mode)
        {
            lock (_lock)
            {
                _stackMode.Push(mode);
                _stackSrceA.Push(srceA);
                _stackSrceB.Push(srceB);
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
        public IEnumerable SrceA
        {
            get
            {
                internalUpdateTandemLists();
                return _srceA;
            }
        }
        IEnumerable _srceA = null;
        public IEnumerable SrceB
        {
            get
            {
                internalUpdateTandemLists();
                return _srceB;
            }
        }
        IEnumerable _srceB = null;

        private void internalUpdateTandemLists()
        {
            lock (_lock)
            {
                _srceA =
                    _stackSrceA.Any() ?
                        _stackSrceA.Peek() :
                        null;
                _srceB =
                    _stackSrceB.Any() ?
                        _stackSrceB.Peek() :
                        null;
            }
        }
        private readonly static object _lock = new object();

        private readonly static Stack<ReconciliationMode> _stackMode = new Stack<ReconciliationMode>();
        private readonly static Stack<IEnumerable> _stackSrceA = new Stack<IEnumerable>();
        private readonly static Stack<IEnumerable> _stackSrceB = new Stack<IEnumerable>();

        public new IDisposable GetToken(object sender = null, Dictionary<string, object> properties = null) =>
            throw new InvalidOperationException($"{nameof(DisposableApplyContext)} requires {nameof(ReconciliationMode)} arg for {nameof(GetToken)}()");

        public new IDisposable GetToken(string key, object value) =>
            throw new InvalidOperationException($"{nameof(DisposableApplyContext)} requires {nameof(ReconciliationMode)} arg for {nameof(GetToken)}()");

        public new IDisposable GetToken(object sender, string key, object value) =>
            throw new InvalidOperationException($"{nameof(DisposableApplyContext)} requires {nameof(ReconciliationMode)} arg for {nameof(GetToken)}()");
    }
    #endregion S T A B L E
}
