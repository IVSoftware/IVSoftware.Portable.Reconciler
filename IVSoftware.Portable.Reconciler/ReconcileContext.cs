using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace IVSoftware.Portable
{
    /// <summary>
    /// Configures parameters for <c>Apply()</c> and <c>ApplyWithLoopback()</c> methods in reconciliation, 
    /// including source collections and settings for handling reconciliation and collision behaviors.
    /// Defines how differences and conflicts between collections are identified and resolved.
    /// </summary>

    public class ReconcileContext
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="ReconcileContext"/> class, specifying the behaviors 
        /// for reconciliation and collision handling, without requiring source collections. This constructor 
        /// is suitable when only behavior settings are needed and carries srce values forward from previous context..
        /// </summary>
        /// <param name="onReconcile">Specifies the behavior for reconciliation; defaults to OnReconcile.Append.</param>
        /// <param name="onCollision">Specifies the behavior for handling collisions; defaults to OnCollision.Move.</param>
        public ReconcileContext(
            OnReconcile? onReconcile = null,
            OnCollision? onCollision = null)
        {
            OnReconcile = onReconcile ?? OnReconcile.Append;
            OnCollision = onCollision ?? OnCollision.Move;
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="ReconcileContext"/> class with specified source 
        /// collections and optional settings for <see cref="OnReconcile"/> and <see cref="OnCollision"/>.
        /// Use this constructor when defining both collections to be reconciled. The source arguments must 
        /// allow null for when progressive inherited context is null.
        /// </summary>
        /// <param name="srceA">The first source collection to be reconciled.</param>
        /// <param name="srceB">The second source collection to be reconciled.</param>
        /// <param name="onReconcile">Specifies the behavior for reconciliation; defaults to OnReconcile.Append.</param>
        /// <param name="onCollision">Specifies the behavior for handling collisions; defaults to OnCollision.Move.</param>
        /// <exception cref="ArgumentNullException">Thrown when srceA or srceB is null.</exception>
        public ReconcileContext(
            IEnumerable srceA,
            IEnumerable srceB,
            OnReconcile? onReconcile = null,
            OnCollision? onCollision = null)
            : this(onReconcile, onCollision)
        {
            SrceA = srceA;
            SrceB = srceB;
        }

        /// <summary>
        /// Gets the primary source collection used in reconciliation, representing the base data 
        /// against which differences will be identified.
        /// </summary>
        public IEnumerable SrceA { get; }

        /// <summary>
        /// Gets the secondary source collection used in reconciliation, representing the comparison 
        /// data set against which <see cref="SrceA"/> will be reconciled.
        /// </summary>
        public IEnumerable SrceB { get; }

        /// <summary>
        /// Gets the behavior setting for reconciliation.
        /// <see cref="Portable.OnReconcile"/>
        /// </summary>
        public OnReconcile OnReconcile { get; } = OnReconcile.Append;

        /// <summary>
        /// Gets the behavior setting for handling collisions between collections (where one 
        /// record of the pair might have a newer timestamp but both records show modifications).
        /// <see cref="Portable.OnCollision"/>
        /// </summary>
        public OnCollision OnCollision { get; } = OnCollision.Move;

        public override string ToString()
        {
            var builder = new List<string>();

            builder.Add(localGetIEnumerableTypeName(SrceA));
            builder.Add(localGetIEnumerableTypeName(SrceB));
            builder.Add($"{typeof(OnCollision).Name}.{OnCollision}");
            builder.Add($"{typeof(OnReconcile).Name}.{OnReconcile}");

            return string.Join(Environment.NewLine, builder);

            #region L o c a l M e t h o d s
            string localGetIEnumerableTypeName(IEnumerable source)
            {
                if (source == null) return "null";

                var type = source.GetType();

                if (type.IsGenericType && typeof(IEnumerable).IsAssignableFrom(type))
                {
                    var genericArguments = type.GetGenericArguments();
                    var genericTypeNames = string.Join(", ", genericArguments.Select(t => t.Name));
                    return $"{typeof(IEnumerable).Name}<{genericTypeNames}>";
                }

                return type.Name; // Non-generic type
            }
            #endregion L o c a l M e t h o d s
        }
    }
}
