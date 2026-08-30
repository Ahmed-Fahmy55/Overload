using Core.Events;
using Deadball.Events;
using Sirenix.OdinInspector;
using UnityEngine;
using UnityEngine.UI;

namespace Deadball.HUD
{
    /// <summary>
    /// The screen-edge alarm while the core is critical.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Heat has no bar anywhere in the game - by design it lives in the core's colour and in the
    /// audio. That works right up until it becomes lethal, at which point the player needs to know
    /// without looking away from the core. This is that tell, and it is deliberately at the edges:
    /// the middle 60% of the screen stays untinted so the deck itself is never read through red.
    /// </para>
    /// <para>
    /// It breathes rather than holding steady. A constant tint is accepted by the eye within a
    /// second or two and stops being information; a slow pulse keeps announcing itself.
    /// </para>
    /// </remarks>
    public class CriticalHeatVignette : MonoBehaviour
    {
        [Title("Widgets")]
        [Required, SerializeField] Image _vignette;
        [SerializeField] Image _topBar;
        [SerializeField] Image _bottomBar;

        [Title("Pulse")]
        [PropertyRange(0f, 1f), SerializeField] float _minAlpha = 0.35f;
        [PropertyRange(0f, 1f), SerializeField] float _maxAlpha = 0.55f;
        [SuffixLabel("s", true), MinValue(0.05f), SerializeField] float _period = 1.1f;
        [SuffixLabel("s", true), MinValue(0f), SerializeField] float _fadeOut = 0.35f;

        [Title("Runtime"), ShowInInspector, ReadOnly]
        public bool IsCritical { get; private set; }

        EventBinding<CriticalStateChanged> _critical;
        EventBinding<RoundStarting> _roundStarting;
        float _phase;
        float _alpha;

        void OnEnable()
        {
            _critical = new EventBinding<CriticalStateChanged>(OnCritical);

            // Heat is zeroed for a fresh round, and the alarm has to go with it even if the round
            // ended while the deck was still hot.
            _roundStarting = new EventBinding<RoundStarting>(() => IsCritical = false);

            EventBus<CriticalStateChanged>.Register(_critical);
            EventBus<RoundStarting>.Register(_roundStarting);

            IsCritical = false;
            _alpha = 0f;
            Apply(0f);
        }

        void OnDisable()
        {
            EventBus<CriticalStateChanged>.Deregister(_critical);
            EventBus<RoundStarting>.Deregister(_roundStarting);
        }

        void OnCritical(CriticalStateChanged evt) => IsCritical = evt.IsCritical;

        void Update()
        {
            if (IsCritical)
            {
                _phase += Time.unscaledDeltaTime / Mathf.Max(0.05f, _period);

                // Cosine rather than a triangle: the ease at both ends is what makes it read as
                // breathing instead of as a strobe.
                float t = 0.5f - 0.5f * Mathf.Cos(_phase * Mathf.PI * 2f);
                _alpha = Mathf.Lerp(_minAlpha, _maxAlpha, t);
            }
            else
            {
                _phase = 0f;

                if (_alpha <= 0.001f) { Apply(0f); return; }

                _alpha = _fadeOut <= 0f
                    ? 0f
                    : Mathf.MoveTowards(_alpha, 0f, Time.unscaledDeltaTime / _fadeOut);
            }

            Apply(_alpha);
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

            if (image.enabled != alpha > 0.001f) image.enabled = alpha > 0.001f;

            Color c = image.color;
            c.a = alpha;
            image.color = c;
        }
    }
}
