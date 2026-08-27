using System;
using System.Collections.Generic;
using UnityEditor;

namespace Core.Events.Editor
{
    /// <summary>
    /// Discovers event types through Unity's <see cref="TypeCache"/>.
    /// </summary>
    /// <remarks>
    /// The cache is built by the editor during compilation, making lookups substantially cheaper
    /// than an assembly scan. This matters for the debugger window, which queries on every repaint.
    /// </remarks>
    public sealed class TypeCacheEventTypeProvider : IEventTypeProvider
    {
        public IReadOnlyList<Type> GetEventTypes()
        {
            var results = new List<Type>();

            foreach (var type in TypeCache.GetTypesDerivedFrom<IEvent>())
            {
                if (type.IsAbstract || type.IsInterface || type.IsGenericTypeDefinition) continue;

                results.Add(type);
            }

            return results;
        }
    }
}
