# IVSoftware.Portable.Reconciler

`IVSoftware.Portable.Reconciler` provides functionality to reconcile two collections by categorizing differences into structured groups based on customizable sorting and comparison criteria. Version 2 introduces significant enhancements, transforming the `Reconciler` from a passive reporter into a tool capable of intelligently acting on source collections, especially when provided as `IList` instead of `IEnumerable`.

---

### Table of Contents


1. [Overview](#overview)
2. [Version 1 Features](#version-1-features)
    - [Categories](#categories)
    - [Reconcile Method](#reconcile-method)
    - [Example](#example-class)
3. [Version 2 Features](#version-2-features)
    - [Enhanced Reconcile Overloads](#enhanced-reconcile-overloads)
    - [Active Reconciliation and the reconciled++ Operator](#active-reconciliation-and-the-reconciled-operator)
    - [Benefits of the New Interfaces](#benefits-of-the-new-interfaces)
    - [Diff Reporting and Handling Modes](#diff-reporting-and-handling-modes)
    - [Compatibility with Version 1](#compatibility-with-version-1)
4. [Reconciled Class](#reconciled-class)
5. [Example Call](#example-of-a-call)

---

## Overview

The `Reconciler` class provides a set of methods to compare two collections and categorize the differences into groups for efficient and structured reconciliation.

---

## Version 1 Features

### Categories

In Version 1, `Reconciler` categorized items from two collections into the following groups:

- **OnlyInA**: Items that exist only in collection A.
- **OnlyInB**: Items that exist only in collection B.
- **NewerInA**: Items present in both collections where the item in A has a more recent timestamp.
- **NewerInB**: Items present in both collections where the item in B has a more recent timestamp.
- **Equal**: Items with identical sort criteria and timestamps in both collections.

> **Note**: Items in the **Equal** category are determined solely by the supplied comparers for UID and Version, leaving additional validation to the user's discretion.

### Reconcile Method

~~~csharp
/// <summary>
/// Static method to categorize two collections of items.
/// </summary>
/// <param name="srceA">The first collection to reconcile.</param>
/// <param name="srceB">The second collection to reconcile.</param>
/// <param name="uidSorter">A function to compare UIDs of elements.</param>
/// <param name="versionComparer">A function to compare version/timestamps of elements.</param>
/// <param name="resultSorter">Optional function to sort the final results.</param>
/// <returns>Reconciled collection of type T</returns>
public static Reconciled<T> Reconcile<T>(
    IEnumerable<T> srceA, 
    IEnumerable<T> srceB, 
    Func<T, T, CompareUIDResult> uidSorter, 
    Func<T, T, CompareVersionResult> versionComparer,
    Func<T, T, int> resultSorter = null
);
~~~

---

### Example Class

Below is an example class that could be used with the `Reconciler`:

~~~csharp
class DatabaseRecord
{
    public object Guid { get; }
    public DateTime TimeStamp { get; }
    // Other properties...
}
~~~

### CompareUIDResult Example

#### Named Delegate

Define a `uidSorter` as a named method for ascending sorting by UID:

~~~csharp
CompareUIDResult CompareUID<DatabaseRecord>(a, b) => (CompareUIDResult)a.Guid.CompareTo(b.Guid);
~~~

In this case, `uidSorter` would be passed as `CompareUID`.

#### Anonymous Delegate

Alternatively, you can define `uidSorter` inline as an anonymous function:

~~~csharp
uidSorter: (a, b) => (CompareUIDResult)a.Guid.CompareTo(b.Guid)
~~~

### VersionComparer Example

#### Named Delegate

Define `versionComparer` as a named method for descending sorting by timestamp:

~~~csharp
CompareVersionResult CompareVersion<DatabaseRecord>(a, b) => (CompareVersionResult)a.TimeStamp.CompareTo(b.TimeStamp);
~~~

In this case, `versionComparer` would be passed as `CompareVersion`.

#### Anonymous Delegate

You can also define `versionComparer` inline as an anonymous function:

~~~csharp
versionComparer: (a, b) => (CompareVersionResult)a.TimeStamp.CompareTo(b.TimeStamp)
~~~

---

## Version 2 Features

### Enhanced Reconcile Overloads

Version 2 introduces three new overloads of the `Reconcile` method, expanding options for handling differences and conflicts between collections.

1. **Compatibility Overload**: The first overload is compatible with Version 1 and introduces a minor addition—`DiffReportMode`. This setting enables users to take advantage of Version 2's reporting options while maintaining compatibility with existing Version 1 calls. For example, if a unit test relies on the `ToString()` output format, this overload ensures backward compatibility, while allowing intentional upgrades to the new, more structured formatting without breaking existing tests.

2. **Explicit Interface Overloads**: The remaining two overloads are unique to Version 2 and require the collections' items to implement the `IReconcilable` interface. This interface enforces a structure for comparing items by UID and version, providing the ability to apply sophisticated reporting and handling modes (`DiffReportMode` and `DiffHandleMode`) tailored to active reconciliation needs.

#### Method Signatures

~~~csharp
/// <summary>
/// Reconciles two collections using custom UID and version comparers, 
/// and allows setting a DiffReportMode to control the reporting of differences.
/// This method is compatible with Version 1's `Reconcile` calls, 
/// adding support for Version 2-style diff reporting.
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
    DiffReportMode diffMode,
    Func<T, T, int> resultSorter = null
);
~~~

~~~csharp
/// <summary>
/// Reconciles two collections where elements implement IReconcilable, 
/// using the default UID and version comparison properties, and allows setting a DiffReportMode.
/// </summary>
/// <typeparam name="T">The type of elements in the collections, constrained to IReconcilable.</typeparam>
/// <param name="srceA">The first collection to reconcile.</param>
/// <param name="srceB">The second collection to reconcile.</param>
/// <param name="diffMode">Specifies the reporting mode for differences.</param>
/// <returns>A Reconciled object containing categorized results.</returns>
public static Reconciled<T> Reconcile<T>(
    IEnumerable<T> srceA,
    IEnumerable<T> srceB,
    DiffReportMode diffMode
) where T : IReconcilable;
~~~

~~~csharp
/// <summary>
/// Reconciles two collections where elements implement IReconcilable, 
/// using the default UID and version comparison properties, and allows setting a DiffHandleMode.
/// </summary>
/// <typeparam name="T">The type of elements in the collections, constrained to IReconcilable.</typeparam>
/// <param name="srceA">The first collection to reconcile.</param>
/// <param name="srceB">The second collection to reconcile.</param>
/// <param name="diffMode">Specifies the handling mode for differences.</param>
/// <returns>A Reconciled object containing categorized results.</returns>
public static Reconciled<T> Reconcile<T>(
    IEnumerable<T> srceA,
    IEnumerable<T> srceB,
    DiffHandleMode diffMode
) where T : IReconcilable;
~~~


### Active Reconciliation and the `reconciled++` Operator

In Version 1, the `Reconciler` was primarily a passive reporter, providing insights into differences without making changes to the collections. Version 2, however, introduces **active reconciliation** capabilities. When provided with collections as `IList`, the reconciler can directly apply modifications based on reconciliation results.

One powerful new feature is the `reconciled++` operator, which acts as a shortcut for the `ApplyWithLoopback` method. After executing `reconciled++`, the collections are updated so that all items ideally fall into the **Equal** category, reflecting an aligned, reconciled state.

### Benefits of the New Interfaces

Version 2 introduces four new interfaces that enhance flexibility and control over the reconciliation process:

- **`IReconciled`**: Represents the result of a reconciliation, including categorized collections of differences and methods to apply the reconciliation.
- **`IReconcilable`**: Defines properties (`UIDCompareProperty`, `VersionCompareProperty`, and `ResultSorterProperty`) that facilitate item comparison. Implementing `IReconcilable` explicitly allows dynamic routing, enabling these properties to return different values based on client-side logic.
- **`IReconcilable<T>`**: Extends `IReconcilable` with an `UpdateFrom` method to update an item with values from another item of the same type, enabling more controlled synchronization.
- **`IReconcilableDiffs`**: Contains an `IsModified` property, useful for tracking whether an item has changed since the last synchronization.

### Diff Reporting and Handling Modes

Version 2 introduces two new options to control how differences are managed:

- **DiffReportMode**: Controls how differences between collections are reported.
- **DiffHandleMode**: Defines how conflicting or ambiguous differences (Collisions) are managed.

### Compatibility with Version 1

Version 2 is fully compatible with Version 1. The original `Reconcile` method remains unchanged, and the new overloads are optional, allowing users to utilize Version 2 features without modifying existing Version 1 code.

---

## Reconciled Class

The `Reconciled` class represents the categorized reconciliation results, with properties for each category:

~~~csharp
/// <summary>
/// Items only in List A.
/// </summary>
public T[] OnlyInA { get; }

/// <summary>
/// Items present in both lists, where List A has a more recent timestamp.
/// </summary>
public T[] NewerInA { get; }

/// <summary>
/// Items only in List B.
/// </summary>
public T[] OnlyInB { get; }

/// <summary>
/// Items present in both lists, where List B has a more recent timestamp.
/// </summary>
public T[] NewerInB { get; }

/// <summary>
/// Items with identical sort criteria and timestamps in both lists.
/// </summary>
public Tuple<T, T>[] Equal { get; }

/// <summary>
/// Indicates if the lists are not in sync.
/// </summary>
public bool HasChanges { get; }

/// <summary>
/// Returns the item that is not the newest, using the newer item as a key.
/// </summary>
public Dictionary<T, T> Not { get; }
~~~

### Method

~~~csharp
public override string ToString();
~~~

The `ToString` method provides a verbose listing of the items by category, useful for debugging and logging reconciliation results.

---

## Example of a Call

Below is an example demonstrating how to call the `Reconcile` method with sample data and comparison functions.

~~~csharp
Reconciled<DatabaseRecord> result = Reconciler.Reconcile(
    listA, 
    listB, 
    (a, b) => (CompareUIDResult)a.Guid.CompareTo(b.Guid),
    (a, b) => (CompareVersionResult)a.TimeStamp.CompareTo(b.TimeStamp)
);
~~~
