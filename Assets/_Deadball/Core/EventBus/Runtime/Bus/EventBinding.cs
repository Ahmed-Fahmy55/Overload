using System;
using System.Collections.Generic;
using System.Text;

namespace Core.Events
{
    /// <summary>
    /// A subscription to <see cref="EventBus{T}"/>.
    /// </summary>
    /// <typeparam name="T">The event type this binding subscribes to.</typeparam>
    /// <remarks>
    /// A binding may carry handlers that accept the event payload and handlers that accept nothing.
    /// Both shapes are invoked on every raise, so the choice of handler signature does not affect
    /// whether an event is received.
    /// </remarks>
    public sealed class EventBinding<T> : IEventBinding<T>, IEventBindingDescriptor where T : IEvent
    {
        // Placeholder delegates keep both invocation lists non-null for the lifetime of the binding,
        // which removes the null check from the dispatch path and prevents a NullReferenceException
        // after the last user handler is removed.
        static readonly Action<T> NoOp = _ => { };
        static readonly Action NoOpNoArgs = () => { };

        Action<T> _onEvent = NoOp;
        Action _onEventNoArgs = NoOpNoArgs;

        /// <summary>Creates a binding whose handler receives the event payload.</summary>
        public EventBinding(Action<T> onEvent) => Add(onEvent);

        /// <summary>Creates a binding whose handler is notified without the event payload.</summary>
        public EventBinding(Action onEventNoArgs) => Add(onEventNoArgs);

        Action<T> IEventBinding<T>.OnEvent
        {
            get => _onEvent;
            set => _onEvent = value ?? NoOp;
        }

        Action IEventBinding<T>.OnEventNoArgs
        {
            get => _onEventNoArgs;
            set => _onEventNoArgs = value ?? NoOpNoArgs;
        }

        /// <summary>Adds a handler that receives the event payload.</summary>
        public void Add(Action<T> onEvent)
        {
            if (onEvent != null) _onEvent += onEvent;
        }

        /// <summary>Removes a previously added payload handler.</summary>
        public void Remove(Action<T> onEvent)
        {
            if (onEvent != null) _onEvent = (_onEvent - onEvent) ?? NoOp;
        }

        /// <summary>Adds a handler that is notified without the event payload.</summary>
        public void Add(Action onEvent)
        {
            if (onEvent != null) _onEventNoArgs += onEvent;
        }

        /// <summary>Removes a previously added no-argument handler.</summary>
        public void Remove(Action onEvent)
        {
            if (onEvent != null) _onEventNoArgs = (_onEventNoArgs - onEvent) ?? NoOpNoArgs;
        }

        IReadOnlyList<Delegate> IEventBindingDescriptor.Handlers
        {
            get
            {
                var handlers = new List<Delegate>();
                CollectUserHandlers(_onEvent, handlers);
                CollectUserHandlers(_onEventNoArgs, handlers);
                return handlers;
            }
        }

        public override string ToString()
        {
            var handlers = ((IEventBindingDescriptor)this).Handlers;
            if (handlers.Count == 0) return $"EventBinding<{typeof(T).Name}> (no handlers)";

            var text = new StringBuilder($"EventBinding<{typeof(T).Name}>: ");
            for (int i = 0; i < handlers.Count; i++)
            {
                if (i > 0) text.Append(", ");
                text.Append(EventHandlerFormatter.Describe(handlers[i]));
            }

            return text.ToString();
        }

        /// <summary>
        /// Appends the user handlers of <paramref name="root"/> to <paramref name="into"/>, omitting
        /// the placeholder delegates.
        /// </summary>
        static void CollectUserHandlers(Delegate root, List<Delegate> into)
        {
            if (root == null) return;

            foreach (var handler in root.GetInvocationList())
            {
                // GetInvocationList returns new Delegate instances, so the placeholders must be
                // matched by value rather than by reference.
                if (NoOp.Equals(handler) || NoOpNoArgs.Equals(handler)) continue;

                into.Add(handler);
            }
        }
    }
}
