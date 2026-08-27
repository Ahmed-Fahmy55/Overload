using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using UnityEngine;

namespace Core.Events
{
    /// <summary>
    /// Lifecycle entry point for the event bus system: creates a bus for every declared event type
    /// before the first scene loads, and clears all buses on request.
    /// </summary>
    public static class EventBusUtil
    {
        /// <summary>
        /// The discovery strategy used by <see cref="Initialize"/>. Editor code substitutes a
        /// TypeCache-backed implementation, which is considerably cheaper than an assembly scan.
        /// </summary>
        public static IEventTypeProvider TypeProvider { get; set; } = new AppDomainEventTypeProvider();

        /// <summary>The event types discovered by the most recent call to <see cref="Initialize"/>.</summary>
        public static IReadOnlyList<Type> EventTypes { get; private set; } = Array.Empty<Type>();

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        public static void Initialize() => EventTypes = WarmBuses(TypeProvider);

        /// <summary>
        /// Removes every binding from every bus. Called when leaving play mode so that static
        /// subscriptions do not survive into the next session when domain reload is disabled.
        /// </summary>
        public static void ClearAllBuses() => EventBusRegistry.ClearAll();

        /// <summary>
        /// Runs the static constructor of <c>EventBus&lt;T&gt;</c> for every event type supplied by
        /// <paramref name="provider"/>, so that <see cref="EventBusRegistry"/> also lists buses that
        /// no code has used yet.
        /// </summary>
        /// <returns>The event types that were discovered.</returns>
        public static IReadOnlyList<Type> WarmBuses(IEventTypeProvider provider)
        {
            if (provider == null) return Array.Empty<Type>();

            var eventTypes = provider.GetEventTypes();
            var busDefinition = typeof(EventBus<>);

            for (int i = 0; i < eventTypes.Count; i++)
            {
                try
                {
                    RuntimeHelpers.RunClassConstructor(busDefinition.MakeGenericType(eventTypes[i]).TypeHandle);
                }
                catch (Exception exception)
                {
                    // Ahead-of-time platforms cannot always construct a generic instantiation that
                    // no compiled code path references. Such a bus is unreachable at runtime anyway.
                    Debug.LogWarning($"[EventBus] Could not create a bus for '{eventTypes[i].Name}': {exception.Message}");
                }
            }

            return eventTypes;
        }
    }
}
