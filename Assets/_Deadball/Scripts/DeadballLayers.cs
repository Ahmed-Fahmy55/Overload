using UnityEngine;

namespace Deadball
{
    /// <summary>
    /// The four physics layers the arena runs on, and the matrix rules that go with them.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Names are prefixed because this project also carries TopDownEngine, which claims a number of
    /// generic layer names of its own.
    /// </para>
    /// <para>
    /// The important rule is that the ball never physically collides with a fighter. Contact is
    /// resolved through trigger volumes instead, so a ball can pass through a dodging fighter without
    /// the physics engine deflecting it, and a catch does not have to fight a bounce impulse.
    /// </para>
    /// </remarks>
    public static class DeadballLayers
    {
        public const string Arena = "DB_Arena";
        public const string Fighter = "DB_Fighter";
        public const string Ball = "DB_Ball";
        public const string Hitbox = "DB_Hitbox";

        public static readonly string[] All = { Arena, Fighter, Ball, Hitbox };

        public static int ArenaLayer => LayerMask.NameToLayer(Arena);
        public static int FighterLayer => LayerMask.NameToLayer(Fighter);
        public static int BallLayer => LayerMask.NameToLayer(Ball);
        public static int HitboxLayer => LayerMask.NameToLayer(Hitbox);

        /// <summary>
        /// Layer pairs that must NOT collide, as (layerA, layerB) name pairs.
        /// </summary>
        /// <remarks>
        /// Applied by the arena builder rather than checked in at runtime, because the collision
        /// matrix lives in ProjectSettings and there is no runtime API worth calling every load.
        /// </remarks>
        public static readonly (string A, string B)[] IgnoredPairs =
        {
            (Ball, Fighter),
            (Hitbox, Hitbox),
            (Hitbox, Arena)
        };
    }
}
