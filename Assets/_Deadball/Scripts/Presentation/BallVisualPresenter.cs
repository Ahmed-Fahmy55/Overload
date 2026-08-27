using Core.Events;
using Deadball.Ball;
using Deadball.Config;
using Deadball.Events;
using Sirenix.OdinInspector;
using UnityEngine;

namespace Deadball.Presentation
{
    /// <summary>
    /// Ball tint, trail and flash - telegraph layers 2 and 3 (GDD sections 8.2 and 11.2).
    /// </summary>
    /// <remarks>
    /// <para>
    /// Three separate readability jobs happen to share one renderer, so they share one presenter.
    /// The tint tells you who is holding it. The trail tells you how hard it was thrown, and
    /// therefore how much time you have. The flash tells you to press now.
    /// </para>
    /// <para>
    /// The ball must be the brightest object on screen at all times - it is the thing the player's
    /// eye is never allowed to lose (17).
    /// </para>
    /// </remarks>
    public class BallVisualPresenter : MonoBehaviour
    {
        [Title("References")]
        [Required, SerializeField] BallController _ball;
        [Required, SerializeField] Renderer _renderer;
        [Required, SerializeField] TrailRenderer _trail;
        [Required, SerializeField] FighterPalette _palette;

        [Tooltip("The plasma body of the core. Tinted with everything else so possession, heat and "
            + "the flash cue still read (17).")]
        [SerializeField] ParticleSystem[] _plasma;

        [Tooltip("Only alight while the core is CRITICAL. Off is the normal state (16.3).")]
        [SerializeField] GameObject _criticalAura;

        [Tooltip("Plasma shed along the flight path. Carries the same charge read as the line trail.")]
        [SerializeField] ParticleSystem _trailVfx;

        [Tooltip("Emission multiplier on the core. Kept low because bloom already flares it.")]
        [PropertyRange(0.5f, 3f), SerializeField] float _emissionBoost = 1.25f;

        [Title("Trail Plasma")]
        [MinValue(0f), SerializeField] float _minTrailRate = 40f;
        [MinValue(0f), SerializeField] float _maxTrailRate = 220f;
        [MinValue(0f), SerializeField] float _minTrailSize = 0.14f;
        [MinValue(0f), SerializeField] float _maxTrailSize = 0.4f;

        [Title("Trail")]
        [Tooltip("A soft lob is a thin white streak; a max-charge shot is a fat hot one (8.2).")]
        [SuffixLabel("m", true), SerializeField] float _minTrailWidth = 0.08f;
        [SuffixLabel("m", true), SerializeField] float _maxTrailWidth = 0.45f;
        [SuffixLabel("s", true), SerializeField] float _minTrailTime = 0.12f;
        [SuffixLabel("s", true), SerializeField] float _maxTrailTime = 0.35f;

        [Title("Flash")]
        [SuffixLabel("s", true), MinValue(0.01f), SerializeField] float _flashDuration = 0.12f;
        [SerializeField] Color _flashColour = Color.white;

        [Title("Heat Ramp", "16.3 - nobody needs a tutorial to read a white-hot core")]
        [SerializeField] Color _heatCold = new(0.75f, 0.9f, 1f);
        [SerializeField] Color _heatWarm = new(1f, 0.65f, 0.15f);
        [SerializeField] Color _heatCritical = Color.white;

        [Tooltip("Strobe rate at CRITICAL. The whole arena should feel wrong.")]
        [SuffixLabel("Hz", true), SerializeField] float _criticalStrobeSpeed = 9f;

        [PropertyRange(0f, 1f), SerializeField] float _criticalStrobeDepth = 0.45f;

        [Title("Loose Pulse")]
        [SuffixLabel("Hz", true), SerializeField] float _pulseSpeed = 2.2f;
        [PropertyRange(0f, 1f), SerializeField] float _pulseDepth = 0.35f;

        static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        static readonly int EmissionColorId = Shader.PropertyToID("_EmissionColor");

        MaterialPropertyBlock _block;
        EventBinding<BallThrown> _thrownBinding;
        float _flashRemaining;
        int _lastThrowerSlot;
        float _heat01;
        bool _isCritical;

        /// <summary>Drives the colour ramp. Called by the heat broadcaster (16.3).</summary>
        public void SetHeat(float heat01) => _heat01 = Mathf.Clamp01(heat01);

        /// <summary>Switches the core to its white-hot strobing state.</summary>
        public void SetCritical(bool critical)
        {
            _isCritical = critical;

            // Guarded so a repeated call does not restart the aura's own animation every frame.
            if (_criticalAura != null && _criticalAura.activeSelf != critical)
                _criticalAura.SetActive(critical);
        }

        void Awake()
        {
            _block = new MaterialPropertyBlock();

            // A round can start with the aura left on from the previous one.
            if (_criticalAura != null) _criticalAura.SetActive(false);
        }

        void OnEnable()
        {
            _ball.StateChanged += OnStateChanged;
            _ball.FlashCueFired += Flash;

            _thrownBinding = new EventBinding<BallThrown>(evt => _lastThrowerSlot = evt.ThrowerSlot);
            EventBus<BallThrown>.Register(_thrownBinding);
        }

        void OnDisable()
        {
            _ball.StateChanged -= OnStateChanged;
            _ball.FlashCueFired -= Flash;
            EventBus<BallThrown>.Deregister(_thrownBinding);
        }

        void LateUpdate()
        {
            if (_flashRemaining > 0f)
                _flashRemaining -= Time.unscaledDeltaTime;

            ApplyColour();
            ApplyTrail();
        }

        /// <summary>Punches the ball white. Used by the catch cue and by a successful catch.</summary>
        public void Flash() => _flashRemaining = _flashDuration;

        void OnStateChanged(BallState previous, BallState current)
        {
            if (current == BallState.Caught)
                Flash();

            bool flying = current == BallState.Flying;

            if (!flying)
                _trail.Clear();

            _trail.emitting = flying;

            // The plasma is the trail; the line underneath is now only its bright spine.
            if (_trailVfx != null)
            {
                if (flying) _trailVfx.Play();
                else _trailVfx.Stop(true, ParticleSystemStopBehavior.StopEmitting);
            }
        }

        void ApplyColour()
        {
            Color target = OwnerColour();

            if (_ball.State == BallState.Loose)
            {
                float pulse = 1f + Mathf.Sin(Time.time * _pulseSpeed * Mathf.PI * 2f) * _pulseDepth;
                target *= pulse;
            }

            if (_flashRemaining > 0f)
                target = Color.Lerp(target, _flashColour, _flashRemaining / _flashDuration);

            _renderer.GetPropertyBlock(_block);
            _block.SetColor(BaseColorId, target);
            // Bloom does most of the work now that post-processing is on; a 2x emission on top of
            // it inflated the core into a ball far larger than its 0.34m body.
            _block.SetColor(EmissionColorId, target * _emissionBoost);
            _renderer.SetPropertyBlock(_block);

            TintPlasma(target);
        }

        /// <summary>
        /// Pushes the core's current colour onto the plasma body.
        /// </summary>
        /// <remarks>
        /// Only newly emitted particles pick this up, which is exactly what is wanted: the plasma
        /// bleeds from one colour to the next over a few frames instead of snapping, while the solid
        /// centre and the trail change instantly. A short particle lifetime keeps that lag readable.
        /// </remarks>
        void TintPlasma(Color colour)
        {
            if (_plasma == null) return;

            foreach (ParticleSystem system in _plasma)
            {
                if (system == null) continue;

                ParticleSystem.MainModule main = system.main;
                main.startColor = colour;
            }
        }

        void ApplyTrail()
        {
            float charge = _ball.Charge01;
            float width = Mathf.Lerp(_minTrailWidth, _maxTrailWidth, charge);

            _trail.widthMultiplier = width;
            _trail.time = Mathf.Lerp(_minTrailTime, _maxTrailTime, charge);

            Color trailColour = OwnerColour();
            _trail.startColor = trailColour;
            _trail.endColor = new Color(trailColour.r, trailColour.g, trailColour.b, 0f);

            if (_trailVfx == null) return;

            // A soft lob sheds a thin wisp; a max-charge shot leaves a thick plasma wake (8.2).
            ParticleSystem.EmissionModule emission = _trailVfx.emission;
            emission.rateOverTime = Mathf.Lerp(_minTrailRate, _maxTrailRate, charge);

            ParticleSystem.MainModule trailMain = _trailVfx.main;
            trailMain.startSize = Mathf.Lerp(_minTrailSize, _maxTrailSize, charge);
            trailMain.startColor = trailColour;
        }

        /// <summary>
        /// While flying, the ball keeps the thrower's colour so the target can read where it came
        /// from at a glance; loose, it goes neutral so neither player reads it as theirs.
        /// </summary>
        Color OwnerColour()
        {
            // Heat overrides identity once the core is genuinely dangerous: at that point what the
            // core is about to do to you matters more than whose colour it is wearing (16.3).
            if (_isCritical)
            {
                float strobe = 1f + Mathf.Sin(Time.time * _criticalStrobeSpeed * Mathf.PI * 2f) * _criticalStrobeDepth;
                return _heatCritical * strobe;
            }

            int slot = _ball.HolderSlot;
            Color identity = slot >= 0
                ? _palette.BodyColour(slot)
                : _ball.State == BallState.Flying
                    ? _palette.TrailColour(_lastThrowerSlot)
                    : _palette.LooseBallColour;

            // Below critical the ramp tints the owner colour rather than replacing it, so you can
            // still read possession at a glance while the core warms up.
            Color heatTint = Color.Lerp(_heatCold, _heatWarm, _heat01);
            return Color.Lerp(identity, heatTint, _heat01 * 0.75f);
        }
    }
}
