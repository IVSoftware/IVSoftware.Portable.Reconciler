using System;
using System.Collections.Generic;
using System.Text;
using static IVSoftware.Portable.Reconciler;

namespace IVSoftware.Portable
{
    using System.Collections;
    using System.Collections.Generic;
    using System.Reflection;

    public interface IReconciled
    {
        bool HasChanges { get; }
        IEnumerable Equal { get; }
        IEnumerable OnlyInA { get; }
        IEnumerable OnlyInB { get; }
        IEnumerable NewerInA { get; }
        IEnumerable NewerInB { get; }
        IEnumerable Not { get; }
        IEnumerable Diffs { get; }
        void Apply();
        IReconciled ApplyWithLoopback(Func<IReconciled> customExec = null);
    }
    public interface IReconcilable : ICloneable
    {
        /// <summary>
        /// A simple or complex property with standard polarity.
        /// The IComparer will render this as a CompareUIDResult 
        /// which has standard polarity.
        /// </summary>
        /// <remarks>
        /// This property cannot be null
        /// </remarks>
        IComparable UIDCompareProperty { get; }

        /// <summary>
        /// A simple or complex property with standard polarity.
        /// The IComparer will render this as a CompareVersionResult 
        /// which has inverted polarity. i.e. "NewerInA" is 1 not -1 
        /// indicating that A has a higher value for version.
        /// </summary>
        /// <remarks>
        /// This property cannot be null
        /// </remarks>
        IComparable VersionCompareProperty { get; }

        /// <summary>
        /// A simple or complex property with standard polarity.
        /// The IComparer will render this as a CompareVersionResult 
        /// which has inverted polarity. i.e. "NewerInA" is 1 not -1 
        /// indicating that A has a higher value for version.
        /// </summary>
        /// <remarks>
        /// This property can be null and is only required when results 
        /// should be sorted in a different order than the UID comparer
        /// already provides..
        /// </remarks>
        IComparable ResultSorterProperty { get; }
    }
    public interface IReconcilable<T> : IReconcilable
    {
        T UpdateFrom(T value);
    }
}
