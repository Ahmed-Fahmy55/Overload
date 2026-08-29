using System;
using Sirenix.OdinInspector;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.Events;
using UnityEngine.UI;

namespace Deadball.HUD
{
    /// <summary>
    /// A one-line option picker that steps with left and right.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Replaces Heat's horizontal selector on the setup screens. Heat builds its arrow
    /// <see cref="Selectable"/>s at runtime, so at author time there is nothing for a navigation
    /// chain to point at - which left the arena and difficulty pickers unreachable by pad or
    /// keyboard, on a game whose whole premise is that no mouse is involved (12).
    /// </para>
    /// <para>
    /// It is a Selectable in its own right, so it takes its place in the vertical chain like any
    /// button, and answers left and right while it holds focus.
    /// </para>
    /// </remarks>
    [RequireComponent(typeof(TextMeshProUGUI))]
    public class MenuSelectorRow : Selectable, IMoveHandler
    {
        [Serializable] public class IndexEvent : UnityEvent<int> { }

        [Title("Options")]
        [SerializeField] string[] _options = { "SECTOR 9", "THE SPINE" };

        [Title("Look")]
        [Tooltip("Wrapped around the value while this row holds focus, so the player can see what "
            + "left and right will act on.")]
        [SerializeField] string _focusPrefix = "‹  ";

        [SerializeField] string _focusSuffix = "  ›";

        [Title("Output")]
        public IndexEvent onValueChanged = new();

        [Title("Runtime"), ShowInInspector, ReadOnly]
        public int Index { get; private set; }

        TextMeshProUGUI _label;
        bool _focused;

        protected override void Awake()
        {
            base.Awake();
            _label = GetComponent<TextMeshProUGUI>();
            targetGraphic = _label;
            Refresh();
        }

        /// <summary>Sets the row without raising the change event, for restoring saved state.</summary>
        public void SetIndexSilently(int index)
        {
            if (_options == null || _options.Length == 0) return;

            Index = Mathf.Clamp(index, 0, _options.Length - 1);
            Refresh();
        }

        public void OnMove(AxisEventData eventData)
        {
            if (_options == null || _options.Length == 0)
            {
                base.OnMove(eventData);
                return;
            }

            switch (eventData.moveDir)
            {
                case MoveDirection.Left:
                    Step(-1);
                    break;

                case MoveDirection.Right:
                    Step(1);
                    break;

                default:
                    // Up and down still belong to the chain, so they fall through untouched.
                    base.OnMove(eventData);
                    break;
            }
        }

        public override void OnSelect(BaseEventData eventData)
        {
            base.OnSelect(eventData);
            _focused = true;
            Refresh();
        }

        public override void OnDeselect(BaseEventData eventData)
        {
            base.OnDeselect(eventData);
            _focused = false;
            Refresh();
        }

        void Step(int direction)
        {
            // Wraps, so a player holding one direction never hits a dead end they cannot see.
            Index = (Index + direction + _options.Length) % _options.Length;

            Refresh();
            onValueChanged.Invoke(Index);
        }

        void Refresh()
        {
            if (_label == null) _label = GetComponent<TextMeshProUGUI>();
            if (_label == null || _options == null || _options.Length == 0) return;

            string value = _options[Mathf.Clamp(Index, 0, _options.Length - 1)];
            _label.text = _focused ? _focusPrefix + value + _focusSuffix : value;
        }
    }
}
