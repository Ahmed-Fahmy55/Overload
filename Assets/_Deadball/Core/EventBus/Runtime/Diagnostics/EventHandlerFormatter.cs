using System;

namespace Core.Events
{
    /// <summary>
    /// Formats handler delegates for diagnostic output.
    /// </summary>
    public static class EventHandlerFormatter
    {
        /// <summary>
        /// Describes <paramref name="handler"/> as "Owner.Method". Handlers owned by a
        /// <see cref="UnityEngine.Object"/> are additionally qualified with the object name, since
        /// several instances of the same component commonly subscribe to one event.
        /// </summary>
        public static string Describe(Delegate handler)
        {
            if (handler == null) return "<null>";

            var owner = handler.Target?.GetType() ?? handler.Method.DeclaringType;
            var ownerName = owner?.Name ?? "<static>";

            if (handler.Target is UnityEngine.Object unityOwner && unityOwner)
                ownerName = $"{unityOwner.name} ({ownerName})";

            return $"{ownerName}.{handler.Method.Name}";
        }
    }
}
