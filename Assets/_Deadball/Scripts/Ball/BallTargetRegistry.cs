using System.Collections.Generic;

namespace Deadball.Ball
{
    /// <summary>
    /// The fighters currently in the arena, kept as a list the ball can walk every physics step.
    /// </summary>
    /// <remarks>
    /// The ball needs the other fighters roughly 50 times a second to decide when to fire the flash
    /// cue. A registry populated on enable costs nothing; a scene query would not.
    /// </remarks>
    public static class BallTargetRegistry
    {
        static readonly List<IBallTarget> s_targets = new(2);

        public static IReadOnlyList<IBallTarget> Targets => s_targets;

        public static void Register(IBallTarget target)
        {
            if (target != null && !s_targets.Contains(target))
                s_targets.Add(target);
        }

        public static void Deregister(IBallTarget target)
        {
            if (target != null)
                s_targets.Remove(target);
        }

        public static IBallTarget Find(int slot)
        {
            for (int i = 0; i < s_targets.Count; i++)
            {
                if (s_targets[i].Slot == slot)
                    return s_targets[i];
            }

            return null;
        }

        /// <summary>Domain reload can be disabled in the editor, so the list is cleared explicitly.</summary>
        [UnityEngine.RuntimeInitializeOnLoadMethod(UnityEngine.RuntimeInitializeLoadType.SubsystemRegistration)]
        static void Reset() => s_targets.Clear();
    }
}
