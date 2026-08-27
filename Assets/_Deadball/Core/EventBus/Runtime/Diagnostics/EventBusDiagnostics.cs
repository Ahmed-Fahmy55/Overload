using System;

namespace Core.Events
{
    /// <summary>
    /// The single observation point for event traffic across all buses.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Because generic statics are per-constructed-type, no single <see cref="EventBus{T}"/> can
    /// observe the others. This type provides the shared hook that diagnostic tooling subscribes to.
    /// </para>
    /// <para>
    /// The entire type is compiled out of player builds. Even in the editor, the bus performs no
    /// reporting work, and boxes no struct payload, until <see cref="Enabled"/> is set.
    /// </para>
    /// </remarks>
    public static class EventBusDiagnostics
    {
#if UNITY_EDITOR
        /// <summary>A single raise, captured at the moment of dispatch.</summary>
        public readonly struct RaiseInfo
        {
            /// <summary>The event type that was raised.</summary>
            public readonly Type EventType;

            /// <summary>The event payload, boxed.</summary>
            public readonly object Payload;

            /// <summary>The number of bindings registered when the event was raised.</summary>
            public readonly int Listeners;

            public readonly int Frame;
            public readonly double Time;

            public RaiseInfo(Type eventType, object payload, int listeners, int frame, double time)
            {
                EventType = eventType;
                Payload = payload;
                Listeners = listeners;
                Frame = frame;
                Time = time;
            }
        }

        /// <summary>
        /// Gate evaluated by the bus before any reporting work is performed. Left false unless a
        /// diagnostic tool is actively recording.
        /// </summary>
        public static bool Enabled { get; set; }

        /// <summary>Raised once per <see cref="EventBus{T}.Raise"/> call while <see cref="Enabled"/> is set.</summary>
        public static event Action<RaiseInfo> Raised;

        /// <summary>Reports a raise. Called by <see cref="EventBus{T}"/> before its handlers run.</summary>
        public static void ReportRaise(Type eventType, object payload, int listeners)
        {
            Raised?.Invoke(new RaiseInfo(
                eventType,
                payload,
                listeners,
                UnityEngine.Time.frameCount,
                UnityEngine.Time.realtimeSinceStartupAsDouble));
        }
#endif
    }
}
