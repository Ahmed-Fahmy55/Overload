using System;
using System.Collections.Generic;

namespace Core.Events
{
    /// <summary>
    /// A type-agnostic description of a binding, used by tooling that cannot resolve the generic
    /// argument of <see cref="IEventBinding{T}"/>.
    /// </summary>
    public interface IEventBindingDescriptor
    {
        /// <summary>
        /// The handlers registered on this binding, in both supported shapes. Internal placeholder
        /// delegates are excluded.
        /// </summary>
        IReadOnlyList<Delegate> Handlers { get; }
    }
}
