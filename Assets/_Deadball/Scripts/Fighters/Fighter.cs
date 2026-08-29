using Core.Events;
using Deadball.Ball;
using Deadball.Events;
using Sirenix.OdinInspector;
using UnityEngine;

namespace Deadball.Fighters
{
    /// <summary>
    /// One fighter: the seam between an input source, the four behaviour parts, and the ball.
    /// </summary>
    /// <remarks>
    /// <para>
    /// This class deliberately contains no rules. Movement lives in <see cref="FighterMotor"/>,
    /// possession in <see cref="FighterThrower"/>, the catch in <see cref="FighterCatcher"/> and
    /// knocks in <see cref="FighterKnocks"/>. What is left here is wiring: read the four inputs,
    /// hand them to the part that owns them, and present a single <see cref="IBallTarget"/> face to
    /// the ball.
    /// </para>
    /// <para>
    /// Because the input arrives as an interface, the Day 2 AI attaches to this same prefab with no
    /// changes to any of the parts.
    /// </para>
    /// </remarks>
    [RequireComponent(typeof(FighterMotor), typeof(FighterThrower))]
    [RequireComponent(typeof(FighterCatcher), typeof(FighterKnocks))]
    public class Fighter : MonoBehaviour, IBallTarget
    {
        [Title("Identity")]
        [ShowInInspector, ReadOnly]
        public int Slot { get; private set; } = -1;

        [Title("Scene References")]
        [Tooltip("Chest-height marker used for flight prediction and the soft aim snap.")]
        [Required, SerializeField] Transform _centre;
        [SuffixLabel("m", true), SerializeField] float _catchRadius = 0.7f;

        [Title("Parts")]
        [Required, SerializeField] FighterMotor _motor;
        [Required, SerializeField] FighterThrower _thrower;
        [Required, SerializeField] FighterCatcher _catcher;
        [Required, SerializeField] FighterKnocks _knocks;

        public FighterMotor Motor => _motor;
        public FighterThrower Thrower => _thrower;
        public FighterCatcher Catcher => _catcher;
        public FighterKnocks Knocks => _knocks;

        public Vector3 CenterPosition => _centre.position;
        public float CatchRadius => _catchRadius;
        public bool IsInPlay => _registered && !_knocks.IsOut;
        public Transform HandAnchor => _thrower.HandAnchor;
        public bool CanTakeBall => IsInPlay && !_thrower.HasBall && !_catcher.IsFumbling;
        public bool IsCatchWindowActive => _catcher.IsWindowActive;
        public ClampTier ClampTier => _catcher.CurrentTier;
        public bool IsImmune => _knocks.IsImmune;

        IFighterInput _input;
        bool _registered;
        bool _controlEnabled = true;

        void Awake() => _knocks.ExternalImmunity = () => _motor.IsInvulnerable;

        void OnEnable() => _knocks.KnockedOut += OnKnockedOut;

        void OnDisable() => _knocks.KnockedOut -= OnKnockedOut;

        void OnDestroy()
        {
            // A held ball is parented to this fighter's hand, so it would be destroyed along with
            // them. Dropping it first keeps the one systemic object in the game alive through a
            // fighter being torn down - a round reset, a mode switch, or a scene reload.
            _thrower.DropBall();
            Deregister();
        }

        void Update()
        {
            if (_input == null || !_controlEnabled) return;

            _motor.SetMoveInput(_input.Move);
            _thrower.Tick(_input.ThrowHeld);

            if (_input.DodgePressed && _motor.TryDodge())
                EventBus<FighterDodged>.Raise(new FighterDodged(Slot, transform.position));

            // A runner carrying a core cannot clamp. It could never take a second one anyway
            // (CanTakeBall is false while holding), so opening a window here only burned the miss
            // lockout on a press that could not succeed. Refusing it outright leaves a carrier two
            // honest answers to an incoming core: dodge, or throw the one they have at it.
            if (_input.CatchPressed && !_thrower.HasBall)
                _catcher.TryOpenWindow();
        }

        /// <summary>
        /// Claims a slot and an input source. Called by the join flow, or by the AI spawner.
        /// </summary>
        public void Bind(int slot, IFighterInput input)
        {
            Slot = slot;
            _input = input;

            _thrower.Initialise(slot);
            _catcher.Initialise(slot);
            _knocks.Initialise(slot);

            if (!_registered)
            {
                BallTargetRegistry.Register(this);
                _registered = true;
                EventBus<FighterRegistered>.Raise(new FighterRegistered(slot));
            }
        }

        /// <summary>Places the fighter for a fresh round and clears every transient state.</summary>
        public void PrepareForRound(Vector3 position, Quaternion rotation, int knocksAllowed = -1)
        {
            _thrower.DropBall();
            _thrower.ClearBall();
            _catcher.ResetState();
            _knocks.ResetForRound(knocksAllowed);
            _motor.Teleport(position, rotation);
            _input?.Clear();
        }

        /// <summary>Hands control to the player, or takes it away between rounds and on a KO.</summary>
        public void SetControlEnabled(bool enabled)
        {
            _controlEnabled = enabled;
            _motor.SetInputEnabled(enabled);
            _thrower.SetEnabled(enabled);
            _catcher.SetEnabled(enabled);
            _input?.Clear();
        }

        public void ReceiveBall(BallController ball, float charge01, bool wasCaught)
        {
            if (wasCaught) _catcher.NotifyCaught();
            _thrower.ReceiveBall(ball, charge01);
        }

        public void ReleaseBall() => _thrower.ClearBall();

        public void ApplyStun(float seconds)
        {
            // A staggered runner drops whatever they were winding up, but keeps the core.
            _thrower.CancelCharge();
            _motor.Stagger(seconds);
        }

        /// <summary>A late clamp still resolved the window, so it must not cost a lockout (8.2).</summary>
        public void NotifyLateClamp() => _catcher.NotifyLateClamp();

        public void TakeKnock(int knocks, Vector3 direction, float charge01) =>
            _knocks.TakeKnock(knocks, direction, charge01);

        void OnKnockedOut()
        {
            _thrower.DropBall();
            SetControlEnabled(false);
        }

        void Deregister()
        {
            if (!_registered) return;

            BallTargetRegistry.Deregister(this);
            _registered = false;
        }
    }
}
