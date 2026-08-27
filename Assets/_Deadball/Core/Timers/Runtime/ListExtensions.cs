using System.Collections.Generic;

namespace Zone8.ImprovedTimers
{
    /// <summary>
    /// The one list helper this assembly needs from Zone8.Utilities.
    /// </summary>
    /// <remarks>
    /// Declared internal and kept local so the timers can be dropped into a project without dragging
    /// in the utilities assembly and its UI and addressables dependencies. If the full utilities
    /// package is imported later, an internal extension cannot collide with the global one.
    /// </remarks>
    internal static class ListExtensions
    {
        /// <summary>Replaces the contents of <paramref name="list"/> with <paramref name="items"/>.</summary>
        internal static void RefreshWith<T>(this List<T> list, IEnumerable<T> items)
        {
            list.Clear();
            list.AddRange(items);
        }
    }
}
