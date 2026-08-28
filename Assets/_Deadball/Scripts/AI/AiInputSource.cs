using Deadball.Ball;
using Deadball.Fighters;
using Deadball.Match;
using Sirenix.OdinInspector;
using UnityEngine;
using UnityEngine.AI;

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

        [Title("Obstacle Avoidance", "13.2 - a runner that walks into a crate is not a runner")]
        [Tooltip("How far ahead the runner looks for props.")]
        [SuffixLabel("m", true), MinValue(0.1f), SerializeField] float _probeDistance = 2.4f;

        [Tooltip("Roughly the runner's shoulder width, so it does not clip corners.")]
        [SuffixLabel("m", true), MinValue(0.05f), SerializeField] float _probeRadius = 0.45f;

        [Tooltip("How often the path is re-queried while chasing something that moves.")]
        [SuffixLabel("s", true), MinValue(0.02f), SerializeField] float _repathInterval = 0.15f;

        [Tooltip("Re-query early if the goal has moved further than this since the last path.")]
        [SuffixLabel("m", true), MinValue(0.1f), SerializeField] float _repathDistance = 1.5f;

        [Tooltip("What counts as an obstacle. Runners and the core are always ignored.")]
        [SerializeField] LayerMask _obstacleMask = ~0;

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
        NavMeshPath _path;
        Vector3 _pathHeading;
        Vector3 _pathTarget;
        float _nextPathAt;
        float _aimingSince = -1f;

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
            _aimingSince = -1f;
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
            if (State != AiState.Aim) _aimingSince = -1f;

            switch (State)
            {
                case AiState.Hunt: Hunt(); break;
                case AiState.Aim: Aim(); break;
                case AiState.Evade: Evade(); break;
                case AiState.React: React(); break;
            }
        }


        /// <summary>
        /// Bends a desired heading around whatever is in the way.
        /// </summary>
        /// <remarks>
        /// <para>
        /// Without this the runner walks straight at the core and simply stops when a crate is
        /// between the two, because the motor keeps pushing into a collider that will not move. The
        /// fix is steering rather than pathfinding: probe ahead, and if the way is blocked take the
        /// nearest heading that is not.
        /// </para>
        /// <para>
        /// Angles are tried in widening pairs so the runner prefers the smallest deviation that
        /// works, which keeps the detour looking like a decision rather than a random turn.
        /// </para>
        /// </remarks>
        /// <summary>
        /// A heading toward <paramref name="target"/> that goes around the deck's geometry.
        /// </summary>
        /// <remarks>
        /// <para>
        /// <see cref="Steer"/> is reactive: it probes a few angles and picks one that is clear. That
        /// works for clipping a corner but not for routing around a coolant tank, where every
        /// heading within its fan is blocked and the runner stalls against the prop while the core
        /// sits on the far side. A path query solves the whole problem instead of a local slice.
        /// </para>
        /// <para>
        /// Only the query is used - there is no NavMeshAgent moving the transform. The runner is
        /// still driven entirely through <see cref="IFighterInput"/>, so 13.1 holds: the bot can
        /// only press what a player can press, and it moves at exactly the speeds a player does.
        /// </para>
        /// <para>
        /// Falls back to the reactive steer when no path exists, so an unbaked scene degrades to the
        /// previous behaviour rather than to a runner that refuses to move.
        /// </para>
        /// </remarks>
        Vector3 PathTo(Vector3 target)
        {
            Vector3 direct = target - transform.position;
            direct.y = 0f;

            // A clear line needs no detour, and skipping the query keeps the common case cheap.
            if (direct.sqrMagnitude < 0.01f || IsClear(direct.normalized))
                return direct;

            _path ??= new NavMeshPath();

            // Re-queried on a timer rather than per frame: the corner we are walking toward does
            // not change often, and the delay is far shorter than the reaction delay anyway.
            bool stale = Time.time >= _nextPathAt
                || (target - _pathTarget).sqrMagnitude > _repathDistance * _repathDistance;

            if (stale)
            {
                _nextPathAt = Time.time + _repathInterval;
                _pathTarget = target;
                _pathHeading = Vector3.zero;

                if (NavMesh.CalculatePath(transform.position, target, NavMesh.AllAreas, _path)
                    && _path.status != NavMeshPathStatus.PathInvalid
                    && _path.corners.Length > 1)
                {
                    // corners[0] is where we already are; corners[1] is the next place worth being.
                    Vector3 corner = _path.corners[1] - transform.position;
                    corner.y = 0f;
                    if (corner.sqrMagnitude > 0.0001f) _pathHeading = corner.normalized;
                }
            }

            if (_pathHeading.sqrMagnitude > 0.0001f)
                return _pathHeading * direct.magnitude;

            return Steer(direct);
        }

        Vector3 Steer(Vector3 desired)
        {
            Vector3 flat = new(desired.x, 0f, desired.z);
            if (flat.sqrMagnitude < 0.0001f) return desired;

            Vector3 direction = flat.normalized;
            if (IsClear(direction)) return desired;

            for (int step = 1; step <= 4; step++)
            {
                float angle = step * 25f;

                Vector3 right = Quaternion.AngleAxis(angle, Vector3.up) * direction;
                if (IsClear(right)) return right * flat.magnitude;

                Vector3 left = Quaternion.AngleAxis(-angle, Vector3.up) * direction;
                if (IsClear(left)) return left * flat.magnitude;
            }

            // Boxed in: keep the original heading rather than freezing, so contact resolves it.
            return desired;
        }

        bool IsClear(Vector3 direction)
        {
            Vector3 origin = transform.position + Vector3.up * 0.9f;

            RaycastHit[] hits = Physics.SphereCastAll(origin, _probeRadius, direction,
                _probeDistance, _obstacleMask, QueryTriggerInteraction.Ignore);

            foreach (RaycastHit hit in hits)
            {
                if (hit.collider == null) continue;
                if (hit.collider.transform.IsChildOf(transform)) continue;

                // Other runners are not obstacles to route around - they move, and treating them
                // as walls makes the bot refuse to close on the core. The core is the goal itself.
                if (hit.collider.GetComponentInParent<Fighter>() != null) continue;
                if (hit.collider.GetComponentInParent<BallController>() != null) continue;

                return false;
            }

            return true;
        }

        void Hunt()
        {
            ThrowHeld = false;

            Vector3 toCore = _core.transform.position - transform.position;
            toCore.y = 0f;

            // A slight overshoot past the core stops the approach looking like a nav-mesh agent
            // gliding onto a waypoint (13.2).
            Vector3 overshoot = toCore.normalized * 0.6f;
            Move = Flatten(PathTo(_core.transform.position + overshoot));
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
            if (_aimingSince < 0f) _aimingSince = Time.time;

            // Holding the core costs 20% speed, so a runner that insists on closing before it will
            // throw can never catch an opponent who simply runs - it chases until the round ends.
            // Past the cap it throws from wherever it is, which is also what a player does.
            bool chasedTooLong = Time.time - _aimingSince > _profile.MaxCloseSeconds;

            if (range > _profile.PreferredRange * 1.15f && !chasedTooLong)
            {
                Move = Flatten(PathTo(opponent.CenterPosition));
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
            Move = Flatten(Steer(strafe * _evadeBias.y + fromThreat.normalized * 0.35f));

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
