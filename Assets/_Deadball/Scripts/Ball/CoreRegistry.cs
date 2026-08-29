using System.Collections.Generic;
using UnityEngine;

namespace Deadball.Ball
{
    /// <summary>
    /// Every core currently on the deck.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The game was built around section 6.1's hard rule that there is exactly one core, so a dozen
    /// systems held a single <see cref="BallController"/> reference. Allowing more than one meant
    /// giving them somewhere to ask instead - the same shape as
    /// <see cref="BallTargetRegistry"/>, which already solved this for fighters.
    /// </para>
    /// <para>
    /// Cores add themselves on enable, so a spawner does not have to tell anyone it made one.
    /// </para>
    /// </remarks>
    public static class CoreRegistry
    {
        static readonly List<BallController> s_cores = new(4);

        public static IReadOnlyList<BallController> Cores => s_cores;

        public static void Register(BallController core)
        {
            if (core != null && !s_cores.Contains(core)) s_cores.Add(core);
        }

        public static void Deregister(BallController core) => s_cores.Remove(core);

        /// <summary>The core nearest <paramref name="position"/>, or null if the deck is empty.</summary>
        public static BallController Nearest(Vector3 position, System.Func<BallController, bool> filter = null)
        {
            BallController best = null;
            float bestDistance = float.MaxValue;

            for (int i = 0; i < s_cores.Count; i++)
            {
                BallController core = s_cores[i];
                if (core == null || (filter != null && !filter(core))) continue;

                float distance = (core.transform.position - position).sqrMagnitude;
                if (distance >= bestDistance) continue;

                bestDistance = distance;
                best = core;
            }

            return best;
        }

        /// <summary>True while any core is in flight, which is the only dangerous state (5).</summary>
        public static bool AnyFlying()
        {
            for (int i = 0; i < s_cores.Count; i++)
                if (s_cores[i] != null && s_cores[i].State == BallState.Flying) return true;

            return false;
        }

        /// <summary>The core this slot is carrying, if any.</summary>
        public static BallController HeldBy(int slot)
        {
            for (int i = 0; i < s_cores.Count; i++)
                if (s_cores[i] != null && s_cores[i].HolderSlot == slot) return s_cores[i];

            return null;
        }
    }
}
