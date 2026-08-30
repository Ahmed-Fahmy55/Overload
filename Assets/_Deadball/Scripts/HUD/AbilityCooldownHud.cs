using Deadball.Ball;
using Deadball.Fighters;
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

        [Title("Colours")]
        [SerializeField] Color _ready = new(0.16f, 0.85f, 1f);
        [SerializeField] Color _cooling = new(0.35f, 0.38f, 0.45f);

        [Title("Runtime"), ShowInInspector, ReadOnly]
        public float DashReady { get; private set; } = 1f;

        [ShowInInspector, ReadOnly]
        public float ClaimReady { get; private set; } = 1f;

        Fighter _fighter;

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

            Drive(_dashFill, DashReady);
            Drive(_claimFill, ClaimReady);
        }

        void Drive(Image fill, float ready01)
        {
            if (fill == null) return;

            fill.fillAmount = Mathf.Clamp01(ready01);
            fill.color = ready01 >= 0.999f ? _ready : _cooling;
        }
    }
}
