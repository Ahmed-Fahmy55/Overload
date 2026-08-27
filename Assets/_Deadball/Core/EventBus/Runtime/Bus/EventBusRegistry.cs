using System;
using System.Collections.Generic;

namespace Core.Events
{
    /// <summary>
    /// The catalogue of event buses that exist in the current domain.
    /// </summary>
    /// <remarks>
    /// Each <see cref="EventBus{T}"/> registers itself from its static constructor, that is, the
    /// first time it is used. Because generic statics are per-constructed-type, this registry is the
    /// only place from which all buses can be enumerated or cleared. Use
    /// <see cref="EventBusUtil.WarmBuses"/> to populate it with buses that have not been used yet.
    /// </remarks>
    public static class EventBusRegistry
    {
        static readonly Dictionary<Type, IEventBusHandle> s_handles = new();

        /// <summary>The buses created so far, keyed internally by event type.</summary>
        public static IReadOnlyCollection<IEventBusHandle> Buses => s_handles.Values;

        /// <summary>The number of buses created so far.</summary>
        public static int Count => s_handles.Count;

        /// <summary>
        /// Records a bus. Called from the static constructor of <see cref="EventBus{T}"/> and safe
        /// to call more than once for the same event type.
        /// </summary>
        public static void Register(IEventBusHandle handle)
        {
            if (handle == null) return;

            s_handles[handle.EventType] = handle;
        }

        /// <summary>Looks up the bus for <paramref name="eventType"/>, if it has been created.</summary>
        public static bool TryGet(Type eventType, out IEventBusHandle handle) => s_handles.TryGetValue(eventType, out handle);

        /// <summary>Removes every binding from every known bus.</summary>
        public static void ClearAll()
        {
            foreach (var handle in s_handles.Values)
                handle.Clear();
        }
    }
}
