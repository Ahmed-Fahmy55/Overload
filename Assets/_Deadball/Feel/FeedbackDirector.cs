using Core.Events;
using Deadball.Events;
using Deadball.Fighters;
using MoreMountains.Feedbacks;
using UnityEngine;

namespace Deadball.Feel
{
    /// <summary>
    /// Turns gameplay events into Feel feedbacks (OVERLOAD GDD section 20).
    /// </summary>
    /// <remarks>
    /// <para>
    /// The design's rule is "never put feedback logic inside gameplay logic", and this is the seam
    /// that keeps that true: gameplay raises a fact, this plays a player, and the actual feel is
    /// authored in the inspector where it can be tuned at 2am without recompiling.
    /// </para>
    /// <para>
    /// It lives outside the Deadball assembly on purpose. Feel ships no assembly definition, so its
    /// types are in Assembly-CSharp, which an asmdef assembly cannot reference. Putting the bridge
    /// here - in a folder with no asmdef - is cheaper and safer than adding an asmdef to a vendor
    /// package that would have to be re-added after every update.
    /// </para>
    /// </remarks>
    public class FeedbackDirector : MonoBehaviour
    {
        [Header("Players (section 20)")]
        [Tooltip("Charge begins: rising loop, scale pulse, light ramp.")]
        public MMF_Player SpinUp;

        [Tooltip("Launch: shake, squash on the thrower, whoosh, vent burst.")]
        public MMF_Player Launch;

        [Tooltip("The clamp cue, 0.35s before arrival.")]
        public MMF_Player Alarm;

        [Tooltip("Perfect clamp. Disproportionately loud - this is the trailer shot (8.6).")]
        public MMF_Player ClampPerfect;

        [Tooltip("Late clamp. Softer: no freeze, small shake.")]
        public MMF_Player ClampLate;

        public MMF_Player Knock;

        [Tooltip("KO: slow-mo, zoom, vignette, derez, blackout.")]
        public MMF_Player KO;

        [Header("Section 22")]
        [Tooltip("Containment field flare where the core rebounds. Moved to the contact point.")]
        public MMF_Player Bounce;

        [Tooltip("The ring slamming shut at full charge: a click and a small kick.")]
        public MMF_Player ChargeMax;

        [Tooltip("Vent puff under a dodging runner. Moved to the runner.")]
        public MMF_Player Dodge;

        EventBinding<ChargeStarted> _chargeStarted;
        EventBinding<BallThrown> _thrown;
        EventBinding<BallFlashCue> _flash;
        EventBinding<BallCaught> _caught;
        EventBinding<FighterKnocked> _knocked;
        EventBinding<FighterKnockedOut> _knockedOut;
        EventBinding<BallBounced> _bounced;
        EventBinding<ChargeMaxed> _chargeMaxed;
        EventBinding<FighterDodged> _dodged;

        void OnEnable()
        {
            _chargeStarted = new EventBinding<ChargeStarted>(() => Play(SpinUp));
            _thrown = new EventBinding<BallThrown>(() => Play(Launch));
            _flash = new EventBinding<BallFlashCue>(() => Play(Alarm));
            _caught = new EventBinding<BallCaught>(OnClamped);
            _knocked = new EventBinding<FighterKnocked>(() => Play(Knock));
            _knockedOut = new EventBinding<FighterKnockedOut>(() => Play(KO));

            EventBus<ChargeStarted>.Register(_chargeStarted);
            EventBus<BallThrown>.Register(_thrown);
            EventBus<BallFlashCue>.Register(_flash);
            EventBus<BallCaught>.Register(_caught);
            EventBus<FighterKnocked>.Register(_knocked);
            EventBus<FighterKnockedOut>.Register(_knockedOut);

            _bounced = new EventBinding<BallBounced>(OnBounced);
            _chargeMaxed = new EventBinding<ChargeMaxed>(() => Play(ChargeMax));
            _dodged = new EventBinding<FighterDodged>(OnDodged);

            EventBus<BallBounced>.Register(_bounced);
            EventBus<ChargeMaxed>.Register(_chargeMaxed);
            EventBus<FighterDodged>.Register(_dodged);
        }

        void OnDisable()
        {
            EventBus<ChargeStarted>.Deregister(_chargeStarted);
            EventBus<BallThrown>.Deregister(_thrown);
            EventBus<BallFlashCue>.Deregister(_flash);
            EventBus<BallCaught>.Deregister(_caught);
            EventBus<FighterKnocked>.Deregister(_knocked);
            EventBus<FighterKnockedOut>.Deregister(_knockedOut);
            EventBus<BallBounced>.Deregister(_bounced);
            EventBus<ChargeMaxed>.Deregister(_chargeMaxed);
            EventBus<FighterDodged>.Deregister(_dodged);
        }

        void OnClamped(BallCaught evt) =>
            Play(evt.Tier == ClampTier.Perfect ? ClampPerfect : ClampLate);

        /// <summary>Flares the containment field where the core actually struck it.</summary>
        /// <remarks>
        /// The player is moved onto the contact point and turned to face along the surface normal,
        /// so a particle burst fires away from the wall rather than into it.
        /// </remarks>
        void OnBounced(BallBounced evt)
        {
            if (Bounce == null) return;

            Bounce.transform.position = evt.Position;
            if (evt.Normal.sqrMagnitude > 0.001f)
                Bounce.transform.rotation = Quaternion.LookRotation(evt.Normal);

            Play(Bounce);
        }

        void OnDodged(FighterDodged evt)
        {
            if (Dodge == null) return;

            Dodge.transform.position = evt.Position;
            Play(Dodge);
        }

        static void Play(MMF_Player player)
        {
            if (player != null) player.PlayFeedbacks();
        }
    }
}
