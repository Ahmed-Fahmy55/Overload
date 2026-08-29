using System.Collections.Generic;
using Deadball.Match;
using Sirenix.OdinInspector;
using UnityEngine;

namespace Deadball.Ball
{
    /// <summary>
    /// Puts the requested number of cores on the deck.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The scene ships with one core, which stays the template. Anything past the first is cloned
    /// from it, so every core is identical without a separate prefab to keep in step - the scene
    /// core already carries its plasma, aura, fuse and presenters, and a duplicate inherits all of
    /// it.
    /// </para>
    /// <para>
    /// Extra cores are spread around the centre rather than stacked on it: dropping four in the
    /// same spot would have them resolve their overlap by firing off in random directions.
    /// </para>
    /// </remarks>
    public class CoreSpawner : MonoBehaviour
    {
        [Title("References")]
        [Required, SerializeField] BallController _template;
        [Required, SerializeField] MatchSettings _settings;

        [Title("Placement")]
        [Tooltip("How far from the centre the extra cores sit at round start.")]
        [SuffixLabel("m", true), MinValue(0.5f), SerializeField] float _spreadRadius = 2.5f;

        [Title("Runtime")]
        [ShowInInspector, ReadOnly]
        public int ActiveCount => _cores.Count;

        readonly List<BallController> _cores = new(MatchSettings.MaxBallCount);

        void Awake() => Build();

        /// <summary>Matches the number of live cores to the setting.</summary>
        [Button("Rebuild"), DisableInEditorMode]
        public void Build()
        {
            _cores.Clear();
            _cores.Add(_template);

            int wanted = _settings != null ? _settings.BallCount : 1;

            // Clones are made once and kept. Destroying and respawning between rounds would break
            // every scene reference anything else holds to a core.
            for (int i = 1; i < wanted; i++)
            {
                BallController clone = Instantiate(_template, _template.transform.parent);
                clone.name = _template.name + " (" + i + ")";
                _cores.Add(clone);
            }
        }

        /// <summary>Places every core for a fresh round, spread around the centre.</summary>
        public void ResetForRound(Vector3 centre, Vector2 arenaSize)
        {
            for (int i = 0; i < _cores.Count; i++)
            {
                BallController core = _cores[i];
                if (core == null) continue;

                core.ArenaSize = arenaSize;
                core.ResetForRound(_cores.Count == 1 ? centre : PositionFor(i, centre));
            }
        }

        Vector3 PositionFor(int index, Vector3 centre)
        {
            // Evenly around a ring, starting at the far side so the first core still reads as
            // "the one in the middle" from either spawn corner.
            float angle = index / (float)_cores.Count * Mathf.PI * 2f;
            return centre + new Vector3(Mathf.Sin(angle), 0f, Mathf.Cos(angle)) * _spreadRadius;
        }
    }
}
