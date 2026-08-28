using Sirenix.OdinInspector;
using UnityEngine;

namespace Deadball.AI
{
    /// <summary>
    /// One difficulty tier (OVERLOAD GDD section 13.3).
    /// </summary>
    /// <remarks>
    /// <para>
    /// The design is emphatic: one AI, three tiers, do not build three AIs. Everything here is a
    /// number fed into the same state machine, so a tier is an asset rather than a code path.
    /// </para>
    /// <para>
    /// <c>ClampChance</c> is the headline float the doc names. The rest exist because "attempts a
    /// clamp" is not the same as "lands one" - when it presses decides which tier it gets, and that
    /// is what separates a bot that clamps from a bot that clamps <em>well</em>.
    /// </para>
    /// </remarks>
    [CreateAssetMenu(menuName = "Overload/AI Profile", fileName = "AiProfile")]
    public class AiProfile : ScriptableObject
    {
        [Title("Identity")]
        [SerializeField] string _displayName = "OPERATOR";

        [Title("Clamp", "13.3 - the one float that defines a tier")]
        [PropertyRange(0f, 1f)]
        [Tooltip("Probability the AI attempts a clamp instead of dodging.")]
        [SerializeField] float _clampChance = 0.45f;

        [SuffixLabel("s", true), PropertyRange(0.02f, 0.35f)]
        [Tooltip("Seconds-to-arrival it aims to press at. Under the perfect band lands PERFECT.")]
        [SerializeField] float _clampTargetArrival = 0.09f;

        [SuffixLabel("s", true), PropertyRange(0f, 0.25f)]
        [Tooltip("Sloppiness on that timing. Wide jitter turns perfect clamps into late ones.")]
        [SerializeField] float _clampTimingJitter = 0.06f;

        [Title("Throwing")]
        [PropertyRange(0f, 1f), LabelText("Charge Target")]
        [SerializeField] float _chargeTarget = 0.7f;

        [PropertyRange(0f, 0.5f), LabelText("Charge Jitter")]
        [SerializeField] float _chargeJitter = 0.15f;

        [SuffixLabel("deg", true), PropertyRange(0f, 20f)]
        [Tooltip("13.4 - so the top tier is not literally unmissable.")]
        [SerializeField] float _aimErrorDegrees = 5f;

        [SuffixLabel("m", true), MinValue(1f)]
        [Tooltip("Range it tries to hold before launching.")]
        [SerializeField] float _preferredRange = 8f;

        [Title("Feel", "13.4 - a frame-perfect bot feels like cheating even when it is fair")]
        [MinMaxSlider(0.05f, 0.6f, true), LabelText("Reaction Delay")]
        [Tooltip("How long the runner will chase before throwing from wherever it stands. "
            + "A holder moves at 80% speed, so it can never close on a fleeing opponent - without "
            + "this cap it chases forever and never lets go of the core.")]
        [SuffixLabel("s", true), MinValue(0.2f), SerializeField] float _maxCloseSeconds = 1.5f;

        [SerializeField] Vector2 _reactionDelay = new(0.15f, 0.25f);

        [SuffixLabel("s", true), MinValue(0.1f), LabelText("Dodge Interval")]
        [Tooltip("Jittered timer for evasive dashes. It should never stand still.")]
        [SerializeField] Vector2 _dodgeInterval = new(0.8f, 1.8f);

        [Title("Heat Awareness", "13.5 - one if, and the bot looks like it understands the stakes")]
        [PropertyRange(0f, 1f), LabelText("Critical Clamp Multiplier")]
        [Tooltip("Clamp chance is scaled by this while the core is CRITICAL. Below 1 = more cautious.")]
        [SerializeField] float _criticalClampMultiplier = 0.5f;

        [PropertyRange(0f, 1f), LabelText("Critical Charge Multiplier")]
        [Tooltip("Throws sooner when the core is critical rather than holding a lethal object.")]
        [SerializeField] float _criticalChargeMultiplier = 0.6f;

        public string DisplayName => _displayName;
        public float ClampChance => _clampChance;
        public float ClampTargetArrival => _clampTargetArrival;
        public float ClampTimingJitter => _clampTimingJitter;
        public float ChargeJitter => _chargeJitter;
        public float AimErrorDegrees => _aimErrorDegrees;
        public float PreferredRange => _preferredRange;
        public float MaxCloseSeconds => _maxCloseSeconds;

        public float NextReactionDelay() => Random.Range(_reactionDelay.x, _reactionDelay.y);
        public float NextDodgeInterval() => Random.Range(_dodgeInterval.x, _dodgeInterval.y);

        /// <summary>Clamp chance, made more cautious while the core is critical (13.5).</summary>
        public float ClampChanceFor(bool coreIsCritical) =>
            Mathf.Clamp01(coreIsCritical ? _clampChance * _criticalClampMultiplier : _clampChance);

        /// <summary>Charge it will hold to before launching. Lower when the core is critical.</summary>
        public float ChargeTargetFor(bool coreIsCritical)
        {
            float target = coreIsCritical ? _chargeTarget * _criticalChargeMultiplier : _chargeTarget;
            return Mathf.Clamp01(target + Random.Range(-_chargeJitter, _chargeJitter));
        }

        /// <summary>Seconds-to-arrival at which it will press clamp on this attempt.</summary>
        public float NextClampPressWindow() =>
            Mathf.Max(0.01f, _clampTargetArrival + Random.Range(-_clampTimingJitter, _clampTimingJitter));
    }
}
