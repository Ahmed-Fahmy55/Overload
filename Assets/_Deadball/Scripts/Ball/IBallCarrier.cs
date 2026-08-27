using UnityEngine;

namespace Deadball.Ball
{
    /// <summary>
    /// What the ball needs from whoever is holding it, and nothing more.
    /// </summary>
    /// <remarks>
    /// Keeping this narrow is what lets the ball stay a single self-contained system: it can be
    /// picked up, carried and caught without knowing that a carrier also moves, dodges or dies.
    /// </remarks>
    public interface IBallCarrier
    {
        int Slot { get; }

        /// <summary>Where the ball parents itself while HELD.</summary>
        Transform HandAnchor { get; }

        /// <summary>False while knocked out, mid-dodge, or otherwise unable to take possession.</summary>
        bool CanTakeBall { get; }

        /// <summary>True only during the 0.30s active catch window (8.1).</summary>
        bool IsCatchWindowActive { get; }

        /// <summary>Hands the ball over, preserving the charge it was thrown at (8.5).</summary>
        void ReceiveBall(BallController ball, float charge01, bool wasCaught);

        /// <summary>Called when the ball leaves this carrier for any reason.</summary>
        void ReleaseBall();
    }
}
