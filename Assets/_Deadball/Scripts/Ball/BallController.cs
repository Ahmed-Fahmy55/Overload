using System;
using Core.Events;
using Deadball.Config;
using Deadball.Events;
using Sirenix.OdinInspector;
using UnityEngine;
using Zone8.ImprovedTimers;

namespace Deadball.Ball
{
    /// <summary>
    /// The one systemic object in the game: the ball, its state machine, and its flight.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Implements GDD sections 6.2 to 6.4. It owns state, ownership and physics only - trail,
    /// shadow and tint are separate presenters that subscribe to <see cref="StateChanged"/>, so the
    /// rules can be re-tuned without touching anything visual and the visuals can be rebuilt on
    /// Day 3 without risking the rules.
    /// </para>
    /// <para>
    /// The ball also owns the catch telegraph's third layer. It is the only object that knows both
    /// where it is going and how fast, so asking it to fire the flash cue is cheaper and more
    /// accurate than having each fighter estimate an incoming trajectory.
    /// </para>
    /// </remarks>
    [RequireComponent(typeof(Rigidbody))]
    public class BallController : MonoBehaviour
    {
        [Title("Config")]
        [Required, SerializeField] MatchConfig _config;

        [Title("Scene References")]
        [Required, SerializeField] SphereCollider _body;
        [Required, SerializeField] Transform _visual;
        [Tooltip("Trigger volume that magnetises a loose ball into a fighter walking over it.")]
        [Required, SerializeField] BallGrabTrigger _grabTrigger;

        [Title("Flight")]
        [Tooltip("Upward speed added to a throw so the arc reads from a top-down camera. An absolute "
            + "speed, not a fraction of the throw: a hard throw should be flatter than a soft one, "
            + "and a loft that scales with charge makes max-charge balls sail over the props.")]
        [SuffixLabel("m/s", true), PropertyRange(0f, 3f), SerializeField] float _throwLoft = 0.5f;

        [SuffixLabel("m", true), SerializeField] float _restHeight = 0.25f;

        [Tooltip("Clearance a loose ball needs from arena geometry before it will rest somewhere. "
            + "Kept below rest height so the probe cannot collide with the floor it is resting on.")]
        [SuffixLabel("m", true), MinValue(0.05f), SerializeField] float _looseClearance = 0.2f;

        [Title("Runtime"), ShowInInspector, ReadOnly]
        public BallState State { get; private set; } = BallState.Loose;

        [ShowInInspector, ReadOnly]
        public int HolderSlot => _carrier?.Slot ?? -1;

        /// <summary>Charge carried by the ball: what it was thrown at, or what a catch preserved (8.5).</summary>
        [ShowInInspector, ReadOnly]
        public float Charge01 { get; private set; }

        /// <summary>Set by the round manager for sudden death (10).</summary>
        public bool OvertimeActive { get; set; }

        /// <summary>
        /// Set by Rally Heat. A critical core kills in one touch (9).
        /// </summary>
        /// <remarks>
        /// Pushed in rather than pulled, so the ball keeps no reference to the heat system and stays
        /// testable on its own.
        /// </remarks>
        public bool IsCritical { get; set; }

        /// <summary>
        /// Deck footprint used to keep a loose core inside the arena.
        /// </summary>
        /// <remarks>
        /// Set per round from the arena rather than read from the shared config, because the two
        /// decks are different shapes and a square assumption strands the core outside The Spine.
        /// </remarks>
        public Vector2 ArenaSize { get; set; }

        public MatchConfig Config => _config;

        /// <summary>Current flight velocity. Read by the AI to predict arrival (13.2).</summary>
        public Vector3 Velocity => _rb != null ? _rb.linearVelocity : Vector3.zero;

        /// <summary>Renderer root, handed to presenters for tinting, flashing and squash.</summary>
        public Transform Visual => _visual;

        /// <summary>Fired as (previous, current). Presenters listen here rather than polling.</summary>
        public event Action<BallState, BallState> StateChanged;

        /// <summary>Fired when the ball crosses the flash lead time toward a fighter (8.2, layer 3).</summary>
        public event Action FlashCueFired;

        Rigidbody _rb;
        IBallCarrier _carrier;
        CountdownTimer _stallFailsafe;
        Vector3 _arenaCentre;
        int _throwerSlot = -1;
        float _throwTime = float.NegativeInfinity;
        int _bounces;
        int _flashedSlotMask;

        void Awake()
        {
            _rb = GetComponent<Rigidbody>();
            _rb.useGravity = false;
            _rb.interpolation = RigidbodyInterpolation.Interpolate;
            _rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;

            _arenaCentre = transform.position;
            _grabTrigger.Initialise(this);

            _stallFailsafe = new CountdownTimer(_config.StallFailsafe);
            _stallFailsafe.OnTimerStop += OnStallFailsafeElapsed;
        }

        void OnDestroy()
        {
            _stallFailsafe.OnTimerStop -= OnStallFailsafeElapsed;
            _stallFailsafe.Dispose();
        }

        void FixedUpdate()
        {
            if (State != BallState.Flying) return;

            // Custom gravity rather than the rigidbody's: the arc has to be shallow enough to stay
            // readable from a fixed top-down camera, which is well below real-world gravity.
            _rb.linearVelocity += Vector3.down * (_config.BallGravity * Time.fixedDeltaTime);

            UpdateFlashCue();

            if (_rb.linearVelocity.magnitude < _config.LooseSpeedThreshold)
                GoLoose(transform.position);
        }

        /// <summary>Puts the ball back in the centre of the arena, loose and uncharged.</summary>
        public void ResetForRound(Vector3 centre)
        {
            _arenaCentre = centre;
            _carrier?.ReleaseBall();
            _carrier = null;
            _throwerSlot = -1;
            _throwTime = float.NegativeInfinity;
            Charge01 = 0f;
            GoLoose(centre);
        }

        /// <summary>Walk-over pickup (7.2). Automatic, with no button and no failure state.</summary>
        public bool TryGrab(IBallTarget target)
        {
            if (State != BallState.Loose || target == null || !target.CanTakeBall)
                return false;

            Attach(target, 0f, wasCaught: false);
            return true;
        }

        /// <summary>Releases the held ball along <paramref name="direction"/> at the given charge.</summary>
        public void Throw(Vector3 direction, float charge01)
        {
            if (State != BallState.Held) return;

            int slot = HolderSlot;
            Detach();

            Charge01 = Mathf.Clamp01(charge01);
            _throwerSlot = slot;
            _throwTime = Time.time;
            _bounces = 0;
            _flashedSlotMask = 0;

            Vector3 flat = new Vector3(direction.x, 0f, direction.z).normalized;
            if (flat.sqrMagnitude < 0.001f) flat = transform.forward;

            // Horizontal speed is exactly what the tuning asset says; the loft rides on top of it
            // rather than being folded into a normalised direction, which used to quietly shave a
            // little off every throw.
            Vector3 launch = flat * _config.ThrowSpeedFor(Charge01, OvertimeActive) + Vector3.up * _throwLoft;

            // Back on for flight, where a ball crossing the arena in well under a second needs it.
            _rb.interpolation = RigidbodyInterpolation.Interpolate;
            _rb.linearVelocity = launch;

            SetState(BallState.Flying);
            _stallFailsafe.Reset(_config.StallFailsafe);
            _stallFailsafe.Start();

            EventBus<BallThrown>.Raise(new BallThrown(slot, Charge01));
            EventBus<BallPossessionChanged>.Raise(new BallPossessionChanged(-1, wasCaught: false));
        }

        /// <summary>
        /// Resolves a flying ball touching a fighter: caught, ignored, or a knock.
        /// </summary>
        /// <remarks>
        /// Called by the fighter's trigger volume rather than by a physics collision, because the
        /// ball and fighters never collide (see <see cref="DeadballLayers"/>). Order matters: self-hit
        /// immunity first, then the catch, then i-frames, then the knock.
        /// </remarks>
        public void ResolveTargetContact(IBallTarget target)
        {
            if (State != BallState.Flying || target == null || !target.IsInPlay) return;

            bool isThrower = target.Slot == _throwerSlot;
            if (isThrower && Time.time - _throwTime < _config.SelfHitImmunity)
                return;

            Fighters.ClampTier tier = target.ClampTier;
            if (tier != Fighters.ClampTier.None && target.CanTakeBall)
            {
                Clamp(target, tier);
                return;
            }

            if (target.IsImmune)
                return;

            int knocks = _config.KnocksForHit(Charge01, IsCritical);
            Vector3 direction = _rb.linearVelocity.normalized;
            target.TakeKnock(knocks, direction, Charge01);
            GoLoose(transform.position);
        }

        /// <summary>
        /// Resolves a clamp into one of its two tiers (8.2).
        /// </summary>
        /// <remarks>
        /// PERFECT locks the core on with its charge intact and staggers the thrower. LATE stops the
        /// core dead but drops it loose at the clamper's feet - no possession, no charge, and the
        /// heat starts bleeding. Both events carry the tier so heat and feedback can tell them apart.
        /// </remarks>
        void Clamp(IBallTarget target, Fighters.ClampTier tier)
        {
            _stallFailsafe.Stop();

            int thrower = _throwerSlot;
            float charge = Charge01;

            SetState(BallState.Caught);
            EventBus<BallCaught>.Raise(new BallCaught(target.Slot, charge, transform.position, tier));

            if (tier == Fighters.ClampTier.Late)
            {
                // Stopped, but not yours. Whoever reaches it first takes the tempo.
                target.NotifyClampResolved();
                GoLoose(target.CenterPosition);
                return;
            }

            StunThrower(thrower);
            Attach(target, charge, wasCaught: true);
        }

        void StunThrower(int throwerSlot)
        {
            if (throwerSlot < 0 || _config.PerfectClampStun <= 0f) return;

            IBallTarget victim = BallTargetRegistry.Find(throwerSlot);
            victim?.ApplyStun(_config.PerfectClampStun);
        }

        void Attach(IBallTarget target, float charge01, bool wasCaught)
        {
            _carrier = target;
            Charge01 = Mathf.Clamp01(charge01);
            _throwerSlot = -1;
            _flashedSlotMask = 0;
            _stallFailsafe.Stop();

            if (!_rb.isKinematic)
            {
                _rb.linearVelocity = Vector3.zero;
                _rb.angularVelocity = Vector3.zero;
            }

            _rb.isKinematic = true;

            // A held ball is parented under a fighter that has its own rigidbody. Switching off
            // collision detection as well as the colliders keeps the nested body fully inert.
            _rb.detectCollisions = false;

            // Interpolation has to go off before the ball is parented. Unity writes an interpolated
            // world-space pose into the transform every frame, which overrides the parent-relative
            // placement entirely - the ball hangs near where it was picked up while the fighter runs
            // off. It is turned back on for the throw, which is the only time it is needed.
            _rb.interpolation = RigidbodyInterpolation.None;
            _body.enabled = false;
            _grabTrigger.SetActive(false);

            transform.SetParent(target.HandAnchor, worldPositionStays: false);
            transform.localPosition = Vector3.zero;
            transform.localRotation = Quaternion.identity;

            SetState(BallState.Held);
            target.ReceiveBall(this, Charge01, wasCaught);
            EventBus<BallPossessionChanged>.Raise(new BallPossessionChanged(target.Slot, wasCaught));
        }

        void Detach()
        {
            transform.SetParent(null, worldPositionStays: true);
            _rb.isKinematic = false;
            _rb.detectCollisions = true;
            _body.enabled = true;
            _carrier?.ReleaseBall();
            _carrier = null;
        }

        /// <summary>Drops the ball to the ground, grabbable and inert.</summary>
        public void GoLoose(Vector3 position)
        {
            if (_carrier != null) Detach();
            else transform.SetParent(null, worldPositionStays: true);

            _stallFailsafe.Stop();
            _throwerSlot = -1;
            _flashedSlotMask = 0;
            _bounces = 0;
            Charge01 = 0f;

            // Velocities are cleared while the body is still dynamic. Writing them after the
            // switch to kinematic is ignored and warns once per round reset.
            if (!_rb.isKinematic)
            {
                _rb.linearVelocity = Vector3.zero;
                _rb.angularVelocity = Vector3.zero;
            }

            _rb.isKinematic = true;
            _rb.detectCollisions = true;
            // A resting ball does not need interpolation, and leaving it off means a fighter who
            // walks onto it inherits a body that is already safe to parent.
            _rb.interpolation = RigidbodyInterpolation.None;
            _body.enabled = true;

            Vector3 resting = ResolveRestingPosition(position);

            // Both poses are set. Writing only the transform leaves the rigidbody - and therefore
            // the grab trigger's overlap test - sitting at the old position until physics next
            // syncs, which is long enough for a fighter standing on the respawn point to miss it.
            transform.position = resting;
            _rb.position = resting;

            _grabTrigger.SetActive(true);

            SetState(BallState.Loose);
            EventBus<BallPossessionChanged>.Raise(new BallPossessionChanged(-1, wasCaught: false));
        }

        /// <summary>
        /// Finds a spot at rest height that is actually clear of the arena and its props.
        /// </summary>
        /// <remarks>
        /// A loose ball is dropped to rest height wherever it happened to stop. Over a prop that put
        /// it *inside* the prop - frozen, kinematic and unreachable, which reads to a player as the
        /// ball sticking to the crate. The design already asks for a stalled ball to be recovered
        /// (23); this is the same idea applied at the moment the ball goes loose rather than five
        /// seconds later.
        /// </remarks>
        Vector3 ResolveRestingPosition(Vector3 desired)
        {
            int arenaMask = 1 << DeadballLayers.ArenaLayer;

            Vector2 footprint = ArenaSize.sqrMagnitude > 0.01f
                ? ArenaSize
                : new Vector2(_config.ArenaSize, _config.ArenaSize);
            var limit = new Vector2(footprint.x * 0.5f - 1f, footprint.y * 0.5f - 1f);

            Vector3 candidate = Flatten(desired, limit);
            if (IsClear(candidate, arenaMask)) return candidate;

            // Spiral outward: nudging the ball off a crate keeps play where it was, which is far
            // better than yanking it back to the centre every time it clips a prop.
            const int rings = 5;
            const int samples = 12;

            for (int ring = 1; ring <= rings; ring++)
            {
                float radius = ring * 0.75f;

                for (int i = 0; i < samples; i++)
                {
                    float angle = i * Mathf.PI * 2f / samples;
                    Vector3 offset = new(Mathf.Cos(angle) * radius, 0f, Mathf.Sin(angle) * radius);
                    Vector3 probe = Flatten(candidate + offset, limit);

                    if (IsClear(probe, arenaMask)) return probe;
                }
            }

            return Flatten(_arenaCentre, limit);
        }

        bool IsClear(Vector3 point, int arenaMask)
        {
            // The floor is arena geometry too, and the ball rests on it. Shrinking the probe so it
            // cannot reach down to the floor is what keeps this a test for props and walls only.
            float radius = Mathf.Min(_looseClearance, _restHeight - 0.05f);
            return !Physics.CheckSphere(point, radius, arenaMask, QueryTriggerInteraction.Ignore);
        }

        Vector3 Flatten(Vector3 point, Vector2 limit) => new(
            Mathf.Clamp(point.x, _arenaCentre.x - limit.x, _arenaCentre.x + limit.x),
            _restHeight,
            Mathf.Clamp(point.z, _arenaCentre.z - limit.y, _arenaCentre.z + limit.y));

        void OnCollisionEnter(Collision collision)
        {
            if (State != BallState.Flying) return;

            // A ball that touches the floor is a dead ball; only walls and props keep it alive.
            bool hitFloor = collision.contactCount > 0 && collision.GetContact(0).normal.y > 0.5f;
            if (hitFloor)
            {
                GoLoose(transform.position);
                return;
            }

            // The collider bounces at full energy so the physics engine handles the reflection,
            // including corners; the design's 85% retention is applied on top of it.
            _rb.linearVelocity *= _config.WallBounceRetention;
            _flashedSlotMask = 0;
            _bounces++;

            // Announced before the possible GoLoose below, so the flare lands on the impact that
            // actually happened rather than being skipped on the bounce that ends the rally.
            if (collision.contactCount > 0)
            {
                ContactPoint contact = collision.GetContact(0);
                EventBus<BallBounced>.Raise(new BallBounced(
                    contact.point, contact.normal, _rb.linearVelocity.magnitude, _bounces));
            }

            if (_bounces >= _config.BouncesBeforeLoose)
                GoLoose(transform.position);
        }

        void UpdateFlashCue()
        {
            var targets = BallTargetRegistry.Targets;
            for (int i = 0; i < targets.Count; i++)
            {
                IBallTarget target = targets[i];
                if (!target.IsInPlay) continue;

                int bit = 1 << target.Slot;
                if ((_flashedSlotMask & bit) != 0) continue;

                if (target.Slot == _throwerSlot && Time.time - _throwTime < _config.SelfHitImmunity)
                    continue;

                Vector3 toTarget = target.CenterPosition - transform.position;
                float distance = toTarget.magnitude;
                if (distance < 0.0001f) continue;

                float closingSpeed = Vector3.Dot(_rb.linearVelocity, toTarget / distance);
                if (closingSpeed <= 0.01f) continue;

                float timeToArrival = (distance - target.CatchRadius) / closingSpeed;
                if (timeToArrival > _config.CatchFlashLead) continue;

                _flashedSlotMask |= bit;
                FlashCueFired?.Invoke();
                EventBus<BallFlashCue>.Raise(new BallFlashCue(target.Slot, transform.position));
            }
        }

        void OnStallFailsafeElapsed()
        {
            // Stop() also fires this, so only a genuine expiry should recentre the ball (23).
            if (_stallFailsafe.IsFinished && State == BallState.Flying)
                GoLoose(_arenaCentre);
        }

        void SetState(BallState next)
        {
            if (State == next) return;

            BallState previous = State;
            State = next;
            StateChanged?.Invoke(previous, next);
        }
    }
}
