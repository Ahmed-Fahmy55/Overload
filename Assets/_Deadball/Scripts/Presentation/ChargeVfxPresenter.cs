using Deadball.Fighters;
using Sirenix.OdinInspector;
using UnityEngine;

namespace Deadball.Presentation
{
    /// <summary>
    /// The wind-up, shown as gathering plasma rather than as a growing core (OVERLOAD GDD 22).
    /// </summary>
    /// <remarks>
    /// <para>
    /// Charge is telegraph layer 1: the other player has to read that a throw is coming and roughly
    /// how hard, from across the deck. Swelling the core does that badly - it fights the one rule
    /// the core has to obey, which is that its position stays readable at all times (17) - so the
    /// core now barely swells and the wind-up is carried by plasma gathering in the hand instead.
    /// </para>
    /// <para>
    /// Driven from the thrower's own charge event, so it costs nothing when nobody is winding up.
    /// </para>
    /// </remarks>
    public class ChargeVfxPresenter : MonoBehaviour
    {
        [Title("References")]
        [Required, SerializeField] FighterThrower _thrower;

        [Tooltip("Sits at the hand anchor, where the core is held.")]
        [Required, SerializeField] ParticleSystem _vfx;

        [Tooltip("Optional aura around the runner themself. Its root particle system drives the whole "
        + "effect - the pack ships it as several systems and they are played together.")]
        [SerializeField] ParticleSystem _aura;

        [Title("Aura Ramp", "The runner winds up too, not just their hand")]
        [MinValue(0f), SerializeField] float _auraMinScale = 0.55f;
        [MinValue(0f), SerializeField] float _auraMaxScale = 1f;

        [Title("Ramp", "How hard the wind-up reads at full charge")]
        [MinValue(0f), SerializeField] float _minRate = 8f;
        [MinValue(0f), SerializeField] float _maxRate = 120f;
        [MinValue(0f), SerializeField] float _minSize = 0.12f;
        [MinValue(0f), SerializeField] float _maxSize = 0.42f;

        [Title("Runtime"), ShowInInspector, ReadOnly]
        public float Charge01 { get; private set; }

        void OnEnable()
        {
            if (_thrower != null) _thrower.ChargeChanged += OnChargeChanged;
            Stop();
        }

        void OnDisable()
        {
            if (_thrower != null) _thrower.ChargeChanged -= OnChargeChanged;
            Stop();
        }

        void OnChargeChanged(float charge01)
        {
            Charge01 = Mathf.Clamp01(charge01);

            // Zero covers release, a cancelled wind-up and losing the core, so every way a charge
            // can end puts the plasma out without each needing its own hook.
            if (Charge01 <= 0.001f)
            {
                Stop();
                return;
            }

            DriveAura();

            if (_vfx == null) return;

            if (!_vfx.isPlaying) _vfx.Play();

            ParticleSystem.EmissionModule emission = _vfx.emission;
            emission.rateOverTime = Mathf.Lerp(_minRate, _maxRate, Charge01);

            ParticleSystem.MainModule main = _vfx.main;
            main.startSize = Mathf.Lerp(_minSize, _maxSize, Charge01);
        }

        /// <summary>
        /// Grows the runner's aura with the wind-up.
        /// </summary>
        /// <remarks>
        /// Scaled at the root rather than ramped per system. The pack's aura is seven particle
        /// systems whose rates are driven by curves and bursts rather than a single rate value, so
        /// there is no one number to lerp - but the whole thing is authored around a character, and
        /// scaling the root reads as the effect gathering.
        /// </remarks>
        void DriveAura()
        {
            if (_aura == null) return;

            // withChildren: the pack ships the aura as a root plus six sub-emitters, and playing
            // only the root leaves the sparks and trails dead.
            if (!_aura.isPlaying) _aura.Play(withChildren: true);

            _aura.transform.localScale =
                Vector3.one * Mathf.Lerp(_auraMinScale, _auraMaxScale, Charge01);
        }

        void Stop()
        {
            Charge01 = 0f;

            if (_aura != null)
                _aura.Stop(withChildren: true, ParticleSystemStopBehavior.StopEmittingAndClear);

            if (_vfx == null) return;

            // Cleared rather than left to fade: a released throw should take its wind-up with it.
            _vfx.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        }
    }
}
