namespace Core.Events
{
    /// <summary>
    /// Marker interface for event payloads carried by <see cref="EventBus{T}"/>.
    /// </summary>
    /// <remarks>
    /// Implementations are normally small immutable structs. Every concrete implementation is
    /// discovered by <see cref="IEventTypeProvider"/> and given its own bus.
    /// </remarks>
    public interface IEvent { }
}
