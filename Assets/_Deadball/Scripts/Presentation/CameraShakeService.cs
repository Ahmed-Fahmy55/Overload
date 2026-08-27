using Core.Events;
using Deadball.Events;
using Deadball.Fighters;
using Sirenix.OdinInspector;
using Unity.Cinemachine;
using UnityEngine;

namespace Deadball.Presentation
{
    /// <summary>
    /// Camera punch on clamps and knocks, scaled by charge (OVERLOAD GDD section 22).
    /// </summary>
    /// <remarks>
    /// <para>
    /// The fixed camera used to punch its own transform. Now that Cinemachine owns the camera, that
    /// approach would be overwritten every frame by the brain - so the punch is an impulse the rig
    /// listens for instead.
    /// </para>
    /// <para>
    /// The two clamp tiers get very different treatment on purpose: a perfect clamp is meant to be
    /// disproportionately loud (8.6), while a late clamp is the mercy tier and should feel like a
    /// scramble, not a triumph.
    /// </para>
    /// </remarks>
    [RequireComponent(typeof(CinemachineImpulseSource))]
    public class CameraShakeService : MonoBehaviour
    {
        [Title("Force")]
        [Tooltip("Perfect clamp. This is the trailer shot - make it loud.")]
        [MinValue(0f), SerializeField] float _onPerfectClamp = 1.1f;

        [Tooltip("Late clamp. A scramble, not a triumph.")]
        [MinValue(0f), SerializeField] float _onLateClamp = 0.35f;

        [MinValue(0f), SerializeField] float _onKnock = 0.8f;
        [MinValue(0f), SerializeField] float _onKnockOut = 1.5f;

        [Title("Charge Scaling")]
        [SerializeField] float _minChargeScale = 0.6f;
        [SerializeField] float _maxChargeScale = 1.4f;

        CinemachineImpulseSource _source;

        EventBinding<BallCaught> _caught;
        EventBinding<FighterKnocked> _knocked;
        EventBinding<FighterKnockedOut> _knockedOut;

        void Awake() => _source = GetComponent<CinemachineImpulseSource>();

        void OnEnable()
        {
            _caught = new EventBinding<BallCaught>(OnClamped);
            _knocked = new EventBinding<FighterKnocked>(
                evt => Shake(_onKnock * Mathf.Lerp(_minChargeScale, _maxChargeScale, evt.Charge01)));
            _knockedOut = new EventBinding<FighterKnockedOut>(() => Shake(_onKnockOut));

            EventBus<BallCaught>.Register(_caught);
            EventBus<FighterKnocked>.Register(_knocked);
            EventBus<FighterKnockedOut>.Register(_knockedOut);
        }

        void OnDisable()
        {
            EventBus<BallCaught>.Deregister(_caught);
            EventBus<FighterKnocked>.Deregister(_knocked);
            EventBus<FighterKnockedOut>.Deregister(_knockedOut);
        }

        void OnClamped(BallCaught evt)
        {
            float force = evt.Tier == ClampTier.Perfect
                ? _onPerfectClamp * Mathf.Lerp(_minChargeScale, _maxChargeScale, evt.Charge01)
                : _onLateClamp;

            Shake(force);
        }

        /// <summary>Fires a one-off impulse. Any listener on the rig picks it up.</summary>
        public void Shake(float force)
        {
            if (_source == null || force <= 0f) return;

            _source.GenerateImpulseWithForce(force);
        }
    }
}
