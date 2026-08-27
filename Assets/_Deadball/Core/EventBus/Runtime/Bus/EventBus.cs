using System;
using System.Collections.Generic;
using UnityEngine;

namespace Core.Events
{
    /// <summary>
    /// A static, type-safe publish/subscribe channel for a single event type.
    /// </summary>
    /// <typeparam name="T">The event carried by this bus.</typeparam>
    /// <remarks>
    /// <para>
    /// <b>Dispatch.</b> <see cref="Raise(T)"/> is the only way to publish. It invokes both handler
    /// shapes on every binding, so a subscriber may declare its handler with or without the payload
    /// parameter without any risk of missing an event.
    /// </para>
    /// <para>
    /// <b>Re-entrancy.</b> Dispatch iterates a copy-on-write snapshot, so handlers may register and
    /// deregister bindings while an event is being delivered. A binding removed during dispatch is
    /// not invoked; a binding added during dispatch is invoked from the next raise onward.
    /// </para>
    /// <para>
    /// <b>Isolation.</b> An exception thrown by one handler is logged and does not prevent the
    /// remaining handlers from running.
    /// </para>
    /// </remarks>
    public static class EventBus<T> where T : IEvent
    {
        static readonly HashSet<IEventBinding<T>> s_bindings = new();
        static IEventBinding<T>[] s_snapshot = Array.Empty<IEventBinding<T>>();
        static bool s_dirty;

        static EventBus() => EventBusRegistry.Register(new Handle());

        /// <summary>The number of bindings currently registered.</summary>
        public static int BindingCount => s_bindings.Count;

        /// <summary>Subscribes <paramref name="binding"/>. Registering the same binding twice has no effect.</summary>
        public static void Register(EventBinding<T> binding)
        {
            if (binding == null)
            {
                Debug.LogError($"[EventBus<{typeof(T).Name}>] Cannot register a null binding.");
                return;
            }

            if (s_bindings.Add(binding))
                s_dirty = true;
        }

        /// <summary>Unsubscribes <paramref name="binding"/>. Unknown or null bindings are ignored.</summary>
        public static void Deregister(EventBinding<T> binding)
        {
            if (binding != null && s_bindings.Remove(binding))
                s_dirty = true;
        }

        /// <summary>
        /// Delivers <paramref name="eventData"/> to every registered binding, invoking both its
        /// payload handlers and its no-argument handlers.
        /// </summary>
        public static void Raise(T eventData)
        {
#if UNITY_EDITOR
            // Reported before dispatch so that a raise whose handler throws is still recorded.
            if (EventBusDiagnostics.Enabled)
                EventBusDiagnostics.ReportRaise(typeof(T), eventData, s_bindings.Count);
#endif

            var snapshot = Snapshot();
            for (int i = 0; i < snapshot.Length; i++)
            {
                var binding = snapshot[i];

                // Honours bindings removed by a handler earlier in this same dispatch.
                if (!s_bindings.Contains(binding)) continue;

                try
                {
                    binding.OnEvent.Invoke(eventData);
                    binding.OnEventNoArgs.Invoke();
                }
                catch (Exception exception)
                {
                    Debug.LogError($"[EventBus<{typeof(T).Name}>] A handler threw while processing the event.");
                    Debug.LogException(exception);
                }
            }
        }

        /// <summary>Returns the current bindings as a snapshot. Intended for diagnostics.</summary>
        public static IReadOnlyList<IEventBinding<T>> GetBindings() => Snapshot();

        /// <summary>Removes every binding from this bus.</summary>
        public static void Clear()
        {
            s_bindings.Clear();
            s_dirty = true;
        }

        /// <summary>
        /// Returns the dispatch snapshot, rebuilding it only when the binding set has changed.
        /// </summary>
        static IEventBinding<T>[] Snapshot()
        {
            if (s_dirty)
            {
                s_snapshot = new IEventBinding<T>[s_bindings.Count];
                s_bindings.CopyTo(s_snapshot);
                s_dirty = false;
            }

            return s_snapshot;
        }

        /// <summary>
        /// Exposes this bus to <see cref="EventBusRegistry"/> without requiring callers to resolve
        /// the generic argument.
        /// </summary>
        sealed class Handle : IEventBusHandle
        {
            public Type EventType => typeof(T);

            public int BindingCount => s_bindings.Count;

            public IReadOnlyList<IEventBindingDescriptor> Bindings
            {
                get
                {
                    var snapshot = Snapshot();
                    var descriptors = new List<IEventBindingDescriptor>(snapshot.Length);

                    for (int i = 0; i < snapshot.Length; i++)
                        if (snapshot[i] is IEventBindingDescriptor descriptor)
                            descriptors.Add(descriptor);

                    return descriptors;
                }
            }

            public void Clear() => EventBus<T>.Clear();
        }
    }
}
