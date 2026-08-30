using Deadball.Match;
using Sirenix.OdinInspector;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Deadball.HUD
{
    /// <summary>
    /// Turns the round clock urgent for the last ten seconds.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The clock is the only thing on screen that can change the plan, and until it does it is
    /// furniture. Ten seconds out it stops being furniture: the digits go alarm red and grow, the
    /// rules under them thicken, and a FINAL 10 label blinks once per second so the change is
    /// caught in peripheral vision rather than needing to be looked at.
    /// </para>
    /// <para>
    /// It blinks on the integer second rather than on a free-running sine, so the flash lands with
    /// the digit change instead of drifting against it.
    /// </para>
    /// </remarks>
    public class ClockUrgency : MonoBehaviour
    {
        [Title("Scene References")]
        [Required, SerializeField] RoundManager _rounds;

        [Title("Widgets")]
        [Required, SerializeField] TMP_Text _digits;
        [SerializeField] TMP_Text _finalLabel;
        [SerializeField] Image _ruleAbove;
        [SerializeField] Image _ruleBelow;

        [Title("Look")]
        [SuffixLabel("s", true), MinValue(0f), SerializeField] float _threshold = 10f;
        [SerializeField] Color _calm = new(0.91f, 0.94f, 0.96f);
        [SerializeField] Color _urgent = new(1f, 0.18f, 0.18f);

        [MinValue(1f), SerializeField] float _calmSize = 150f;
        [MinValue(1f), SerializeField] float _urgentSize = 168f;
        [MinValue(0f), SerializeField] float _calmRule = 3f;
        [MinValue(0f), SerializeField] float _urgentRule = 6f;

        [Title("Runtime"), ShowInInspector, ReadOnly]
        public bool IsUrgent { get; private set; }

        void LateUpdate()
        {
            if (_rounds == null || _digits == null) return;

            // Overtime has no clock to run down, so there is nothing to be urgent about.
            float remaining = _rounds.TimeRemaining;
            bool urgent = !_rounds.IsOvertime && _rounds.IsRoundActive
                && remaining > 0f && remaining <= _threshold;

            if (urgent != IsUrgent) Apply(urgent);

            if (_finalLabel == null) return;

            // Blink on the integer second: the half of each second that the digit has just changed.
            _finalLabel.enabled = IsUrgent && Mathf.Repeat(remaining, 1f) > 0.5f;
        }

        void Apply(bool urgent)
        {
            IsUrgent = urgent;

            _digits.color = urgent ? _urgent : _calm;
            _digits.fontSize = urgent ? _urgentSize : _calmSize;

            float rule = urgent ? _urgentRule : _calmRule;
            Size(_ruleAbove, rule);
            Size(_ruleBelow, rule);

            if (_finalLabel == null) return;

            _finalLabel.color = _urgent;
            if (!urgent) _finalLabel.enabled = false;
        }

        static void Size(Image rule, float height)
        {
            if (rule == null) return;

            var rt = (RectTransform)rule.transform;
            rt.sizeDelta = new Vector2(rt.sizeDelta.x, height);
        }
    }
}
