using UnityEngine;

namespace Deadball.Fighters
{
    /// <summary>Something a flying ball can knock.</summary>
    /// <remarks>
    /// Deliberately says nothing about identity or possession - a knock is a knock. Slot ownership
    /// lives on <see cref="Deadball.Ball.IBallCarrier"/> so the two interfaces compose without
    /// colliding on a shared member.
    /// </remarks>
    public interface IKnockable
    {
        /// <summary>True while the target cannot be knocked - dodge i-frames, or already out.</summary>
        bool IsImmune { get; }

        /// <summary>Applies <paramref name="knocks"/> knocks from a ball travelling <paramref name="direction"/>.</summary>
        void TakeKnock(int knocks, Vector3 direction, float charge01);
    }
}
