using Deadball.Match;
using Deadball.Presentation;
using Sirenix.OdinInspector;
using UnityEngine;
using UnityEngine.UI;

namespace Deadball.HUD
{
    /// <summary>
    /// The screen-edge alarm for the last seconds of a round.
    /// </summary>
    /// <remarks>
    /// <para>
    /// A full-screen tell has to mean something that is true for both players at once, and the
    /// clock is the only thing that is. This used to fire on core heat, which is the opposite: heat
    /// is a property of one core that one runner is usually carrying, so painting the whole screen
    /// for it told the player who was nowhere near it to worry about nothing. Heat keeps the tells
    /// that belong to it - the core's own colour, the audio hum, and the danger triangles over
    /// whoever is holding it.
    /// </para>
    /// <para>
    /// The middle 60% of the screen stays untinted so the deck is never read through red, and the
    /// pulse breathes rather than holding steady - a constant tint is accepted by the eye within a
    /// second or two and stops carrying information.
    /// </para>
    /// </remarks>
    public class LowTimeAlarm : MonoBehaviour
    {
        [Title("Scene References")]
        [Required, SerializeField] RoundManager _rounds;

        [Tooltip("Optional. Runs the containment alarm bed alongside the vignette.")]
        [SerializeField] OverloadAudioDirector _audio;

        [Title("Widgets")]
        [Required, SerializeField] Image _vignette;
        [SerializeField] Image _topBar;
        [SerializeField] Image _bottomBar;

        [Title("Trigger")]
        [Tooltip("Matches the clock's own urgent threshold, so the screen and the digits turn "
            + "together rather than a beat apart.")]
        [SuffixLabel("s", true), MinValue(0f), SerializeField] float _threshold = 10f;

        [Title("Pulse")]
        [PropertyRange(0f, 1f), SerializeField] float _minAlpha = 0.35f;
        [PropertyRange(0f, 1f), SerializeField] float _maxAlpha = 0.55f;
        [SuffixLabel("s", true), MinValue(0.05f), SerializeField] float _period = 1.1f;
        [SuffixLabel("s", true), MinValue(0f), SerializeField] float _fadeOut = 0.35f;

        [Title("Runtime"), ShowInInspector, ReadOnly]
        public bool IsAlarming { get; private set; }

        float _phase;
        float _alpha;

        void OnEnable()
        {
            IsAlarming = false;
            _alpha = 0f;
            _phase = 0f;
            Apply(0f);
        }

        void OnDisable() => SetAudio(false);

        void Update()
        {
            bool alarming = ShouldAlarm();

            if (alarming != IsAlarming)
            {
                IsAlarming = alarming;
                if (!alarming) _phase = 0f;
                SetAudio(alarming);
            }

            if (IsAlarming)
            {
                _phase += Time.unscaledDeltaTime / Mathf.Max(0.05f, _period);

                // Cosine rather than a triangle: the ease at both ends is what makes it read as
                // breathing instead of as a strobe.
                float t = 0.5f - 0.5f * Mathf.Cos(_phase * Mathf.PI * 2f);
                _alpha = Mathf.Lerp(_minAlpha, _maxAlpha, t);
            }
            else
            {
                if (_alpha <= 0.001f) { Apply(0f); return; }

                _alpha = _fadeOut <= 0f
                    ? 0f
                    : Mathf.MoveTowards(_alpha, 0f, Time.unscaledDeltaTime / _fadeOut);
            }

            Apply(_alpha);
        }

        /// <summary>
        /// True for the closing seconds of a live round.
        /// </summary>
        /// <remarks>
        /// Overtime is excluded deliberately. It has no clock to run out, so an alarm there would
        /// never stop - and sudden death already announces itself.
        /// </remarks>
        bool ShouldAlarm()
        {
            if (_rounds == null || !_rounds.IsRoundActive || _rounds.IsOvertime) return false;

            float remaining = _rounds.TimeRemaining;
            return remaining > 0f && remaining <= _threshold;
        }

        void SetAudio(bool on)
        {
            if (_audio != null) _audio.SetAlarm(on);
        }

        void Apply(float alpha)
        {
            Tint(_vignette, alpha);
            Tint(_topBar, alpha);
            Tint(_bottomBar, alpha);
        }

        static void Tint(Image image, float alpha)
        {
            if (image == null) return;

            bool visible = alpha > 0.001f;
            if (image.enabled != visible) image.enabled = visible;

            Color c = image.color;
            c.a = alpha;
            image.color = c;
        }
    }
}
