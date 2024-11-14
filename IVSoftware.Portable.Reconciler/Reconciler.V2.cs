using System;
using System.Collections.Generic;

namespace IVSoftware.Portable
{
    partial class Reconciler
    {
        /// <summary>
        /// Reconciles two collections using custom UID and version comparers, 
        /// and allows setting a DiffReportMode to control the reporting of differences.
        /// </summary>
        /// <typeparam name="T">The type of elements in the collections.</typeparam>
        /// <param name="srceA">The first collection (List A) to reconcile.</param>
        /// <param name="srceB">The second collection (List B) to reconcile.</param>
        /// <param name="uidSorter">A function to compare the UID of two elements.</param>
        /// <param name="versionComparer">A function to compare the version or timestamp of two elements.</param>
        /// <param name="diffMode">Specifies the reporting mode for differences.</param>
        /// <param name="resultSorter">Optional function to sort the final results.</param>
        /// <returns>A Reconciled object containing categorized results.</returns>
        public static Reconciled<T> Reconcile<T>(
            IEnumerable<T> srceA,
            IEnumerable<T> srceB,
            Func<T, T, CompareUIDResult> uidSorter,
            Func<T, T, CompareVersionResult> versionComparer,
            OnReport diffMode,
            Func<T, T, int> resultSorter = null
        )
        {
            var reconciled = Reconcile(srceA, srceB, uidSorter, versionComparer, resultSorter);
            reconciled.DiffMode = diffMode;
            return reconciled;
        }

        /// <summary>
        /// Reconciles two collections where elements implement IReconcilable, 
        /// using the default UID and version comparison properties, and allows setting a DiffReportMode.
        /// </summary>
        /// <typeparam name="T">The type of elements in the collections, constrained to IReconcilable.</typeparam>
        /// <param name="srceA">The first collection (List A) to reconcile.</param>
        /// <param name="srceB">The second collection (List B) to reconcile.</param>
        /// <param name="diffMode">Specifies the reporting mode for differences.</param>
        /// <returns>A Reconciled object containing categorized results.</returns>
        public static Reconciled<T> Reconcile<T>(
            IEnumerable<T> srceA,
            IEnumerable<T> srceB,
            OnReport diffMode
        ) where T : IReconcilable
        {
            var reconciled = Reconcile(
                srceA,
                srceB,
                (a, b) => (CompareUIDResult)a.UIDCompareProperty.CompareTo(b.UIDCompareProperty),
                (a, b) => (CompareVersionResult)a.VersionCompareProperty.CompareTo(b.VersionCompareProperty),
                (a, b) => a.ResultSorterProperty?.CompareTo(b?.ResultSorterProperty) ?? 0);
            reconciled.DiffMode = diffMode;
            return reconciled;
        }

        /// <summary>
        /// Reconciles two collections where elements implement IReconcilable, 
        /// using the default UID and version comparison properties, and allows setting a DiffHandleMode.
        /// </summary>
        /// <typeparam name="T">The type of elements in the collections, constrained to IReconcilable.</typeparam>
        /// <param name="srceA">The first collection (List A) to reconcile.</param>
        /// <param name="srceB">The second collection (List B) to reconcile.</param>
        /// <param name="diffMode">Specifies the handling mode for differences.</param>
        /// <returns>A Reconciled object containing categorized results.</returns>
        public static Reconciled<T> Reconcile<T>(
            IEnumerable<T> srceA,
            IEnumerable<T> srceB,
            OnCollision diffMode
        ) where T : IReconcilable
        {
            var reconciled = Reconcile(
                srceA,
                srceB,
                (a, b) => (CompareUIDResult)a.UIDCompareProperty.CompareTo(b.UIDCompareProperty),
                (a, b) => (CompareVersionResult)a.VersionCompareProperty.CompareTo(b.VersionCompareProperty),
                (a, b) => a.ResultSorterProperty?.CompareTo(b?.ResultSorterProperty) ?? 0);
            reconciled.DiffMode = diffMode;
            return reconciled;
        }
    }
}
