using Core.Events;
using Deadball.Config;
using Deadball.Events;
using Deadball.Fighters;
using Sirenix.OdinInspector;
using UnityEngine;
using UnityEngine.UI;

namespace Deadball.Presentation
{
    /// <summary>
    /// Screen-edge flash in the holder's colour on possession change (GDD section 22).
    /// </summary>
    /// <remarks>
    /// Possession flips every few seconds and it is the single most important thing to know at any
    /// moment. The core's tint already carries it, but the core is small and the eye may be
    /// elsewhere - a flash at the edge of the frame catches peripheral vision without adding
    /// anything to the HUD, which section 21 keeps deliberately bare.
    /// </remarks>
    public class ScreenEdgeFlash : MonoBehaviour
    {
        [Title("Scene References")]
        [Required, SerializeField] Image _vignette;
        [Required, SerializeField] FighterPalette _palette;

        [Title("Flash")]
        [SuffixLabel("s", true), MinValue(0.01f), SerializeField] float _duration = 0.35f;
        [PropertyRange(0f, 1f), SerializeField] float _peakAlpha = 0.5f;

        [Tooltip("A perfect clamp is a bigger moment than walking onto a loose core.")]
        [PropertyRange(1f, 3f), SerializeField] float _clampMultiplier = 1.6f;

        EventBinding<BallCaught> _caught;
        EventBinding<BallPossessionChanged> _possession;

        float _remaining;
        float _strength = 1f;
        Color _colour = Color.white;

        void OnEnable()
        {
            // Clamps are handled through their own event so the tier can scale the flash; a plain
            // walk-over pickup comes through possession instead.
            _caught = new EventBinding<BallCaught>(OnClamped);
            _possession = new EventBinding<BallPossessionChanged>(OnPossession);

            EventBus<BallCaught>.Register(_caught);
            EventBus<BallPossessionChanged>.Register(_possession);

            Hide();
        }

        void OnDisable()
        {
            EventBus<BallCaught>.Deregister(_caught);
            EventBus<BallPossessionChanged>.Deregister(_possession);
        }

        void LateUpdate()
        {
            if (_remaining <= 0f) return;

            _remaining -= Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(_remaining / _duration);

            if (_vignette != null)
            {
                _vignette.enabled = true;
                _vignette.color = new Color(_colour.r, _colour.g, _colour.b, _peakAlpha * _strength * t);
            }

            if (_remaining <= 0f) Hide();
        }

        void OnClamped(BallCaught evt)
        {
            // A late clamp gives no possession, so it gets no possession flash.
            if (evt.Tier != ClampTier.Perfect) return;

            Flash(evt.CatcherSlot, _clampMultiplier);
        }

        void OnPossession(BallPossessionChanged evt)
        {
            if (evt.HolderSlot < 0 || evt.WasCaught) return;

            Flash(evt.HolderSlot, 1f);
        }

        void Flash(int slot, float strength)
        {
            _colour = _palette.BodyColour(slot);
            _strength = strength;
            _remaining = _duration;
        }

        void Hide()
        {
            _remaining = 0f;
            if (_vignette != null) _vignette.enabled = false;
        }
    }
}
