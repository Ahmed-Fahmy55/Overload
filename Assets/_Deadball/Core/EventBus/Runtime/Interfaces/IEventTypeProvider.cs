using System;
using System.Collections.Generic;

namespace Core.Events
{
    /// <summary>
    /// Supplies the set of concrete <see cref="IEvent"/> implementations declared in the project.
    /// </summary>
    /// <remarks>
    /// Runtime and editor code discover types by different means, so consumers depend on this
    /// abstraction rather than on a particular discovery strategy.
    /// </remarks>
    public interface IEventTypeProvider
    {
        /// <summary>
        /// Returns every concrete event type. Abstract types, interfaces and open generic
        /// definitions are excluded, since no bus can be created for them.
        /// </summary>
        IReadOnlyList<Type> GetEventTypes();
    }
}
