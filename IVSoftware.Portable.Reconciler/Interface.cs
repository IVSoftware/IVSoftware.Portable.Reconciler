using System;
using System.Collections;

namespace IVSoftware.Portable
{
    #region S T A B L E
    public interface IReconciled
    {
        /// <summary>
        /// Indicates if there are any changes between the collections.
        /// </summary>
        bool HasChanges { get; }

        /// <summary>
        /// Collection of items found in both collections with identical UID and version.
        /// </summary>
        IEnumerable Equal { get; }

        /// <summary>
        /// Collection of items unique to the first (A) collection.
        /// </summary>
        IEnumerable OnlyInA { get; }

        /// <summary>
        /// Collection of items unique to the second (B) collection.
        /// </summary>
        IEnumerable OnlyInB { get; }

        /// <summary>
        /// Collection of items found in both collections where the item in A has a more recent version.
        /// </summary>
        IEnumerable NewerInA { get; }

        /// <summary>
        /// Collection of items found in both collections where the item in B has a more recent version.
        /// </summary>
        IEnumerable NewerInB { get; }

        /// <summary>
        /// Collection of items that do not meet the criteria to be grouped in any other specific category.
        /// </summary>
        IEnumerable Not { get; }

        /// <summary>
        /// Collection of items that have conflicts or discrepancies between collections, requiring further resolution.
        /// </summary>
        IEnumerable Collisions { get; }

        /// <summary>
        /// Applies the reconciliation changes to update the collections based on the categorized differences.
        /// </summary>
        void Apply(ReconcileContext applyContext = null);

        /// <summary>
        /// Applies reconciliation with an optional loopback function for iterative or custom execution.
        /// </summary>
        /// <param name="customExec">Optional function to customize the reconciliation process.</param>
        /// <returns>An updated IReconciled instance reflecting applied changes.</returns>
        IReconciled ApplyWithLoopback(Func<IReconciled> customExec = null, ReconcileContext applyContext = null);
    }

    public interface IReconcilable : ICloneable
    {
        /// <summary>
        /// Property used to compare items by unique identifier (UID) with standard polarity.
        /// This can be implemented explicitly, allowing dynamic redirection to a property 
        /// or method selected by external routing logic in the client code.
        /// </summary>
        /// <remarks>
        /// This property must not be null.
        /// </remarks>
        IComparable UIDCompareProperty { get; }

        /// <summary>
        /// Property used to compare items by version or timestamp, with inverted polarity.
        /// For example, "NewerInA" yields 1 instead of -1, indicating that A has a higher version.
        /// Like UIDCompareProperty, this can be implemented explicitly to allow routing.
        /// </summary>
        /// <remarks>
        /// This property must not be null.
        /// </remarks>
        IComparable VersionCompareProperty { get; }

        /// <summary>
        /// Optional property used for additional sorting of results.
        /// Provides a different order than the UID comparer if necessary.
        /// This property can also be implemented explicitly for flexible routing 
        /// based on client code logic.
        /// </summary>
        /// <remarks>
        /// This property can be null.
        /// </remarks>
        IComparable ResultSorterProperty { get; }
    }

    public interface IReconcilable<T> : IReconcilable
    {
        /// <summary>
        /// Updates the current item with values from another item of the same type.
        /// Used to merge or sync changes from a source item.
        /// </summary>
        /// <param name="value">The source item to update from.</param>
        /// <returns>The updated item with applied changes.</returns>
        T UpdateFrom(T value);
    }

    public interface IReconcilableDiffs
    {
        /// <summary>
        /// Indicates whether the item has been modified since the last synchronization.
        /// </summary>
        bool IsModified { get; }
    }
    #endregion S T A B L E
}