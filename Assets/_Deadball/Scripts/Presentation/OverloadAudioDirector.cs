using Ami.BroAudio;
using Core.Events;
using Deadball.Events;
using Sirenix.OdinInspector;
using UnityEngine;

namespace Deadball.Presentation
{
    /// <summary>
    /// The five sounds that carry the game, plus the heat hum (OVERLOAD GDD section 19).
    /// </summary>
    /// <remarks>
    /// <para>
    /// Everything here is driven by events the gameplay already raises. The design is explicit that
    /// feedback must never live inside gameplay logic (20), so the rules publish facts - a charge
    /// started, a core was clamped, a runner derezzed - and this listens. No gameplay class knows
    /// that audio exists.
    /// </para>
    /// <para>
    /// Playback goes through BroAudio rather than raw AudioSources, which is what gives us pooling,
    /// per-category buses and the ducking used on the knock.
    /// </para>
    /// </remarks>
    public class OverloadAudioDirector : MonoBehaviour
    {
        [Title("Cues", "19 - the five sounds")]
        [Tooltip("Rising loop while charging. Pitch tracks charge.")]
        [SerializeField] SoundID _spinUp;

        [Tooltip("Plasma whoosh on launch. Pitch from charge.")]
        [SerializeField] SoundID _launch;

        [Tooltip("The clamp cue. Short, unmistakable, mixed above everything.")]
        [SerializeField] SoundID _flashCueAlarm;


        [Tooltip("Bass layer under the clamp, played together with the core thunk.")]
        [SerializeField] SoundID _clamp;

        [SerializeField] SoundID _knock;

        [Tooltip("Power-down whine into near-silence on a KO.")]
        [SerializeField] SoundID _derez;

        [Title("Section 22")]
        [Tooltip("The click as the ring slams shut at full charge. One clip, kept dry and short.")]
        [SerializeField] SoundID _ringSnap;

        [Tooltip("Containment field hit as the core rebounds. Pitch rises with impact speed.")]
        [SerializeField] SoundID _bounce;

        [Title("Music", "One track per arena - a match is played on one deck (15.3)")]
        [Tooltip("Set per arena scene. Arena 02 is a different scene, so it carries its own track.")]
        [SerializeField] SoundID _music;

        [Title("Beds", "16.3 - heat is a HUD element disguised as ambience")]
        [SerializeField] SoundID _heatHum;
        [SerializeField] SoundID _criticalAlarm;
        [SerializeField] SoundID _deckRumble;

        [Title("Mix")]
        [PropertyRange(0f, 3f), SerializeField] float _clampVolume = 1.6f;
        [PropertyRange(0f, 2f), SerializeField] float _alarmVolume = 1.2f;

        [Title("Launch Pitch")]
        [SerializeField] float _minLaunchPitch = 0.85f;
        [SerializeField] float _maxLaunchPitch = 1.35f;

        [Title("Bounce Pitch")]
        [Tooltip("A soft rebound versus a max-charge slam, from the same clip.")]
        [SerializeField] float _minBouncePitch = 0.9f;
        [SerializeField] float _maxBouncePitch = 1.3f;

        [Title("Heat Hum Pitch")]
        [Tooltip("Hum pitch at zero heat and at critical. The rise is the tell.")]
        [SerializeField] float _minHumPitch = 0.8f;
        [SerializeField] float _maxHumPitch = 1.6f;

        [Title("Ducking")]
        [Tooltip("How far everything else drops under a knock.")]
        [PropertyRange(0f, 1f), SerializeField] float _knockDuckVolume = 0.35f;
        [SuffixLabel("Hz", true), SerializeField] float _knockDuckCutoff = 700f;
        [SuffixLabel("s", true), SerializeField] float _knockDuckFade = 0.15f;

        [Title("Runtime"), ShowInInspector, ReadOnly]
        public float Heat01 { get; private set; }

        [ShowInInspector, ReadOnly]
        public bool IsCritical { get; private set; }

        EventBinding<ChargeStarted> _chargeStarted;
        EventBinding<ChargeCancelled> _chargeCancelled;
        EventBinding<BallThrown> _thrown;
        EventBinding<BallFlashCue> _flash;
        EventBinding<BallCaught> _caught;
        EventBinding<FighterKnocked> _knocked;
        EventBinding<FighterKnockedOut> _knockedOut;
        EventBinding<BallPossessionChanged> _possession;
        EventBinding<RoundStarted> _roundStarted;
        EventBinding<RoundEnded> _roundEnded;
        EventBinding<MatchEnded> _matchEnded;
        EventBinding<ChargeMaxed> _chargeMaxed;
        EventBinding<BallBounced> _bounced;

        IAudioPlayer _humPlayer;

        void OnEnable()
        {
            _chargeStarted = new EventBinding<ChargeStarted>(() => Play(_spinUp));
            _chargeCancelled = new EventBinding<ChargeCancelled>(StopSpinUp);
            _thrown = new EventBinding<BallThrown>(OnThrown);
            _flash = new EventBinding<BallFlashCue>(OnFlashCue);
            _caught = new EventBinding<BallCaught>(OnClamped);
            _knocked = new EventBinding<FighterKnocked>(OnKnocked);
            _knockedOut = new EventBinding<FighterKnockedOut>(OnKnockedOut);

            // A holder can lose the core mid-charge without ever cancelling - knocked out, or the
            // round reset under them. Possession changing is the reliable signal to kill the loop.
            _possession = new EventBinding<BallPossessionChanged>(StopSpinUp);
            _roundStarted = new EventBinding<RoundStarted>(StartBeds);
            _roundEnded = new EventBinding<RoundEnded>(StopSpinUp);
            _matchEnded = new EventBinding<MatchEnded>(StopBeds);

            EventBus<ChargeStarted>.Register(_chargeStarted);
            EventBus<ChargeCancelled>.Register(_chargeCancelled);
            EventBus<BallThrown>.Register(_thrown);
            EventBus<BallFlashCue>.Register(_flash);
            EventBus<BallCaught>.Register(_caught);
            EventBus<FighterKnocked>.Register(_knocked);
            EventBus<FighterKnockedOut>.Register(_knockedOut);
            EventBus<BallPossessionChanged>.Register(_possession);
            EventBus<RoundStarted>.Register(_roundStarted);
            EventBus<RoundEnded>.Register(_roundEnded);
            EventBus<MatchEnded>.Register(_matchEnded);

            _chargeMaxed = new EventBinding<ChargeMaxed>(() => Play(_ringSnap));
            _bounced = new EventBinding<BallBounced>(OnBounced);

            EventBus<ChargeMaxed>.Register(_chargeMaxed);
            EventBus<BallBounced>.Register(_bounced);
        }

        void OnDisable()
        {
            EventBus<ChargeStarted>.Deregister(_chargeStarted);
            EventBus<ChargeCancelled>.Deregister(_chargeCancelled);
            EventBus<BallThrown>.Deregister(_thrown);
            EventBus<BallFlashCue>.Deregister(_flash);
            EventBus<BallCaught>.Deregister(_caught);
            EventBus<FighterKnocked>.Deregister(_knocked);
            EventBus<FighterKnockedOut>.Deregister(_knockedOut);
            EventBus<BallPossessionChanged>.Deregister(_possession);
            EventBus<RoundStarted>.Deregister(_roundStarted);
            EventBus<RoundEnded>.Deregister(_roundEnded);
            EventBus<MatchEnded>.Deregister(_matchEnded);
            EventBus<ChargeMaxed>.Deregister(_chargeMaxed);
            EventBus<BallBounced>.Deregister(_bounced);

            // Stop is safe during teardown; Play is not.
            StopBeds();
        }

        /// <summary>
        /// The containment field taking a hit. One clip, pitched by how hard the core struck.
        /// </summary>
        /// <remarks>
        /// Pitch does the work that layering would otherwise do: a glancing rebound and a
        /// max-charge slam are the same sample, and they still read as different impacts.
        /// </remarks>
        void OnBounced(BallBounced evt)
        {
            IAudioPlayer player = PlayFor(_bounce);
            if (player == null) return;

            float hardness = Mathf.InverseLerp(4f, 22f, evt.Speed);
            player.SetPitch(Mathf.Lerp(_minBouncePitch, _maxBouncePitch, hardness));
        }

        /// <summary>
        /// Drives the heat bed. Call from Rally Heat once it exists (16).
        /// </summary>
        /// <param name="heat01">Normalised heat, 0 to 1, where 1 is critical.</param>
        public void SetHeat(float heat01)
        {
            Heat01 = Mathf.Clamp01(heat01);

            if (_humPlayer is { IsPlaying: true })
                _humPlayer.SetPitch(Mathf.Lerp(_minHumPitch, _maxHumPitch, Heat01), fadeTime: 0.25f);
        }

        /// <summary>
        /// Starts or stops the containment alarm bed.
        /// </summary>
        /// <remarks>
        /// Driven by the round clock running out rather than by core heat. A bed that fills the
        /// room has to mean something true for both players, and heat is one core's business -
        /// heat keeps the hum, whose pitch already tracks it.
        /// </remarks>
        public void SetAlarm(bool on)
        {
            if (on == IsCritical) return;

            IsCritical = on;

            if (on) Play(_criticalAlarm);
            else BroAudio.Stop(_criticalAlarm, fadeOut: 0.4f);
        }

        void OnThrown(BallThrown evt)
        {
            StopSpinUp();

            if (!_launch.IsValid()) return;

            BroAudio.Play(_launch).SetPitch(Mathf.Lerp(_minLaunchPitch, _maxLaunchPitch, evt.Charge01));
        }

        void OnFlashCue(BallFlashCue evt)
        {
            if (!_flashCueAlarm.IsValid()) return;

            // Mixed above everything else: this is the beat the player presses on.
            BroAudio.Play(_flashCueAlarm).SetVolume(_alarmVolume);
        }

        void OnClamped(BallCaught evt)
        {
            if (_clamp.IsValid()) BroAudio.Play(_clamp).SetVolume(_clampVolume);
        }

        void OnKnocked(FighterKnocked evt) => PlayKnock();

        void OnKnockedOut(FighterKnockedOut evt)
        {
            PlayKnock();

            if (_derez.IsValid()) BroAudio.Play(_derez).SetVolume(1.1f);
        }

        void PlayKnock()
        {
            if (!_knock.IsValid()) return;

            // "Meaty, with a brief low-pass duck on everything else" (19).
            BroAudio.Play(_knock)
                .AsDominator()
                .LowPassOthers(_knockDuckCutoff, _knockDuckFade)
                .QuietOthers(_knockDuckVolume, _knockDuckFade);
        }

        void StartBeds()
        {
            // Guarded so the track rides through round changes rather than restarting on each one -
            // a match is played on one deck, so it gets one continuous piece of music.
            if (_music.IsValid() && !BroAudio.HasAnyPlayingInstances(_music))
                BroAudio.Play(_music, fadeIn: 2f).AsBGM().SetTransition(Transition.CrossFade);

            if (_deckRumble.IsValid() && !BroAudio.HasAnyPlayingInstances(_deckRumble))
                BroAudio.Play(_deckRumble, fadeIn: 1.5f);

            if (!_heatHum.IsValid() || BroAudio.HasAnyPlayingInstances(_heatHum)) return;

            _humPlayer = BroAudio.Play(_heatHum, fadeIn: 0.8f);
            SetHeat(Heat01);
        }

        void StopBeds()
        {
            BroAudio.Stop(_heatHum, fadeOut: 0.5f);
            BroAudio.Stop(_criticalAlarm, fadeOut: 0.5f);
            BroAudio.Stop(_deckRumble, fadeOut: 1f);
            BroAudio.Stop(_music, fadeOut: 1.5f);
            _humPlayer = null;
            IsCritical = false;
        }

        void StopSpinUp() => BroAudio.Stop(_spinUp, fadeOut: 0.05f);

        static void Play(SoundID id)
        {
            if (id.IsValid()) BroAudio.Play(id);
        }

        /// <summary>Plays a cue and hands back the player so the caller can pitch it.</summary>
        static IAudioPlayer PlayFor(SoundID id) => id.IsValid() ? BroAudio.Play(id) : null;
    }
}
