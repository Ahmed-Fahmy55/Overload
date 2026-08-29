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
    /// A one-line level control that steps with left and right.
    /// </summary>
    /// <remarks>
    /// A drag-along slider would need a pointer, and the pointer is locked away (12). This reads as
    /// a bar but is driven entirely by the same left and right the option rows use, so the settings
    /// screen works on a pad without a second input model.
    /// </remarks>
    [RequireComponent(typeof(TextMeshProUGUI))]
    public class MenuSliderRow : Selectable, IMoveHandler
    {
        [Serializable] public class LevelEvent : UnityEvent<float> { }

        [Title("Label")]
        [SerializeField] string _title = "MASTER";

        [Title("Range")]
        [PropertyRange(0f, 1f), SerializeField] float _value = 1f;
        [MinValue(1), SerializeField] int _steps = 10;

        [Title("Look")]
        [SerializeField] string _filled = "▮";
        [SerializeField] string _empty = "▯";

        [Title("Output")]
        public LevelEvent onValueChanged = new();

        [Title("Runtime"), ShowInInspector, ReadOnly]
        public float Value => _value;

        TextMeshProUGUI _label;
        bool _focused;

        protected override void Awake()
        {
            base.Awake();
            _label = GetComponent<TextMeshProUGUI>();
            targetGraphic = _label;
            Refresh();
        }

        /// <summary>Sets the level without raising the event, for showing what was saved.</summary>
        public void SetValueSilently(float value)
        {
            _value = Mathf.Clamp01(value);
            Refresh();
        }

        public void OnMove(AxisEventData eventData)
        {
            switch (eventData.moveDir)
            {
                case MoveDirection.Left:
                    Step(-1);
                    break;

                case MoveDirection.Right:
                    Step(1);
                    break;

                default:
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
            // Clamped rather than wrapped: rolling from silent round to full blast would be a
            // nasty surprise on a volume control.
            float step = 1f / _steps;
            float next = Mathf.Clamp01(Mathf.Round((_value + direction * step) * _steps) / _steps);

            if (Mathf.Approximately(next, _value)) return;

            _value = next;
            Refresh();
            onValueChanged.Invoke(_value);
        }

        void Refresh()
        {
            if (_label == null) _label = GetComponent<TextMeshProUGUI>();
            if (_label == null) return;

            int filled = Mathf.RoundToInt(_value * _steps);
            var bar = new System.Text.StringBuilder();
            for (int i = 0; i < _steps; i++) bar.Append(i < filled ? _filled : _empty);

            string arrows = _focused ? "‹  " : "   ";
            string tail = _focused ? "  ›" : "   ";
            _label.text = $"{_title}   {arrows}{bar}{tail}  {Mathf.RoundToInt(_value * 100f)}%";
        }
    }
}
