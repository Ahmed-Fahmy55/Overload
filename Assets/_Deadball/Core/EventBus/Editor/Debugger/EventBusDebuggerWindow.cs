using System;
using System.Collections;
using System.Reflection;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace Core.Events.Editor
{
    /// <summary>
    /// Inspects the event bus at runtime through two views.
    /// <para>
    /// <b>Traffic</b> is a live log of every raise, sourced from <see cref="EventBusRecorder"/>.
    /// <b>Listeners</b> is the current subscriber list, sourced from <see cref="EventBusRegistry"/>.
    /// Both read published abstractions rather than reflecting into the bus, so neither view needs
    /// updating when new event types are declared.
    /// </para>
    /// </summary>
    public class EventBusDebuggerWindow : EditorWindow
    {
        enum Tab
        {
            Traffic,
            Listeners
        }

        static readonly string[] TabNames = { "Traffic", "Listeners" };
        static readonly Color UnheardTint = new(1f, 0.6f, 0.6f);
        static readonly IEventTypeProvider TypeProvider = new TypeCacheEventTypeProvider();

        Tab _tab;
        string _filter = string.Empty;
        Vector2 _trafficScroll;
        Vector2 _listenerScroll;
        bool _followTail = true;
        bool _onlyUnheard;
        bool _showIdleBuses;
        int _lastDrawnVersion = -1;

        [MenuItem("Tools/EventBus Debugger")]
        static void OpenWindow() => GetWindow<EventBusDebuggerWindow>("EventBus").Show();

        void OnEnable()
        {
            EditorApplication.update -= OnEditorUpdate;
            EditorApplication.update += OnEditorUpdate;
        }

        // Block bodies rather than expression bodies: Unity's domain reload analyzer inspects
        // method bodies for the matching unsubscription and faults on expression-bodied members.
        void OnDisable()
        {
            EditorApplication.update -= OnEditorUpdate;
        }

        void OnDestroy()
        {
            EditorApplication.update -= OnEditorUpdate;
        }

        /// <summary>
        /// Drives repaints. The traffic view repaints only when the recorder reports new entries,
        /// which coalesces the per-frame event volume into a single repaint per editor tick.
        /// </summary>
        void OnEditorUpdate()
        {
            if (_tab == Tab.Listeners)
            {
                Repaint();
                return;
            }

            if (_lastDrawnVersion == EventBusRecorder.Version) return;

            _lastDrawnVersion = EventBusRecorder.Version;
            Repaint();
        }

        void OnGUI()
        {
            DrawToolbar();

            if (_tab == Tab.Traffic) DrawTraffic();
            else DrawListeners();
        }

        void DrawToolbar()
        {
            using (new EditorGUILayout.HorizontalScope(EditorStyles.toolbar))
            {
                _tab = (Tab)GUILayout.Toolbar((int)_tab, TabNames, EditorStyles.toolbarButton, GUILayout.Width(140));

                if (_tab == Tab.Traffic) DrawTrafficControls();
                else DrawListenerControls();

                GUILayout.FlexibleSpace();

                _filter = GUILayout.TextField(_filter, EditorStyles.toolbarSearchField, GUILayout.MinWidth(120));
            }
        }

        void DrawTrafficControls()
        {
            if (GUILayout.Button("Clear", EditorStyles.toolbarButton, GUILayout.Width(50)))
                EventBusRecorder.Clear();

            EventBusRecorder.Recording = GUILayout.Toggle(
                EventBusRecorder.Recording, "Record", EditorStyles.toolbarButton, GUILayout.Width(60));

            _followTail = GUILayout.Toggle(
                _followTail, "Follow", EditorStyles.toolbarButton, GUILayout.Width(60));

            // Isolates raises that reached no subscriber, the usual symptom of a listener that
            // registered too late or never registered at all.
            _onlyUnheard = GUILayout.Toggle(
                _onlyUnheard, "Unheard only", EditorStyles.toolbarButton, GUILayout.Width(90));

            GUILayout.Label($"{EventBusRecorder.Count}", EditorStyles.miniLabel, GUILayout.Width(45));
        }

        void DrawListenerControls()
        {
            if (GUILayout.Button("Clear all buses", EditorStyles.toolbarButton, GUILayout.Width(100)))
                EventBusUtil.ClearAllBuses();

            _showIdleBuses = GUILayout.Toggle(
                _showIdleBuses, "Show idle", EditorStyles.toolbarButton, GUILayout.Width(70));
        }

        void DrawTraffic()
        {
            if (EventBusRecorder.Count == 0)
            {
                EditorGUILayout.HelpBox(
                    Application.isPlaying
                        ? "Nothing raised yet. Every call to EventBus<T>.Raise appears here."
                        : "Enter play mode to record events.",
                    MessageType.Info);
                return;
            }

            using var scope = new EditorGUILayout.ScrollViewScope(_trafficScroll);

            foreach (var entry in EventBusRecorder.Entries)
            {
                if (_onlyUnheard && !entry.Unheard) continue;
                if (!MatchesFilter(entry.EventType)) continue;

                DrawTrafficRow(entry);
            }

            _trafficScroll = _followTail && Event.current.type == EventType.Repaint
                ? new Vector2(scope.scrollPosition.x, float.MaxValue)
                : scope.scrollPosition;
        }

        static void DrawTrafficRow(in EventBusRecorder.Entry entry)
        {
            Color previous = GUI.color;
            if (entry.Unheard) GUI.color = UnheardTint;

            using (new EditorGUILayout.HorizontalScope())
            {
                GUILayout.Label($"{entry.Time:0.00}", EditorStyles.miniLabel, GUILayout.Width(60));
                GUILayout.Label($"f{entry.Frame}", EditorStyles.miniLabel, GUILayout.Width(60));
                GUILayout.Label(entry.EventType.Name, EditorStyles.boldLabel, GUILayout.Width(200));
                GUILayout.Label(
                    entry.Unheard ? "0 listeners" : $"{entry.Listeners}",
                    EditorStyles.miniLabel, GUILayout.Width(75));
                GUILayout.Label(Describe(entry.Payload), EditorStyles.miniLabel);
            }

            GUI.color = previous;
        }

        void DrawListeners()
        {
            // A bus is created on first use, so idle event types have no handle until warmed.
            var eventTypes = TypeProvider.GetEventTypes();
            if (EventBusRegistry.Count < eventTypes.Count)
                EventBusUtil.WarmBuses(TypeProvider);

            using var scope = new EditorGUILayout.ScrollViewScope(_listenerScroll);
            _listenerScroll = scope.scrollPosition;

            int shown = 0;
            foreach (var bus in EventBusRegistry.Buses)
            {
                if (!MatchesFilter(bus.EventType)) continue;
                if (!_showIdleBuses && bus.BindingCount == 0) continue;

                shown++;
                DrawBus(bus);
            }

            if (shown == 0)
            {
                EditorGUILayout.HelpBox(
                    _showIdleBuses
                        ? "No event types matched the filter."
                        : "Nothing is subscribed. Enable Show idle to list every event type.",
                    MessageType.Info);
            }
        }

        static void DrawBus(IEventBusHandle bus)
        {
            Color previous = GUI.color;
            if (bus.BindingCount == 0) GUI.color = UnheardTint;

            using (new EditorGUILayout.HorizontalScope())
            {
                GUILayout.Label(bus.EventType.Name, EditorStyles.boldLabel, GUILayout.Width(240));
                GUILayout.Label(
                    bus.BindingCount == 0 ? "no listeners" : $"{bus.BindingCount} binding(s)",
                    EditorStyles.miniLabel);
            }

            GUI.color = previous;

            using (new EditorGUI.IndentLevelScope())
            {
                foreach (var binding in bus.Bindings)
                    foreach (var handler in binding.Handlers)
                        DrawHandler(handler);
            }
        }

        static void DrawHandler(Delegate handler)
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                EditorGUILayout.LabelField(EventHandlerFormatter.Describe(handler), EditorStyles.miniLabel);

                if (handler.Target is UnityEngine.Object owner && owner &&
                    GUILayout.Button("Ping", EditorStyles.miniButton, GUILayout.Width(40)))
                {
                    EditorGUIUtility.PingObject(owner);
                }
            }
        }

        bool MatchesFilter(Type eventType)
        {
            return string.IsNullOrEmpty(_filter) ||
                   eventType.Name.IndexOf(_filter, StringComparison.OrdinalIgnoreCase) >= 0;
        }

        /// <summary>
        /// Formats the public fields of an event as "Field=Value" so the log reports the payload and
        /// not merely the event type.
        /// </summary>
        /// <remarks>
        /// Performed at draw time rather than at record time: most entries are never displayed, and
        /// formatting on record would place reflection in the path of per-frame events.
        /// </remarks>
        static string Describe(object payload)
        {
            if (payload == null) return string.Empty;

            FieldInfo[] fields = payload.GetType().GetFields(BindingFlags.Public | BindingFlags.Instance);
            if (fields.Length == 0) return string.Empty;

            var text = new StringBuilder();
            for (int i = 0; i < fields.Length; i++)
            {
                if (i > 0) text.Append(", ");
                text.Append(fields[i].Name).Append('=').Append(Format(fields[i].GetValue(payload)));
            }

            return text.ToString();
        }

        static string Format(object value)
        {
            if (value == null) return "null";

            // Collections are reduced to their length to keep rows on a single line.
            if (value is Array array) return $"[{array.Length}]";
            if (value is ICollection collection) return $"[{collection.Count}]";

            return value.ToString();
        }
    }
}
