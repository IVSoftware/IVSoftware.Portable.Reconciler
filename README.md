## IVSoftware.Portable.Reconciler

`Reconciler` class encapsulates a `static` method to efficiently sort the two lists into five categories, using the sort and compare criteria supplied to the method. 

#### Categories

- Items that are only contained in the A list.
- Items that are only contained in the B list.
- Items that are contained in both lists, where A has the newer time stamp.
- Items that are contained in both lists, where B has the newer time stamp.
- Items whose sort criteria and time stamps are identical.

___

**Method**

```
/// <summary>
/// Static method to categorize two collections of items.
/// </summary>
/// <param name="srceA">An enumerable collection of T informally known as the A List.</param>
/// <param name="srceB">An enumerable collection of T informally known as the B List.</param>
/// <param name="uidSorter">A CompareUIDResult method that compares two T objects for sorting.</param>
/// <param name="versionComparer">A CompareVersionResult method that compares two T objects for <, >, == </param>
/// <param name="resultSorter">Optional Comparer T to return values in order.</param>
/// <returns>Reconciled T</returns>
/// <exception cref="NotImplementedException"></exception>
public static Reconciled<T> Reconcile<T>(
	IEnumerable<T> srceA, 
	IEnumerable<T> srceB, 
	Func<T, T, CompareUIDResult> uidSorter, 
	Func<T, T, CompareVersionResult> versionComparer,
	Func<T, T, int> resultSorter = null
);
```

___

**Example class**
```
class DatabaseRecord
{
    public object Guid { get;  }
        
    public DateTime TimeStamp { get; }

    // Other properties...
}
```

___

**CompareUIDResult Example**

##### Named delegate

The uidSorter sorts in ascending order. This argument can be passed by making a named method and passing 'just' the name:

```csharp
CompareUIDResult CompareUID<DatabaseRecord>(a,b) => (CompareUIDResult)a.Guid.CompareTo(b.Guid);
```

In this case the argument would be `uidSorter: CompareUID`,


##### Anonymous delegate

It's usually easier to use an anonymous delegate and pass the method inline:

In this case the argument would be `uidSorter: (a,b)=> (CompareUIDResult)a.Guid.CompareTo(b.Guid)`


___

**VersionComparer example**

##### Named delegate

The versionComparer sorts in descending order. This argument can be passed by making a named method and passing 'just' the name:

```csharp
CompareVersionResult CompareVersion<DatabaseRecord>(a,b) => (CompareVersionResult)a.TimeStamp.CompareTo(b.TimeStamp);
```

In this case the argument would be `versionComparer: CompareUID`,


##### Anonymous delegate

It's usually easier to use an anonymous delegate and pass the method inline:

In this case the argument would be `versionComparer: (a,b)=> (CompareVersionResult)a.TimeStamp.CompareTo(b.TimeStamp)`

___

**Reconciled return class**

##### Properties

```
/// <summary>
/// Items that are only contained in the A list.
/// </summary>
public T[] OnlyInA { get; }

/// <summary>
/// Items contained in both lists, where A has the newer time stamp.
/// </summary>
public T[] NewerInA { get; }

/// <summary>
/// Items that are only contained in the B list.
/// </summary>
public T[] OnlyInB { get; }

/// <summary>
/// Items contained in both lists, where B has the newer time stamp.
/// </summary>
public T[] NewerInB { get; }

/// <summary>
/// Items contained in both lists, whose sort criteria and time stamps are identical.
/// </summary>
public Tuple<T, T>[] Equal { get; }

/// <summary>
/// Detects whether the two lists are 'not' in sync.
/// </summary>
public bool HasChanges { get;}
			
/// <summary>
/// Using the item that 'is' newer as a key,
/// returns the item that 'is not' newer
/// </summary>
public Dictionary<T, T> Not { get; }
```

___

##### Method

```csharp
/// <summary>
/// Verbose listing of the items in their categories.
/// </summary>
public override string ToString();
```