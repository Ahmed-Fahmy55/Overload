using Deadball.Ball;
using Deadball.Fighters;
using Deadball.Match;
using Sirenix.OdinInspector;
using UnityEngine;

namespace Deadball.AI
{
    /// <summary>The four states from GDD section 13.2.</summary>
    public enum AiState
    {
        /// <summary>Core is loose: go and get it.</summary>
        Hunt,

        /// <summary>Holding the core: close to range, face, charge, launch.</summary>
        Aim,

        /// <summary>They have the core: never stand still.</summary>
        Evade,

        /// <summary>The core is coming: clamp it or roll clear.</summary>
        React
    }

    /// <summary>
    /// The house runner (OVERLOAD GDD section 13).
    /// </summary>
    /// <remarks>
    /// <para>
    /// This writes the same four inputs a player presses and nothing else, which is the whole point
    /// of 13.1: Solo and Local Versus share every line of runner code, a human can be swapped into
    /// this slot mid-match to debug, and the AI physically cannot cheat because it has no channel
    /// through which to do so.
    /// </para>
    /// <para>
    /// A hand-rolled state machine, as the design insists - four states and one float do not justify
    /// a behaviour-tree framework or debugging a graph at 2am.
    /// </para>
    /// </remarks>
    [DefaultExecutionOrder(-50)]
    public class AiInputSource : MonoBehaviour, IFighterInput
    {
        [Title("Profile")]
        [Required, SerializeField] AiProfile _profile;

        [Title("Scene References")]
        [Required, SerializeField] Fighter _self;
        [Required, SerializeField] BallController _core;

        [Tooltip("Optional. Lets the runner get visibly more careful at CRITICAL (13.5).")]
        [SerializeField] RallyHeat _heat;

        [Title("Runtime"), ShowInInspector, ReadOnly]
        /// <summary>The tier this runner is playing at (13.3). Read-only: the roster owns the choice.</summary>
        public AiProfile Profile => _profile;

        public AiState State { get; private set; } = AiState.Hunt;

        [ShowInInspector, ReadOnly]
        public bool WillClampThisThrow { get; private set; }

        public Vector2 Move { get; private set; }
        public bool ThrowHeld { get; private set; }

        public bool DodgePressed
        {
            get
            {
                bool pressed = _dodgeQueued;
                _dodgeQueued = false;
                return pressed;
            }
        }

        public bool CatchPressed
        {
            get
            {
                bool pressed = _clampQueued;
                _clampQueued = false;
                return pressed;
            }
        }

        bool _dodgeQueued;
        bool _clampQueued;
        float _nextThink;
        float _nextDodge;
        float _chargeTarget;
        float _clampPressWindow;
        int _decidedForThrowId = -1;
        Vector2 _evadeBias = Vector2.up;

        bool CoreIsCritical => _heat != null && _heat.IsCritical;

        void Update()
        {
            if (_self == null || _core == null || _profile == null) return;

            // The reaction delay gates re-deciding, not acting - between decisions it keeps doing
            // what it last decided, which is what a person looks like (13.4).
            if (Time.time >= _nextThink)
            {
                _nextThink = Time.time + _profile.NextReactionDelay();
                State = ChooseState();
            }

            Act();
        }

        /// <summary>Wires a runtime-spawned brain, since it has no inspector to be set up from.</summary>
        public void Configure(AiProfile profile, Fighter self, BallController core, RallyHeat heat = null)
        {
            _profile = profile;
            _self = self;
            _core = core;
            _heat = heat;
        }

        public void Clear()
        {
            Move = Vector2.zero;
            ThrowHeld = false;
            _dodgeQueued = false;
            _clampQueued = false;
            _decidedForThrowId = -1;
        }

        AiState ChooseState()
        {
            if (_core.State == BallState.Flying && IsIncoming()) return AiState.React;
            if (_core.HolderSlot == _self.Slot) return AiState.Aim;
            if (_core.State == BallState.Loose) return AiState.Hunt;

            return AiState.Evade;
        }

        void Act()
        {
            switch (State)
            {
                case AiState.Hunt: Hunt(); break;
                case AiState.Aim: Aim(); break;
                case AiState.Evade: Evade(); break;
                case AiState.React: React(); break;
            }
        }

        void Hunt()
        {
            ThrowHeld = false;

            Vector3 toCore = _core.transform.position - transform.position;
            toCore.y = 0f;

            // A slight overshoot past the core stops the approach looking like a nav-mesh agent
            // gliding onto a waypoint (13.2).
            Vector3 overshoot = toCore.normalized * 0.6f;
            Move = Flatten(toCore + overshoot);
        }

        void Aim()
        {
            IBallTarget opponent = Opponent();
            if (opponent == null) { Move = Vector2.zero; ThrowHeld = false; return; }

            Vector3 toOpponent = opponent.CenterPosition - transform.position;
            toOpponent.y = 0f;
            float range = toOpponent.magnitude;

            if (!ThrowHeld)
                _chargeTarget = _profile.ChargeTargetFor(CoreIsCritical);

            // Close to preferred range first; once there, root and wind up. Facing follows Move even
            // while rooted, so aiming and moving use the same channel a player has (7.3).
            if (range > _profile.PreferredRange * 1.15f)
            {
                Move = Flatten(toOpponent);
                ThrowHeld = false;
                return;
            }

            Move = Flatten(ApplyAimError(toOpponent));
            ThrowHeld = true;

            if (_self.Thrower.Charge01 >= _chargeTarget)
                ThrowHeld = false;
        }

        void Evade()
        {
            IBallTarget threat = Opponent();
            if (threat == null) { Move = Vector2.zero; return; }

            Vector3 fromThreat = transform.position - threat.CenterPosition;
            fromThreat.y = 0f;

            // Perpendicular strafe rather than a straight retreat: backing away in a line is both
            // easy to lead and a good way to end up pinned against the containment field.
            Vector3 strafe = Vector3.Cross(Vector3.up, fromThreat.normalized);
            Move = Flatten(strafe * _evadeBias.y + fromThreat.normalized * 0.35f);

            if (Time.time < _nextDodge) return;

            _nextDodge = Time.time + _profile.NextDodgeInterval();
            _evadeBias.y = -_evadeBias.y;
            _dodgeQueued = true;
        }

        void React()
        {
            ThrowHeld = false;

            // Decide once per throw, not once per frame, or the roll is re-rolled 60 times a second
            // and every tier collapses into "always clamps".
            int throwId = _core.GetInstanceID() ^ Mathf.RoundToInt(_core.Charge01 * 1000f);
            if (_decidedForThrowId != throwId)
            {
                _decidedForThrowId = throwId;
                WillClampThisThrow = Random.value < _profile.ClampChanceFor(CoreIsCritical);
                _clampPressWindow = _profile.NextClampPressWindow();
            }

            float arrival = SecondsToArrival();

            if (WillClampThisThrow)
            {
                Move = Vector2.zero;

                if (arrival > 0f && arrival <= _clampPressWindow)
                    _clampQueued = true;

                return;
            }

            // Not clamping: roll clear, perpendicular to the incoming core so the dodge actually
            // moves out of its path rather than along it.
            Vector3 incoming = _core.Velocity;
            incoming.y = 0f;
            Vector3 sideways = Vector3.Cross(Vector3.up, incoming.normalized);
            Move = Flatten(sideways * _evadeBias.y);

            if (arrival > 0f && arrival <= _profile.ClampTargetArrival + 0.12f)
                _dodgeQueued = true;
        }

        /// <summary>Is the core closing on us fast enough to be a threat?</summary>
        bool IsIncoming()
        {
            Vector3 toSelf = _self.CenterPosition - _core.transform.position;
            float distance = toSelf.magnitude;
            if (distance < 0.01f) return true;

            return Vector3.Dot(_core.Velocity, toSelf / distance) > 0.5f;
        }

        float SecondsToArrival()
        {
            Vector3 toSelf = _self.CenterPosition - _core.transform.position;
            float distance = toSelf.magnitude;
            if (distance < 0.0001f) return 0f;

            float closing = Vector3.Dot(_core.Velocity, toSelf / distance);
            if (closing <= 0.01f) return -1f;

            return (distance - _self.CatchRadius) / closing;
        }

        Vector3 ApplyAimError(Vector3 direction)
        {
            float error = Random.Range(-_profile.AimErrorDegrees, _profile.AimErrorDegrees);
            return Quaternion.Euler(0f, error, 0f) * direction;
        }

        IBallTarget Opponent()
        {
            var targets = BallTargetRegistry.Targets;
            for (int i = 0; i < targets.Count; i++)
            {
                if (targets[i].Slot != _self.Slot && targets[i].IsInPlay)
                    return targets[i];
            }

            return null;
        }

        static Vector2 Flatten(Vector3 direction)
        {
            var flat = new Vector2(direction.x, direction.z);
            return flat.sqrMagnitude > 1f ? flat.normalized : flat;
        }
    }
}
