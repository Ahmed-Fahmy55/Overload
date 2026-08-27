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

        EventBinding<ChargeStarted> _chargeStarted;
        EventBinding<BallThrown> _thrown;
        EventBinding<BallFlashCue> _flash;
        EventBinding<BallCaught> _caught;
        EventBinding<FighterKnocked> _knocked;
        EventBinding<FighterKnockedOut> _knockedOut;

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
        }

        void OnDisable()
        {
            EventBus<ChargeStarted>.Deregister(_chargeStarted);
            EventBus<BallThrown>.Deregister(_thrown);
            EventBus<BallFlashCue>.Deregister(_flash);
            EventBus<BallCaught>.Deregister(_caught);
            EventBus<FighterKnocked>.Deregister(_knocked);
            EventBus<FighterKnockedOut>.Deregister(_knockedOut);
        }

        void OnClamped(BallCaught evt) =>
            Play(evt.Tier == ClampTier.Perfect ? ClampPerfect : ClampLate);

        static void Play(MMF_Player player)
        {
            if (player != null) player.PlayFeedbacks();
        }
    }
}
