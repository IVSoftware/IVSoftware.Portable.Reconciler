using System;
using System.Collections;

namespace IVSoftware.Portable
{
    #region S T A B L E
    public interface IReconciled
    {
        bool HasChanges { get; }
        IEnumerable Equal { get; }
        IEnumerable OnlyInA { get; }
        IEnumerable OnlyInB { get; }
        IEnumerable NewerInA { get; }
        IEnumerable NewerInB { get; }
        IEnumerable Not { get; }
        IEnumerable Collisions { get; }
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
    public interface IReconcilableDiffs
    {
        bool IsModified { get; }
    }
    #endregion S T A B L E
}