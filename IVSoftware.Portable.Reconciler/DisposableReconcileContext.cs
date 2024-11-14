using IVSoftware.Portable.Disposable;
using System;
using System.Collections;
using System.Collections.Generic;

namespace IVSoftware.Portable
{
    #region S T A B L E
    public class DisposableReconcileContext : DisposableHost
    {
        public DisposableReconcileContext()
        {
            CountChanged += (sender, e) =>
            {
                switch (e.Count.CompareTo(_stackContext.Count))
                {
                    case -1:
                        _stackContext.Pop();
                        break;
                    default:
                        // Value has been pushed.
                        break;
                    case +1:
                        throw new InvalidOperationException(
                            "By design, _stackMode.Count should NEVER be > Count");
                }
            };
        }

        private static int _prevCount;

        private ReconcileContext internalCreateNewContext(
            IEnumerable srceA = null,
            IEnumerable srceB = null,
            OnReconcile? onReconcile = null,
            OnCollision? onCollision = null)
        {
            var baseContext = CurrentContext;

            return new ReconcileContext(
                srceA ?? baseContext.SrceA,
                srceB ?? baseContext.SrceB,
                onReconcile ?? baseContext.OnReconcile,
                onCollision ?? baseContext.OnCollision
            );
        }


        public IDisposable GetToken(OnReconcile onReconcile)
        {
            lock (_lock)
            {
                _stackContext.Push(internalCreateNewContext(onReconcile: onReconcile));
            }
            return base.GetToken();
        }

        public IDisposable GetToken(OnCollision onCollision)
        {
            lock (_lock)
            {
                _stackContext.Push(internalCreateNewContext(onCollision: onCollision));
            }
            return base.GetToken();
        }

        public IDisposable GetToken(IEnumerable srceA, IEnumerable srceB)
        {
            lock (_lock)
            {
                _stackContext.Push(internalCreateNewContext(srceA: srceA, srceB: srceB));
            }
            return base.GetToken();
        }

        public IDisposable GetToken(IEnumerable srceA, IEnumerable srceB, OnReconcile mode)
        {
            lock (_lock)
            {
                _stackContext.Push(internalCreateNewContext(srceA: srceA, srceB: srceB, onReconcile: mode));
            }
            return base.GetToken();
        }

        public ReconcileContext DefaultContext => _defaultContext;
        private ReconcileContext _defaultContext = new ReconcileContext();

        public ReconcileContext CurrentContext => _stackContext.Count == 0 ? DefaultContext : _stackContext.Peek();

        public void SetDefaultApplyContext(ReconcileContext context) => _defaultContext = context;

        public void ClearDefaultApplyContext() => _defaultContext = new ReconcileContext();

        public override string ToString() => CurrentContext.ToString();

        public OnReconcile OnReconcile
        {
            get
            {
                lock (_lock)
                {
                    return CurrentContext?.OnReconcile ?? DefaultContext.OnReconcile;
                }
            }
        }

        public IEnumerable SrceA
        {
            get
            {
                lock (_lock)
                {
                    return CurrentContext?.SrceA ?? DefaultContext.SrceA;
                }
            }
        }

        public IEnumerable SrceB
        {
            get
            {
                lock (_lock)
                {
                    return CurrentContext?.SrceB ?? DefaultContext.SrceB;
                }
            }
        }

        private readonly static object _lock = new object();
        private readonly static Stack<ReconcileContext> _stackContext = new Stack<ReconcileContext>();

        public new IDisposable GetToken(object sender = null, Dictionary<string, object> properties = null) =>
            throw new InvalidOperationException($"{nameof(Portable.DisposableReconcileContext)} requires {nameof(Portable.OnReconcile)} arg for {nameof(GetToken)}()");

        public new IDisposable GetToken(string key, object value) =>
            throw new InvalidOperationException($"{nameof(Portable.DisposableReconcileContext)} requires {nameof(Portable.OnReconcile)} arg for {nameof(GetToken)}()");

        public new IDisposable GetToken(object sender, string key, object value) =>
            throw new InvalidOperationException($"{nameof(Portable.DisposableReconcileContext)} requires {nameof(Portable.OnReconcile)} arg for {nameof(GetToken)}()");
    }
    #endregion S T A B L E
}
