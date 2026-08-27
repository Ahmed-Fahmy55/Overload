using System;
using System.Collections.Generic;
using UnityEditor;

namespace Core.Events.Editor
{
    /// <summary>
    /// Maintains a rolling log of every event raised on the bus, for display by
    /// <see cref="EventBusDebuggerWindow"/>.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Recording begins with the editor rather than with the window, so that the log already covers
    /// the events that preceded opening it. Disabling <see cref="Recording"/> clears
    /// <see cref="EventBusDiagnostics.Enabled"/>, after which the bus performs no reporting work.
    /// </para>
    /// <para>
    /// Entries are held in a circular buffer. Some events are raised every frame, so an unbounded
    /// log would grow without limit over a long session.
    /// </para>
    /// </remarks>
    // UDR0001 asks for a [RuntimeInitializeOnLoadMethod] reset of static state. It does not apply
    // here: this type exists only in the editor assembly and its subscriptions are re-established by
    // [InitializeOnLoad] on every domain reload.
#pragma warning disable UDR0001
    [InitializeOnLoad]
    public static class EventBusRecorder
    {
        /// <summary>One recorded raise.</summary>
        public readonly struct Entry
        {
            public readonly Type EventType;
            public readonly object Payload;
            public readonly int Listeners;
            public readonly int Frame;
            public readonly double Time;

            public Entry(in EventBusDiagnostics.RaiseInfo info)
            {
                EventType = info.EventType;
                Payload = info.Payload;
                Listeners = info.Listeners;
                Frame = info.Frame;
                Time = info.Time;
            }

            /// <summary>
            /// Whether the event reached no subscriber. Usually indicates a listener that registered
            /// after the raise, or one that was never registered.
            /// </summary>
            public bool Unheard => Listeners == 0;
        }

        const string RecordingPrefsKey = "Core.EventBus.Recorder.Recording";
        const int DefaultCapacity = 1000;

        static Entry[] s_buffer = new Entry[DefaultCapacity];
        static int s_head;
        static int s_count;

        static EventBusRecorder()
        {
            EventBusDiagnostics.Raised -= OnRaised;
            EventBusDiagnostics.Raised += OnRaised;

            EditorApplication.playModeStateChanged -= OnPlayModeStateChanged;
            EditorApplication.playModeStateChanged += OnPlayModeStateChanged;

            Recording = EditorPrefs.GetBool(RecordingPrefsKey, true);
        }

        /// <summary>
        /// Incremented on every change to the log. A view can compare this against the value it last
        /// drew and repaint once per editor tick instead of once per recorded event.
        /// </summary>
        public static int Version { get; private set; }

        /// <summary>The number of entries currently held.</summary>
        public static int Count => s_count;

        /// <summary>
        /// Whether events are being recorded. Persisted across sessions and mirrored to
        /// <see cref="EventBusDiagnostics.Enabled"/>.
        /// </summary>
        public static bool Recording
        {
            get => EventBusDiagnostics.Enabled;
            set
            {
                if (EventBusDiagnostics.Enabled == value) return;

                EventBusDiagnostics.Enabled = value;
                EditorPrefs.SetBool(RecordingPrefsKey, value);
                Version++;
            }
        }

        /// <summary>
        /// The maximum number of entries retained. Resizing preserves the most recent entries that
        /// still fit.
        /// </summary>
        public static int Capacity
        {
            get => s_buffer.Length;
            set
            {
                int capacity = Math.Max(1, value);
                if (capacity == s_buffer.Length) return;

                var retained = new List<Entry>(Entries);
                s_buffer = new Entry[capacity];
                s_head = 0;
                s_count = 0;

                int discard = Math.Max(0, retained.Count - capacity);
                for (int i = discard; i < retained.Count; i++)
                    Append(retained[i]);

                Version++;
            }
        }

        /// <summary>The recorded entries, oldest first.</summary>
        public static IEnumerable<Entry> Entries
        {
            get
            {
                int start = s_head - s_count;
                if (start < 0) start += s_buffer.Length;

                for (int i = 0; i < s_count; i++)
                    yield return s_buffer[(start + i) % s_buffer.Length];
            }
        }

        /// <summary>Discards every recorded entry.</summary>
        public static void Clear()
        {
            Array.Clear(s_buffer, 0, s_buffer.Length);
            s_head = 0;
            s_count = 0;
            Version++;
        }

        static void OnRaised(EventBusDiagnostics.RaiseInfo info)
        {
            Append(new Entry(info));
            Version++;
        }

        static void Append(in Entry entry)
        {
            s_buffer[s_head] = entry;
            s_head = (s_head + 1) % s_buffer.Length;

            if (s_count < s_buffer.Length) s_count++;
        }

        static void OnPlayModeStateChanged(PlayModeStateChange state)
        {
            // Entries from a previous session would be misleading alongside the new run.
            if (state == PlayModeStateChange.ExitingEditMode) Clear();
        }
    }
#pragma warning restore UDR0001
}
