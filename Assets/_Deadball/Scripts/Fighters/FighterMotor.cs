using System;
using Deadball.Config;
using Sirenix.OdinInspector;
using UnityEngine;
using Zone8.ImprovedTimers;

namespace Deadball.Fighters
{
    /// <summary>
    /// Locomotion, facing, and the dodge roll (GDD sections 7.1 and 7.4).
    /// </summary>
    /// <remarks>
    /// Movement is the one thing in this game that must be perfectly predictable - the design leans
    /// on the player being able to say where both fighters will be in half a second - so there is no
    /// acceleration curve, no sprint and no stamina here. Velocity is set outright each physics step.
    /// </remarks>
    [RequireComponent(typeof(Rigidbody))]
    public class FighterMotor : MonoBehaviour
    {
        [Required, SerializeField] MatchConfig _config;

        [Title("Runtime"), ShowInInspector, ReadOnly]
        public bool IsDodging { get; private set; }

        /// <summary>True during the i-frame slice of a dodge roll. Shorter than the roll itself.</summary>
        [ShowInInspector, ReadOnly]
        public bool IsInvulnerable => IsDodging && Dodge.CurrentTime > _config.DodgeDuration - _config.DodgeInvulnerability;

        [ShowInInspector, ReadOnly]
        public bool CanDodge => !IsDodging && _acceptsInput && !IsStunned && !Cooldown.IsRunning;

        /// <summary>True while the dodge is still recovering.</summary>
        public bool IsDodgeCoolingDown => Cooldown is { IsRunning: true, IsFinished: false };

        /// <summary>
        /// How far the dodge has recovered, 0 just after a dodge to 1 when it is available again.
        /// </summary>
        /// <remarks>
        /// Expressed as readiness rather than as time remaining so the HUD can fill both ability
        /// icons from the same number - the catcher reports its lockout the same way.
        /// </remarks>
        public float DodgeReady01 => IsDodgeCoolingDown ? 1f - Cooldown.Progress : 1f;

        /// <summary>
        /// True while staggered by a perfect clamp (8.2). No movement, no dodge, no throw.
        /// </summary>
        /// <remarks>
        /// Deliberately not the same thing as being rooted by a charge: a stun is imposed, so it also
        /// blocks the dodge that would normally cancel a charge.
        /// </remarks>
        [ShowInInspector, ReadOnly]
        public bool IsStunned => Stun is { IsRunning: true, IsFinished: false };

        /// <summary>Set while charging a throw: rotation stays free, translation does not (7.3).</summary>
        public bool Rooted { get; set; }

        /// <summary>Set while holding the ball. The cost of possession is -20% speed (7.2).</summary>
        public bool Slowed { get; set; }

        public Vector3 Facing => transform.forward;

        /// <summary>Raised when a dodge actually starts, for the dust puff and the charge cancel.</summary>
        public event Action DodgeStarted;

        Rigidbody _rb;
        CountdownTimer _dodge;
        CountdownTimer _cooldown;
        CountdownTimer _stun;
        Vector2 _moveInput;
        Vector3 _dodgeDirection;
        bool _acceptsInput = true;

        // Built on demand rather than in Awake. The Input System raises its join callback from
        // PlayerInput.OnEnable, which can reach this component before its own Awake has run - so
        // the very first SetControlEnabled of a match would otherwise hit a null timer.
        CountdownTimer Dodge => _dodge ??= new CountdownTimer(_config.DodgeDuration);
        CountdownTimer Cooldown => _cooldown ??= new CountdownTimer(_config.DodgeCooldown);
        CountdownTimer Stun => _stun ??= new CountdownTimer(_config.PerfectClampStun);

        Rigidbody Body
        {
            get
            {
                if (_rb != null) return _rb;

                _rb = GetComponent<Rigidbody>();
                _rb.useGravity = false;
                _rb.freezeRotation = true;
                _rb.constraints |= RigidbodyConstraints.FreezePositionY;
                _rb.interpolation = RigidbodyInterpolation.Interpolate;
                return _rb;
            }
        }

        void Awake() => _ = Body;

        void OnDestroy()
        {
            _dodge?.Dispose();
            _cooldown?.Dispose();
            _stun?.Dispose();
        }

        void FixedUpdate()
        {
            if (IsDodging && Dodge.IsFinished)
                IsDodging = false;

            Body.linearVelocity = IsDodging
                ? _dodgeDirection * _config.DodgeSpeed
                : DesiredVelocity();

            ApplyFacing();
        }

        /// <summary>Feeds this frame's stick or key direction. Safe to call every frame.</summary>
        public void SetMoveInput(Vector2 move) => _moveInput = Vector2.ClampMagnitude(move, 1f);

        /// <summary>Starts a dodge roll if one is available. Returns whether it committed.</summary>
        public bool TryDodge()
        {
            if (!CanDodge) return false;

            _dodgeDirection = _moveInput.sqrMagnitude > 0.01f
                ? new Vector3(_moveInput.x, 0f, _moveInput.y).normalized
                : transform.forward;

            IsDodging = true;
            Dodge.Reset(_config.DodgeDuration);
            Dodge.Start();
            Cooldown.Reset(_config.DodgeCooldown);
            Cooldown.Start();

            DodgeStarted?.Invoke();
            return true;
        }

        /// <summary>Freezes the fighter between rounds and after a knockout.</summary>
        public void SetInputEnabled(bool enabled)
        {
            _acceptsInput = enabled;
            if (enabled) return;

            _moveInput = Vector2.zero;
            IsDodging = false;
            Rooted = false;
            Dodge.Stop();
            Stun.Stop();
            Body.linearVelocity = Vector3.zero;
        }

        /// <summary>Places the fighter for a fresh round, clearing dodge and cooldown state.</summary>
        public void Teleport(Vector3 position, Quaternion rotation)
        {
            IsDodging = false;
            Dodge.Stop();
            Cooldown.Stop();
            Stun.Stop();
            _moveInput = Vector2.zero;

            Body.linearVelocity = Vector3.zero;
            Body.position = position;
            Body.rotation = rotation;
            transform.SetPositionAndRotation(position, rotation);
        }

        /// <summary>Staggers the runner for a moment after being beaten by a perfect clamp.</summary>
        public void Stagger(float seconds)
        {
            if (seconds <= 0f) return;

            IsDodging = false;
            Dodge.Stop();
            Stun.Reset(seconds);
            Stun.Start();
        }

        Vector3 DesiredVelocity()
        {
            if (!_acceptsInput || Rooted || IsStunned) return Vector3.zero;

            float speed = _config.MoveSpeed * (Slowed ? _config.HoldingSpeedMultiplier : 1f);
            return new Vector3(_moveInput.x, 0f, _moveInput.y) * speed;
        }

        void ApplyFacing()
        {
            if (!_acceptsInput || IsStunned || _moveInput.sqrMagnitude < 0.01f) return;

            var target = Quaternion.LookRotation(new Vector3(_moveInput.x, 0f, _moveInput.y));
            Body.MoveRotation(Quaternion.RotateTowards(Body.rotation, target, _config.TurnSpeed * Time.fixedDeltaTime));
        }
    }
}
