using Deadball.Config;
using Sirenix.OdinInspector;
using UnityEngine;

namespace Deadball.Match
{
    /// <summary>
    /// The fixed points of the lot: centre, and one spawn per slot.
    /// </summary>
    /// <remarks>
    /// Round setup asks this rather than the scene, so the district re-skins planned for Day 3 can
    /// swap every prop and material as long as they keep these markers where they are.
    /// </remarks>
    public class ArenaReferences : MonoBehaviour
    {
        [Required, SerializeField] Transform _centre;

        [Tooltip("One per slot, at opposite corners (10).")]
        [Required, SerializeField] Transform[] _spawnPoints;

        [Required, SerializeField] MatchConfig _config;

        public Vector3 Centre => _centre.position;
        public int SpawnCount => _spawnPoints.Length;

        /// <summary>
        /// Where slot <paramref name="slot"/> starts the round, facing the middle.
        /// </summary>
        /// <param name="handicapped">
        /// True for the fighter who lost the previous round: they start closer to the centre ball.
        /// Small enough to feel fair, real enough to keep round 3 alive (10).
        /// </param>
        public void GetSpawn(int slot, bool handicapped, out Vector3 position, out Quaternion rotation)
        {
            Transform point = _spawnPoints[Mathf.Abs(slot) % _spawnPoints.Length];
            position = point.position;

            Vector3 toCentre = Centre - position;
            toCentre.y = 0f;

            if (handicapped && _config.ComebackHandicap > 0f && toCentre.magnitude > _config.ComebackHandicap)
                position += toCentre.normalized * _config.ComebackHandicap;

            rotation = toCentre.sqrMagnitude > 0.0001f
                ? Quaternion.LookRotation(toCentre.normalized)
                : point.rotation;
        }
    }
}
