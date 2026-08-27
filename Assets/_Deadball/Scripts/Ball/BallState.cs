namespace Deadball.Ball
{
    /// <summary>The ball state machine from GDD section 6.2.</summary>
    public enum BallState
    {
        /// <summary>On the ground, glowing, grabbable by walking over it.</summary>
        Loose,

        /// <summary>Parented to a hand anchor. The holder is slowed and is the only scoring threat.</summary>
        Held,

        /// <summary>In flight. The only dangerous moment in the game.</summary>
        Flying,

        /// <summary>Transitional freeze-frame on a successful catch, then straight back to Held.</summary>
        Caught
    }
}
