using Deadball.Fighters;
using Sirenix.OdinInspector;
using UnityEngine;

namespace Deadball.Presentation
{
    /// <summary>
    /// Drives the runner's locomotion blend from what the motor is actually doing.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Read-only with respect to gameplay: it samples the rigidbody and sets animator parameters,
    /// and never feeds anything back. Animation cannot therefore change where a runner is, which
    /// keeps the physics authoritative and the netcode-free determinism intact.
    /// </para>
    /// <para>
    /// The clips are humanoid, so the same controller retargets onto both character rigs even though
    /// they came from different packs.
    /// </para>
    /// </remarks>
    public class FighterAnimatorDriver : MonoBehaviour
    {
        [Required, SerializeField] Fighter _fighter;
        [Required, SerializeField] FighterModelSelector _models;

        [Title("Parameters")]
        [SerializeField] string _speedParameter = "Speed";

        [Tooltip("Smoothing on the blend so a direction change does not snap the legs.")]
        [SuffixLabel("s", true), PropertyRange(0f, 0.5f), SerializeField] float _damping = 0.12f;

        [Title("Runtime"), ShowInInspector, ReadOnly]
        public float Speed { get; private set; }

        Animator _animator;
        Rigidbody _body;
        int _speedHash;
        int _appliedSlot = -1;

        void Awake()
        {
            _speedHash = Animator.StringToHash(_speedParameter);
            _body = _fighter != null ? _fighter.GetComponent<Rigidbody>() : null;
        }

        void LateUpdate()
        {
            if (_fighter == null || _body == null) return;

            // The active model changes when the slot is assigned, so the animator is re-fetched
            // rather than cached once at Awake.
            if (_appliedSlot != _fighter.Slot || _animator == null)
            {
                _animator = _models != null ? _models.GetComponentInChildren<Animator>() : null;
                _appliedSlot = _fighter.Slot;
            }

            if (_animator == null || _animator.runtimeAnimatorController == null) return;

            Vector3 flat = _body.linearVelocity;
            flat.y = 0f;
            Speed = flat.magnitude;

            _animator.SetFloat(_speedHash, Speed, _damping, Time.deltaTime);
        }
    }
}
