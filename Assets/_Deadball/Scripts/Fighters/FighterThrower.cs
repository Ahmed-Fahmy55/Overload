using System;
using Core.Events;
using Deadball.Ball;
using Deadball.Config;
using Deadball.Events;
using Sirenix.OdinInspector;
using UnityEngine;

namespace Deadball.Fighters
{
    /// <summary>
    /// Possession, charging and throwing (GDD sections 7.2 and 7.3).
    /// </summary>
    /// <remarks>
    /// <para>
    /// Charging roots the fighter, and that rooted window is the entire risk of throwing - charge to
    /// max in front of someone five metres away and they will simply walk into your face. The class
    /// therefore does two things and no more: it holds the charge value, and it tells the motor when
    /// the fighter is not allowed to move.
    /// </para>
    /// <para>
    /// Aim is facing-based rather than mouse-based, which is what makes two players on one machine
    /// possible at all (12). The soft snap exists so a near-miss reads as a decision rather than as
    /// bad controls.
    /// </para>
    /// </remarks>
    public class FighterThrower : MonoBehaviour
    {
        [Required, SerializeField] MatchConfig _config;
        [Required, SerializeField] FighterMotor _motor;
        [Tooltip("Where a held ball parents itself.")]
        [Required, SerializeField] Transform _handAnchor;

        [Title("Runtime"), ShowInInspector, ReadOnly]
        public float Charge01 { get; private set; }

        [ShowInInspector, ReadOnly]
        public bool IsCharging { get; private set; }

        [ShowInInspector, ReadOnly]
        public bool HasBall => _ball != null;

        public Transform HandAnchor => _handAnchor;

        /// <summary>Raised every frame the charge changes, for the ring on the character.</summary>
        public event Action<float> ChargeChanged;

        /// <summary>Raised on release, carrying the charge the ball left at.</summary>
        public event Action<float> Thrown;

        BallController _ball;
        int _slot;
        bool _enabled = true;

        public void Initialise(int slot) => _slot = slot;

        void OnEnable()
        {
            if (_motor != null) _motor.DodgeStarted += CancelCharge;
        }

        void OnDisable()
        {
            if (_motor != null) _motor.DodgeStarted -= CancelCharge;
        }

        /// <summary>Drives charge and release. Called once per frame with the raw button state.</summary>
        public void Tick(bool throwHeld)
        {
            bool canCharge = _enabled && HasBall && !_motor.IsDodging;

            if (!canCharge)
            {
                if (IsCharging) CancelCharge();
                return;
            }

            if (throwHeld)
            {
                // A ball that arrived pre-charged from a catch keeps that charge, so a fresh press
                // resumes from it rather than starting over (8.5).
                if (!IsCharging)
                    EventBus<ChargeStarted>.Raise(new ChargeStarted(_slot));

                IsCharging = true;
                _motor.Rooted = true;
                SetCharge(Charge01 + Time.deltaTime / _config.MaxChargeTime);
            }
            else if (IsCharging)
            {
                Release();
            }
        }

        /// <summary>Takes possession, preserving the charge a catch handed over.</summary>
        public void ReceiveBall(BallController ball, float charge01)
        {
            _ball = ball;
            _motor.Slowed = true;
            SetCharge(charge01);
        }

        /// <summary>Called by the ball when it leaves, for any reason.</summary>
        public void ClearBall()
        {
            _ball = null;
            IsCharging = false;
            _motor.Slowed = false;
            _motor.Rooted = false;
            SetCharge(0f);
        }

        /// <summary>Drops the ball where the fighter stands - used on knockout and between rounds.</summary>
        public void DropBall()
        {
            // The Unity null check also covers a ball already destroyed by a scene teardown.
            if (_ball == null) return;

            _ball.GoLoose(transform.position);
            _ball = null;
        }

        /// <summary>Charge drops to zero, the ball stays (7.3).</summary>
        public void CancelCharge()
        {
            if (!IsCharging && Charge01 <= 0f) return;

            IsCharging = false;
            _motor.Rooted = false;
            SetCharge(0f);
            EventBus<ChargeCancelled>.Raise(new ChargeCancelled(_slot));
        }

        public void SetEnabled(bool enabled)
        {
            _enabled = enabled;
            if (!enabled) CancelCharge();
        }

        void Release()
        {
            float charge = Charge01;
            BallController ball = _ball;

            IsCharging = false;
            _motor.Rooted = false;

            ball.Throw(AimDirection(), charge);
            Thrown?.Invoke(charge);
        }

        /// <summary>Facing, nudged toward an opponent already inside the snap cone.</summary>
        Vector3 AimDirection()
        {
            Vector3 facing = _motor.Facing;
            facing.y = 0f;
            facing.Normalize();

            var targets = BallTargetRegistry.Targets;
            Vector3 best = facing;
            float bestAngle = _config.AimSnapAngle;

            for (int i = 0; i < targets.Count; i++)
            {
                IBallTarget target = targets[i];
                if (target.Slot == _slot || !target.IsInPlay) continue;

                Vector3 toTarget = target.CenterPosition - _handAnchor.position;
                toTarget.y = 0f;
                if (toTarget.sqrMagnitude < 0.0001f) continue;

                toTarget.Normalize();
                float angle = Vector3.Angle(facing, toTarget);
                if (angle > bestAngle) continue;

                bestAngle = angle;
                best = toTarget;
            }

            return best;
        }

        void SetCharge(float value)
        {
            float clamped = Mathf.Clamp01(value);
            if (Mathf.Approximately(clamped, Charge01)) return;

            Charge01 = clamped;
            ChargeChanged?.Invoke(Charge01);
        }
    }
}
