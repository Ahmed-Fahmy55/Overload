using System;
using System.Collections.Generic;

namespace Core.Events
{
    /// <summary>
    /// A type-agnostic handle to a single <see cref="EventBus{T}"/>.
    /// </summary>
    /// <remarks>
    /// Lifecycle code and tooling depend on this interface instead of reflecting over the private
    /// members of the bus, so renaming or restructuring the bus cannot silently break them.
    /// </remarks>
    public interface IEventBusHandle
    {
        /// <summary>The event type this bus carries.</summary>
        Type EventType { get; }

        /// <summary>The number of bindings currently registered on the bus.</summary>
        int BindingCount { get; }

        /// <summary>The registered bindings, described without their generic argument.</summary>
        IReadOnlyList<IEventBindingDescriptor> Bindings { get; }

        /// <summary>Removes every binding from the bus.</summary>
        void Clear();
    }
}
