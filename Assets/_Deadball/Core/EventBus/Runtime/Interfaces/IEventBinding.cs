using System;

namespace Core.Events
{
    /// <summary>
    /// The contract <see cref="EventBus{T}"/> invokes when dispatching an event.
    /// </summary>
    /// <typeparam name="T">The event type this binding subscribes to.</typeparam>
    public interface IEventBinding<T>
    {
        /// <summary>Handlers that receive the event payload.</summary>
        Action<T> OnEvent { get; set; }

        /// <summary>Handlers that are notified without receiving the payload.</summary>
        Action OnEventNoArgs { get; set; }
    }
}
