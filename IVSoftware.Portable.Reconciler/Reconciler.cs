using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;

namespace IVSoftware.Portable
{
    public class Reconciler
	{
		public enum CompareUIDResult
		{
			/// <summary>
			/// A occurs sooner in list than B
			/// </summary>
			OnlyInA = -1,
			/// <summary>
			/// The UID is the same.
			/// </summary>
			InBoth = 0,
			/// <summary>
			/// B occurs sooner in list than A
			/// </summary>
			OnlyInB = 1,
		}

		/// <summary>
		/// POLARITY REVERSED: Whether the property is DateTime or a 
		/// version like 1.0.00, the greater-than result is the newest. 
		/// </summary>
		public enum CompareVersionResult
		{
			/// <summary>
			/// A is Higher, therefore newer
			/// </summary>
			NewerIsX = 1,

			/// <summary>
			/// The versions are identical
			/// </summary>
			Equal = 0,

			/// <summary>
			/// B is higher, therefore newer.
			/// </summary>
			NewerIsY = -1,
		}


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
		)
		{
			var a = srceA.ToList();
			var b = srceB.ToList();
			var onlyInA = new List<T>();
			var onlyInB = new List<T>(); 
			var newerInA = new List<T>();
			var newerInB = new List<T>();
			var equal = new List<Tuple<T, T>>();
			var not = new Dictionary<T, T>();

			a.Sort((x, y)=> (int)uidSorter(x, y));
			b.Sort((x, y)=> (int)uidSorter(x, y));

			while(a.Any() && b.Any())
			{
				T a0 = a[0];
				T b0 = b[0];

				var compareUID = uidSorter(a0, b0);
				switch (compareUID)
				{
					case CompareUIDResult.OnlyInA:
						onlyInA.Add(a0);
						a.RemoveAt(0);
						break;
					case CompareUIDResult.InBoth:
						var compareVersion = versionComparer(a0, b0);
						switch (compareVersion)
						{
							case CompareVersionResult.NewerIsX:
								newerInA.Add(a0);
								not[a0] = b0;
								break;
							case CompareVersionResult.Equal:
								equal.Add(new Tuple<T, T>(a0, b0));
								break;
							case CompareVersionResult.NewerIsY:
								newerInB.Add(b0);
								not[b0] = a0;
								break;
							default:
								throw new NotImplementedException($"'{compareVersion}' is not a valid compare result");
						}
						// Toss both, regardless of which is newer.
						a.RemoveAt(0);
						b.RemoveAt(0);
						break;
					case CompareUIDResult.OnlyInB:
						onlyInB.Add(b0);
						b.RemoveAt(0);
						break;
					default:
						throw new NotImplementedException($"'{compareUID}' is not a valid compare result");
				}
			}
			Debug.Assert(!(a.Any() && b.Any()));

			// Leftovers
			if (a.Any())
			{
				onlyInA.AddRange(a);
			}
			else if (b.Any())
			{
				onlyInB.AddRange(b);
			}
			return new Reconciled<T>(
				onlyInA: onlyInA,
				onlyInB: onlyInB,
				newerInA: newerInA,
				newerInB: newerInB,
				equal: equal,
				resultSorter: resultSorter,
				not: not
			);
		}

		/// <summary>
		/// Presents the results of the reconciliation
		/// </summary>
		[System.Diagnostics.DebuggerDisplay("{DebuggerDisplay}")]
		public class Reconciled<T>
		{
			public Reconciled(
				List<T> onlyInA, 
				List<T> onlyInB,
				List<T> newerInA, 
				List<T> newerInB, 
				List<Tuple<T, T>> equal,
				Func<T, T, int> resultSorter,
				Dictionary<T,T> not)
			{
				if(resultSorter != null)
				{
					onlyInA.Sort((x, y) => resultSorter(x, y));
					onlyInB.Sort((x, y) => resultSorter(x, y));
					newerInA.Sort((x, y) => resultSorter(x, y));
					newerInB.Sort((x, y) => resultSorter(x, y));
					equal.Sort((x, y) => resultSorter(x.Item1, y.Item1));
				}
				OnlyInA = onlyInA.ToArray();
				OnlyInB = onlyInB.ToArray();
				NewerInA = newerInA.ToArray();
				NewerInB = newerInB.ToArray();
				Equal = equal.ToArray();
				Not = not;
			}

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
			public bool HasChanges =>
				OnlyInA.Any() ||
				NewerInA.Any() ||
				OnlyInB.Any() ||
				NewerInB.Any();
			
			/// <summary>
			/// Using the item that 'is' newer as a key,
			/// returns the item that 'is not' newer
			/// </summary>
			public Dictionary<T, T> Not { get; }

			/// <summary>
			/// Verbose listing of the items in their categories.
			/// </summary>
			public override string ToString()
			{
				var equal =
					nameof(Equal) + Environment.NewLine +
					string.Join(
						Environment.NewLine, 
						Equal.Select(e => $"{e.Item1}{Environment.NewLine}{e.Item2}")) + 
						Environment.NewLine;
				var onlyInA =
					nameof(OnlyInA) + Environment.NewLine +
					string.Join(Environment.NewLine, OnlyInA.Select(a => $"{a}")) + Environment.NewLine;
				var onlyInB =
					nameof(OnlyInB) + Environment.NewLine +
					string.Join(Environment.NewLine, OnlyInB.Select(b => $"{b}")) + Environment.NewLine;
				var newerInA =
					nameof(NewerInA) + Environment.NewLine +
					string.Join(Environment.NewLine, NewerInA.Select(a => $"a:{a}{Environment.NewLine}b:{Not[a]}")) + Environment.NewLine;
				var newerInB =
					nameof(NewerInB) + Environment.NewLine +
					string.Join(Environment.NewLine, NewerInB.Select(b => $"a:{Not[b]}{Environment.NewLine}b:{b}")) + Environment.NewLine;
				var display = 
					equal +
					onlyInA +
					onlyInB +
					newerInA +
					newerInB;
				return display;
			}
			public string DebuggerDisplay
			{
				get
				{
					var builder = new List<string>();
					builder.Add($"{nameof(Equal)}={Equal.Length}");
					builder.Add($"{nameof(OnlyInA)}={OnlyInA.Length}");
					builder.Add($"{nameof(OnlyInB)}={OnlyInB.Length}");
					builder.Add($"{nameof(NewerInA)}={NewerInA.Length}");
					builder.Add($"{nameof(NewerInB)}={NewerInB.Length}");
					var display = "[" + string.Join("][", builder) + "]";
					return display;
				}
			}
		}
	}
}
