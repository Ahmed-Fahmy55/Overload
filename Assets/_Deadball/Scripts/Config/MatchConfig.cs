using Sirenix.OdinInspector;
using UnityEngine;

namespace Deadball.Config
{
    /// <summary>
    /// Every tunable number from GDD section 9, in one asset.
    /// </summary>
    /// <remarks>
    /// The design doc is explicit that this game is won or lost on tuning rather than on content, so
    /// the numbers live in a single asset that both modes read. Solo and Local Versus deliberately
    /// share one instance - balancing them separately doubles the Day 3 workload (section 11.3).
    /// </remarks>
    [CreateAssetMenu(menuName = "Deadball/Match Config", fileName = "MatchConfig")]
    public class MatchConfig : ScriptableObject
    {
        [TabGroup("Tuning", "Movement")]
        [Title("Movement", "7.1 - one speed, no sprint, no stamina")]
        [SuffixLabel("m/s", true), MinValue(0.1f)]
        [SerializeField] float _moveSpeed = 6f;

        [TabGroup("Tuning", "Movement")]
        [PropertyRange(0f, 1f), LabelText("Holding Speed Multiplier")]
        [InfoBox("The cost of possession (7.2). This is what stops the holder kiting forever.")]
        [SerializeField] float _holdingSpeedMultiplier = 0.8f;

        [TabGroup("Tuning", "Movement")]
        [SuffixLabel("deg/s", true), MinValue(0f)]
        [SerializeField] float _turnSpeed = 900f;

        [TabGroup("Tuning", "Dodge")]
        [Title("Dodge Roll", "7.4 - the safe answer to an incoming ball")]
        [SuffixLabel("m/s", true), MinValue(0.1f)]
        [SerializeField] float _dodgeSpeed = 12f;

        [TabGroup("Tuning", "Dodge")]
        [SuffixLabel("s", true), MinValue(0.01f)]
        [SerializeField] float _dodgeDuration = 0.25f;

        [TabGroup("Tuning", "Dodge")]
        [SuffixLabel("s", true), MinValue(0f)]
        [SerializeField] float _dodgeInvulnerability = 0.20f;

        [TabGroup("Tuning", "Dodge")]
        [SuffixLabel("s", true), MinValue(0f)]
        [SerializeField] float _dodgeCooldown = 1.2f;

        [TabGroup("Tuning", "Throw")]
        [Title("Charge and Throw", "7.3 - being rooted while charging is the entire risk")]
        [SuffixLabel("s", true), MinValue(0.01f)]
        [SerializeField] float _maxChargeTime = 1.2f;

        [TabGroup("Tuning", "Throw")]
        [SuffixLabel("m/s", true), MinValue(0.1f)]
        [SerializeField] float _minThrowSpeed = 12f;

        [TabGroup("Tuning", "Throw")]
        [SuffixLabel("m/s", true), MinValue(0.1f)]
        [SerializeField] float _maxThrowSpeed = 28f;

        [TabGroup("Tuning", "Throw")]
        [SuffixLabel("deg", true), PropertyRange(0f, 45f), LabelText("Aim Soft Snap")]
        [InfoBox("Near-misses read as intentional rather than as bad controls (7.3).")]
        [SerializeField] float _aimSnapAngle = 10f;

        [TabGroup("Tuning", "Throw")]
        [SuffixLabel("m/s2", true), MinValue(0f), LabelText("Ball Gravity")]
        [InfoBox("Very low on purpose - this is the 'kinematic-ish' flight the design asks for (6.3). "
            + "At real gravity a min-charge throw hits the floor around 11m and never reaches the far "
            + "wall, which kills the ricochets that generate most of the game's chaos (15).")]
        [SerializeField] float _ballGravity = 0.9f;

        [TabGroup("Tuning", "Throw")]
        [SuffixLabel("s", true), MinValue(0f), LabelText("Self-Hit Immunity")]
        [SerializeField] float _selfHitImmunity = 0.4f;

        [TabGroup("Tuning", "Catch")]
        [Title("Catch", "8 - the mechanic the whole game rests on")]
        [SuffixLabel("s", true), MinValue(0.01f), LabelText("Active Window")]
        [SerializeField] float _catchWindow = 0.30f;

        [TabGroup("Tuning", "Catch")]
        [SuffixLabel("s", true), MinValue(0.01f), LabelText("Flash Lead Time")]
        [InfoBox("Layer 3 of the telegraph. This is the beat the player presses on - never cut it.")]
        [SerializeField] float _catchFlashLead = 0.35f;

        [TabGroup("Tuning", "Catch")]
        [SuffixLabel("s", true), MinValue(0.01f), LabelText("Perfect Band")]
        [InfoBox("The first slice of the window (8.2). Arrive inside it and the core locks on with "
            + "charge preserved; arrive after it and you get the LATE tier - the core stops but drops "
            + "loose at your feet. The mercy tier is also the cooling tier.")]
        [SerializeField] float _perfectClampBand = 0.12f;

        [TabGroup("Tuning", "Catch")]
        [SuffixLabel("s", true), MinValue(0f), LabelText("Perfect Clamp Stun")]
        [SerializeField] float _perfectClampStun = 0.35f;

        [TabGroup("Tuning", "Catch")]
        [SuffixLabel("s", true), MinValue(0f), LabelText("Lockout On Miss")]
        [InfoBox("$LockoutAdvice", InfoMessageType.Warning)]
        [SerializeField] float _catchLockout = 0.6f;

        [TabGroup("Tuning", "Heat")]
        [Title("Hold Fuse", "The core cooks whoever is carrying it")]
        [SuffixLabel("s", true), MinValue(0.5f), LabelText("Hold Fuse")]
        [InfoBox("How long the core can be carried before it detonates and takes the holder with "
            + "it. Without this, a player who is ahead can simply pick the core up and run out the "
            + "clock - nobody can take it off them and nobody can hurt them while they hold it.")]
        [SerializeField] float _holdFuseSeconds = 5f;

        [TabGroup("Tuning", "Heat")]
        [SuffixLabel("s", true), MinValue(0.2f), LabelText("Hold Fuse At Critical")]
        [Tooltip("An unstable core gives you far less time to find a target.")]
        [SerializeField] float _holdFuseCriticalSeconds = 3f;

        [TabGroup("Tuning", "Heat")]
        [PropertyRange(0f, 1f), LabelText("Warn Below")]
        [Tooltip("Fraction of the fuse left when the danger triangles start showing.")]
        [SerializeField] float _fuseWarningFraction = 0.6f;

        [TabGroup("Tuning", "Heat")]
        [Title("Rally Heat", "16 - the longer you keep the core alive, the likelier it is to kill you")]
        [MinValue(0f), LabelText("Gain Per Perfect Clamp")]
        [SerializeField] float _heatPerPerfectClamp = 22f;

        [TabGroup("Tuning", "Heat")]
        [SuffixLabel("per sec", true), MinValue(0f), LabelText("Decay While Loose")]
        [InfoBox("Heat only bleeds off while the core is LOOSE on the deck. That is what makes "
            + "deliberately letting it go a real option (16.1).")]
        [SerializeField] float _heatDecayPerSecond = 25f;

        [TabGroup("Tuning", "Heat")]
        [MinValue(1f), LabelText("Critical Threshold")]
        [SerializeField] float _criticalHeat = 80f;

        [TabGroup("Tuning", "Heat")]
        [MinValue(1f), LabelText("Max Heat")]
        [SerializeField] float _maxHeat = 100f;

        [TabGroup("Tuning", "Ball")]
        [Title("Ball", "6.3 - there is exactly one")]
        [PropertyRange(0f, 1f), LabelText("Wall Bounce Energy")]
        [SerializeField] float _wallBounceRetention = 0.85f;

        [TabGroup("Tuning", "Ball")]
        [MinValue(1), LabelText("Bounces Before Loose")]
        [SerializeField] int _bouncesBeforeLoose = 2;

        [TabGroup("Tuning", "Ball")]
        [SuffixLabel("m/s", true), MinValue(0f), LabelText("Loose Speed Threshold")]
        [SerializeField] float _looseSpeedThreshold = 3f;

        [TabGroup("Tuning", "Ball")]
        [SuffixLabel("s", true), MinValue(0.5f), LabelText("Stall Failsafe")]
        [InfoBox("A flying ball that has not resolved by now is teleported to centre (23).")]
        [SerializeField] float _stallFailsafe = 5f;

        [TabGroup("Tuning", "Ball")]
        [SuffixLabel("m", true), MinValue(0.01f), LabelText("Pickup Magnetise Radius")]
        [SerializeField] float _pickupRadius = 0.9f;

        [TabGroup("Tuning", "Round")]
        [Title("Round and Match", "10")]
        [MinValue(1), LabelText("Knocks To KO")]
        [SerializeField] int _knocksToKo = 2;

        [TabGroup("Tuning", "Round")]
        [PropertyRange(0f, 1f), LabelText("Instant KO Charge")]
        [InfoBox("A throw charged at or above this lands a KO in one hit (9).")]
        [SerializeField] float _instantKoCharge = 0.95f;

        [TabGroup("Tuning", "Round")]
        [SuffixLabel("s", true), MinValue(5f)]
        [SerializeField] float _roundDuration = 60f;

        [TabGroup("Tuning", "Round")]
        [MinValue(1), LabelText("Round Wins To Take Match")]
        [SerializeField] int _roundWinsToTakeMatch = 2;

        [TabGroup("Tuning", "Round")]
        [SuffixLabel("s", true), MinValue(0f), LabelText("Round Intro Card")]
        [SerializeField] float _roundIntroDuration = 2f;

        [TabGroup("Tuning", "Round")]
        [Title("Overtime")]
        [SerializeField] bool _overtimeEnabled = true;

        [TabGroup("Tuning", "Round")]
        [ShowIf("_overtimeEnabled"), LabelText("Ball Speed Bonus"), PropertyRange(0f, 1f)]
        [SerializeField] float _overtimeSpeedBonus = 0.25f;

        [TabGroup("Tuning", "Round")]
        [ShowIf("_overtimeEnabled"), LabelText("Knocks Remaining"), MinValue(1)]
        [SerializeField] int _overtimeKnocksRemaining = 1;

        [TabGroup("Tuning", "Arena")]
        [Title("Arena", "15 - one flat lot, walls on all sides")]
        [SuffixLabel("m", true), MinValue(5f)]
        [SerializeField] float _arenaSize = 20f;

        [TabGroup("Tuning", "Arena")]
        [SuffixLabel("m", true), MinValue(0f), LabelText("Comeback Handicap")]
        [InfoBox("The fighter who lost the previous round spawns this much closer to centre (10).")]
        [SerializeField] float _comebackHandicap = 2f;

        public float MoveSpeed => _moveSpeed;
        public float HoldingSpeedMultiplier => _holdingSpeedMultiplier;
        public float TurnSpeed => _turnSpeed;

        public float DodgeSpeed => _dodgeSpeed;
        public float DodgeDuration => _dodgeDuration;
        public float DodgeInvulnerability => _dodgeInvulnerability;
        public float DodgeCooldown => _dodgeCooldown;

        public float MaxChargeTime => _maxChargeTime;
        public float MinThrowSpeed => _minThrowSpeed;
        public float MaxThrowSpeed => _maxThrowSpeed;
        public float AimSnapAngle => _aimSnapAngle;
        public float BallGravity => _ballGravity;
        public float SelfHitImmunity => _selfHitImmunity;

        public float CatchWindow => _catchWindow;
        public float PerfectClampBand => Mathf.Min(_perfectClampBand, _catchWindow);
        public float PerfectClampStun => _perfectClampStun;

        public float HeatPerPerfectClamp => _heatPerPerfectClamp;
        public float HeatDecayPerSecond => _heatDecayPerSecond;
        public float CriticalHeat => _criticalHeat;
        public float MaxHeat => _maxHeat;
        public float CatchFlashLead => _catchFlashLead;
        public float CatchLockout => _catchLockout;

        public float WallBounceRetention => _wallBounceRetention;
        public int BouncesBeforeLoose => _bouncesBeforeLoose;
        public float LooseSpeedThreshold => _looseSpeedThreshold;
        public float FuseWarningFraction => _fuseWarningFraction;

        /// <summary>How long the core tolerates being carried at the given heat (16).</summary>
        /// <remarks>
        /// Scaled by heat rather than switched at the CRITICAL threshold, so the pressure builds
        /// through a rally instead of arriving as a step change the player cannot anticipate.
        /// </remarks>
        public float HoldFuseFor(float heat01) =>
            Mathf.Lerp(_holdFuseSeconds, _holdFuseCriticalSeconds, Mathf.Clamp01(heat01));
        public float StallFailsafe => _stallFailsafe;
        public float PickupRadius => _pickupRadius;

        public int KnocksToKo => _knocksToKo;
        public float InstantKoCharge => _instantKoCharge;
        public float RoundDuration => _roundDuration;
        public int RoundWinsToTakeMatch => _roundWinsToTakeMatch;
        public float RoundIntroDuration => _roundIntroDuration;

        public bool OvertimeEnabled => _overtimeEnabled;
        public float OvertimeSpeedBonus => _overtimeSpeedBonus;
        public int OvertimeKnocksRemaining => _overtimeKnocksRemaining;

        public float ArenaSize => _arenaSize;
        public float ComebackHandicap => _comebackHandicap;

        /// <summary>Throw speed for a normalised charge, optionally boosted for overtime.</summary>
        public float ThrowSpeedFor(float charge01, bool overtime = false)
        {
            float speed = Mathf.Lerp(_minThrowSpeed, _maxThrowSpeed, Mathf.Clamp01(charge01));
            return overtime && _overtimeEnabled ? speed * (1f + _overtimeSpeedBonus) : speed;
        }

        /// <summary>
        /// Knocks dealt by a hit (9).
        /// </summary>
        /// <remarks>
        /// A max-charge throw ends it, and so does any contact while the core is critical - that is
        /// the whole point of letting heat climb.
        /// </remarks>
        public int KnocksForHit(float charge01, bool coreIsCritical = false) =>
            coreIsCritical || charge01 >= _instantKoCharge ? _knocksToKo : 1;

        string LockoutAdvice =>
            "The most important number in the game (9). Tune this before touching anything else - "
            + "too generous and catching is free, too harsh and nobody presses it.";
    }
}
