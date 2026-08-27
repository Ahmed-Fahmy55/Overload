using UnityEngine;

namespace Deadball.Fighters
{
    /// <summary>
    /// The four inputs from GDD section 12, decoupled from where they came from.
    /// </summary>
    /// <remarks>
    /// This is the seam that makes the Day 2 AI cheap: <c>Fighter</c> and its parts never learn
    /// whether a human or a state machine is driving them, so the AI ships as one more implementation
    /// of this interface rather than as a parallel control path.
    /// </remarks>
    public interface IFighterInput
    {
        /// <summary>Desired move direction on the arena plane, magnitude 0..1.</summary>
        Vector2 Move { get; }

        /// <summary>True for as long as the throw button is held.</summary>
        bool ThrowHeld { get; }

        /// <summary>True on the frame the dodge button went down.</summary>
        bool DodgePressed { get; }

        /// <summary>True on the frame the catch button went down. Tap only, never a hold (8.1).</summary>
        bool CatchPressed { get; }

        /// <summary>Drops any buffered presses. Called when control is taken away between rounds.</summary>
        void Clear();
    }
}
