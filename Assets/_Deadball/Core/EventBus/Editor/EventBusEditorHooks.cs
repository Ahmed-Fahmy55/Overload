using UnityEditor;

namespace Core.Events.Editor
{
    /// <summary>
    /// Installs the editor-side configuration of the event bus system.
    /// </summary>
    /// <remarks>
    /// Substitutes the TypeCache-backed discovery strategy, and clears all buses when play mode
    /// ends. The latter is required because the buses are static: with domain reload disabled,
    /// bindings from one play session would otherwise remain registered in the next.
    /// </remarks>
    // UDR0001 asks for a [RuntimeInitializeOnLoadMethod] reset of static state. It does not apply
    // here: this type exists only in the editor assembly and its subscription is re-established by
    // [InitializeOnLoad] on every domain reload.
#pragma warning disable UDR0001
    [InitializeOnLoad]
    static class EventBusEditorHooks
    {
        static EventBusEditorHooks()
        {
            EventBusUtil.TypeProvider = new TypeCacheEventTypeProvider();

            EditorApplication.playModeStateChanged -= OnPlayModeStateChanged;
            EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
        }

        /// <summary>The most recent play mode transition observed by the editor.</summary>
        public static PlayModeStateChange PlayModeState { get; private set; }

        static void OnPlayModeStateChanged(PlayModeStateChange state)
        {
            PlayModeState = state;

            if (state == PlayModeStateChange.ExitingPlayMode)
                EventBusUtil.ClearAllBuses();
        }
    }
#pragma warning restore UDR0001
}
