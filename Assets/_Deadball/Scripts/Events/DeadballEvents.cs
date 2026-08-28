using Core.Events;
using UnityEngine;

namespace Deadball.Events
{
    /// <summary>The reason a round stopped.</summary>
    public enum RoundEndReason
    {
        KnockOut,
        TimeExpired,
        Draw
    }

    /// <summary>Raised when the ball changes hands, is thrown, or goes loose.</summary>
    /// <remarks>
    /// Presentation (ball tint, screen-edge flash, HUD) subscribes to this instead of polling the
    /// ball, which keeps the ball ignorant of everything that wants to react to it.
    /// </remarks>
    public readonly struct BallPossessionChanged : IEvent
    {
        /// <summary>Slot of the new holder, or -1 when the ball went loose or was thrown.</summary>
        public readonly int HolderSlot;
        public readonly bool WasCaught;

        public BallPossessionChanged(int holderSlot, bool wasCaught)
        {
            HolderSlot = holderSlot;
            WasCaught = wasCaught;
        }
    }

    /// <summary>Raised the instant a throw is released, carrying the charge it was thrown at.</summary>
    public readonly struct BallThrown : IEvent
    {
        public readonly int ThrowerSlot;
        public readonly float Charge01;

        public BallThrown(int throwerSlot, float charge01)
        {
            ThrowerSlot = throwerSlot;
            Charge01 = charge01;
        }
    }

    /// <summary>Raised the moment a holder begins spinning the core up (19: spin-up loop).</summary>
    public readonly struct ChargeStarted : IEvent
    {
        public readonly int Slot;

        public ChargeStarted(int slot) => Slot = slot;
    }

    /// <summary>Raised when a charge ends without a throw - a dodge cancel, or losing the core.</summary>
    public readonly struct ChargeCancelled : IEvent
    {
        public readonly int Slot;

        public ChargeCancelled(int slot) => Slot = slot;
    }

    /// <summary>
    /// Layer 3 of the catch telegraph (GDD section 8.2) - the "press now" beat.
    /// </summary>
    public readonly struct BallFlashCue : IEvent
    {
        public readonly int TargetSlot;
        public readonly Vector3 BallPosition;

        public BallFlashCue(int targetSlot, Vector3 ballPosition)
        {
            TargetSlot = targetSlot;
            BallPosition = ballPosition;
        }
    }

    /// <summary>Raised on a successful catch. This is the moment the whole game is built around.</summary>
    public readonly struct BallCaught : IEvent
    {
        public readonly int CatcherSlot;
        public readonly float Charge01;
        public readonly Vector3 Position;

        /// <summary>Which tier the clamp landed in (8.2). Only PERFECT adds heat.</summary>
        public readonly Fighters.ClampTier Tier;

        public BallCaught(int catcherSlot, float charge01, Vector3 position, Fighters.ClampTier tier)
        {
            CatcherSlot = catcherSlot;
            Charge01 = charge01;
            Position = position;
            Tier = tier;
        }
    }

    /// <summary>Raised when the core crosses the CRITICAL threshold in either direction (16.3).</summary>
    public readonly struct CriticalStateChanged : IEvent
    {
        public readonly bool IsCritical;
        public readonly float Heat;

        public CriticalStateChanged(bool isCritical, float heat)
        {
            IsCritical = isCritical;
            Heat = heat;
        }
    }

    /// <summary>Raised when a fighter takes one or more knocks but is still standing.</summary>
    public readonly struct FighterKnocked : IEvent
    {
        public readonly int Slot;
        public readonly int KnocksTaken;
        public readonly int KnocksRemaining;
        public readonly float Charge01;
        public readonly Vector3 Position;

        public FighterKnocked(int slot, int knocksTaken, int knocksRemaining, float charge01, Vector3 position)
        {
            Slot = slot;
            KnocksTaken = knocksTaken;
            KnocksRemaining = knocksRemaining;
            Charge01 = charge01;
            Position = position;
        }
    }

    /// <summary>Raised when a fighter runs out of knocks.</summary>
    public readonly struct FighterKnockedOut : IEvent
    {
        public readonly int Slot;
        public readonly Vector3 Position;

        public FighterKnockedOut(int slot, Vector3 position)
        {
            Slot = slot;
            Position = position;
        }
    }

    /// <summary>Raised when a fighter commits to a dodge roll.</summary>
    public readonly struct FighterDodged : IEvent
    {
        public readonly int Slot;
        public readonly Vector3 Position;

        public FighterDodged(int slot, Vector3 position)
        {
            Slot = slot;
            Position = position;
        }
    }

    /// <summary>Raised when a catch attempt expires without connecting, starting the lockout.</summary>
    public readonly struct CatchMissed : IEvent
    {
        public readonly int Slot;
        public readonly float LockoutDuration;

        public CatchMissed(int slot, float lockoutDuration)
        {
            Slot = slot;
            LockoutDuration = lockoutDuration;
        }
    }

    /// <summary>Raised when a fighter claims a slot, so the HUD can build its pips.</summary>
    public readonly struct FighterRegistered : IEvent
    {
        public readonly int Slot;

        public FighterRegistered(int slot) => Slot = slot;
    }

    public readonly struct RoundStarting : IEvent
    {
        public readonly int RoundNumber;
        public readonly float IntroDuration;

        public RoundStarting(int roundNumber, float introDuration)
        {
            RoundNumber = roundNumber;
            IntroDuration = introDuration;
        }
    }

    public readonly struct RoundStarted : IEvent
    {
        public readonly int RoundNumber;

        public RoundStarted(int roundNumber) => RoundNumber = roundNumber;
    }

    public readonly struct RoundEnded : IEvent
    {
        /// <summary>Slot of the round winner, or -1 for a draw.</summary>
        public readonly int WinnerSlot;
        public readonly RoundEndReason Reason;

        public RoundEnded(int winnerSlot, RoundEndReason reason)
        {
            WinnerSlot = winnerSlot;
            Reason = reason;
        }
    }

    public readonly struct OvertimeStarted : IEvent { }

    public readonly struct MatchEnded : IEvent
    {
        public readonly int WinnerSlot;

        public MatchEnded(int winnerSlot) => WinnerSlot = winnerSlot;
    }

    /// <summary>
    /// Raised when a flying core rebounds off the containment field or a prop (section 22).
    /// </summary>
    /// <remarks>
    /// Carries the contact point and normal so the flare can be placed on the surface that was hit,
    /// facing outwards, rather than at the core's centre.
    /// </remarks>
    public readonly struct BallBounced : IEvent
    {
        public readonly Vector3 Position;
        public readonly Vector3 Normal;
        public readonly float Speed;
        public readonly int BounceNumber;

        public BallBounced(Vector3 position, Vector3 normal, float speed, int bounceNumber)
        {
            Position = position;
            Normal = normal;
            Speed = speed;
            BounceNumber = bounceNumber;
        }
    }

    /// <summary>
    /// Raised when the core detonates in a holder's hands because they carried it too long.
    /// </summary>
    public readonly struct CoreDetonated : IEvent
    {
        public readonly int Slot;
        public readonly Vector3 Position;

        public CoreDetonated(int slot, Vector3 position)
        {
            Slot = slot;
            Position = position;
        }
    }

    /// <summary>Raised the instant a wind-up reaches full charge (section 22, ring snap).</summary>
    /// <remarks>
    /// The moment deserves its own event rather than a threshold test in the presenter: max charge
    /// is a commitment the other player must be able to read, and it should fire exactly once.
    /// </remarks>
    public readonly struct ChargeMaxed : IEvent
    {
        public readonly int Slot;
        public readonly Vector3 Position;

        public ChargeMaxed(int slot, Vector3 position)
        {
            Slot = slot;
            Position = position;
        }
    }
}
