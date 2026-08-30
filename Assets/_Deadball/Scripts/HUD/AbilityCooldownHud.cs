using Deadball.Ball;
using Deadball.Fighters;
using TMPro;
using Sirenix.OdinInspector;
using UnityEngine;
using UnityEngine.UI;

namespace Deadball.HUD
{
    /// <summary>
    /// Dash and claim cooldowns for one player, at the bottom of the HUD.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Both abilities are gated on a timer the player could not see. The dodge had no tell at all,
    /// and the claim lockout shared the bar above the runner with the charge - so the one thing a
    /// locked-out player most needs to know was the thing they had to decode a colour to read.
    /// These are the two icons that answer "can I do it right now".
    /// </para>
    /// <para>
    /// Radial fills rather than bars: a sweep that closes is read without measuring, and it does
    /// not stretch its own artwork the way a scaled fill does.
    /// </para>
    /// </remarks>
    public class AbilityCooldownHud : MonoBehaviour
    {
        [Title("Player")]
        [Tooltip("Which player these icons belong to.")]
        [SerializeField] int _slot;

        [Title("References")]
        [Required, SerializeField] CanvasGroup _group;
        [Required, SerializeField] Image _dashFill;
        [Required, SerializeField] Image _claimFill;

        [Tooltip("Tile art, dimmed while the ability is recovering.")]
        [SerializeField] Image _dashArt;
        [SerializeField] Image _claimArt;

        [Tooltip("Flashes when the ability comes back.")]
        [SerializeField] TMP_Text _dashReadyLabel;
        [SerializeField] TMP_Text _claimReadyLabel;

        [Title("Colours")]
        [SerializeField] Color _ready = new(0.16f, 0.85f, 1f);
        [SerializeField] Color _cooling = new(0.35f, 0.42f, 0.48f);

        [Title("Ready Flash")]
        [Tooltip("How many times the READY label blinks when the ability returns.")]
        [MinValue(0), SerializeField] int _flashes = 3;
        [SuffixLabel("s", true), MinValue(0.05f), SerializeField] float _flashPeriod = 0.6f;

        [PropertyRange(0f, 1f), SerializeField] float _coolingArtAlpha = 0.4f;

        [Title("Runtime"), ShowInInspector, ReadOnly]
        public float DashReady { get; private set; } = 1f;

        [ShowInInspector, ReadOnly]
        public float ClaimReady { get; private set; } = 1f;

        Fighter _fighter;
        float _dashFlash;
        float _claimFlash;
        bool _dashWasReady = true;
        bool _claimWasReady = true;

        void LateUpdate()
        {
            // Runners are spawned when their device joins, so there is nothing to wire at author
            // time and the panel stays hidden until its player actually exists.
            if (_fighter == null) _fighter = BallTargetRegistry.Find(_slot) as Fighter;

            if (_fighter == null)
            {
                if (_group != null) _group.alpha = 0f;
                return;
            }

            if (_group != null) _group.alpha = 1f;

            DashReady = _fighter.Motor != null ? _fighter.Motor.DodgeReady01 : 1f;
            ClaimReady = _fighter.Catcher != null ? _fighter.Catcher.ClaimReady01 : 1f;

            // The moment of return is the thing worth announcing - a sweep that quietly completes
            // is easy to miss when your eyes are on the deck.
            _dashFlash = Recovered(DashReady, ref _dashWasReady) ? _flashes * _flashPeriod
                : Mathf.Max(0f, _dashFlash - Time.unscaledDeltaTime);
            _claimFlash = Recovered(ClaimReady, ref _claimWasReady) ? _flashes * _flashPeriod
                : Mathf.Max(0f, _claimFlash - Time.unscaledDeltaTime);

            Drive(_dashFill, _dashArt, _dashReadyLabel, DashReady, _dashFlash);
            Drive(_claimFill, _claimArt, _claimReadyLabel, ClaimReady, _claimFlash);
        }

        static bool Recovered(float ready01, ref bool wasReady)
        {
            bool now = ready01 >= 0.999f;
            bool crossed = now && !wasReady;
            wasReady = now;
            return crossed;
        }

        void Drive(Image fill, Image art, TMP_Text readyLabel, float ready01, float flashLeft)
        {
            bool ready = ready01 >= 0.999f;

            if (fill != null)
            {
                fill.fillAmount = Mathf.Clamp01(ready01);
                fill.color = ready ? _ready : _cooling;
            }

            if (art != null)
            {
                Color c = art.color;
                c.a = ready ? 1f : _coolingArtAlpha;
                art.color = c;
            }

            if (readyLabel == null) return;

            // Blinks for its window, then goes out entirely - it is a transition tell, not a state.
            bool blinkOn = flashLeft > 0f
                && Mathf.Repeat(flashLeft, _flashPeriod) > _flashPeriod * 0.5f;

            if (readyLabel.enabled != blinkOn) readyLabel.enabled = blinkOn;
            readyLabel.color = _ready;
        }
    }
}
