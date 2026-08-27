using Deadball.Fighters;
using UnityEngine;

namespace Deadball.Ball
{
    /// <summary>
    /// A fighter as the ball sees it: something that can hold it, catch it, or be knocked by it.
    /// </summary>
    /// <remarks>
    /// The ball resolves every interaction through this, so it never references the fighter class or
    /// any of its parts. That is what keeps the Day 2 AI a drop-in: an AI-driven fighter is the same
    /// target.
    /// </remarks>
    public interface IBallTarget : IBallCarrier, IKnockable
    {
        /// <summary>Chest-height centre, used for flight prediction rather than the feet pivot.</summary>
        Vector3 CenterPosition { get; }

        /// <summary>Radius of the volume that resolves a hit or a catch.</summary>
        float CatchRadius { get; }

        /// <summary>False once knocked out of the round.</summary>
        bool IsInPlay { get; }

        /// <summary>Which clamp tier this target is currently offering (8.2).</summary>
        Fighters.ClampTier ClampTier { get; }

        /// <summary>Takes the thrower's stun after being beaten by a perfect clamp.</summary>
        void ApplyStun(float seconds);

        /// <summary>Closes the clamp window without a lockout, even when nothing was gained.</summary>
        void NotifyClampResolved();
    }
}
