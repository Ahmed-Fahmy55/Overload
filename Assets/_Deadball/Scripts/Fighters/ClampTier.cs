namespace Deadball.Fighters
{
    /// <summary>
    /// How well a clamp was timed (OVERLOAD GDD section 8.2).
    /// </summary>
    /// <remarks>
    /// The two tiers are what give Rally Heat its decision. A PERFECT clamp wins the exchange and
    /// makes the next one deadlier; a LATE clamp saves your life and starts the core cooling, but
    /// hands the tempo to whoever reaches the loose core first. The mercy tier is the cooling tier.
    /// </remarks>
    public enum ClampTier
    {
        /// <summary>No window open, or the window has expired.</summary>
        None,

        /// <summary>Arrived inside the first band: possession, charge preserved, thrower stunned.</summary>
        Perfect,

        /// <summary>Arrived in the remainder: the core is stopped, but drops loose at your feet.</summary>
        Late
    }
}
