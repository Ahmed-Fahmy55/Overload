using Deadball.Match;
using Sirenix.OdinInspector;
using UnityEngine;

namespace Deadball.HUD
{
    /// <summary>
    /// Writes gameplay choices into the settings asset.
    /// </summary>
    /// <remarks>
    /// The core count is read by the spawner when a scene loads, so changing it here takes effect
    /// on the next match rather than mid-round - swapping the number of cores under a live rally
    /// would strand whichever one somebody happened to be holding.
    /// </remarks>
    public class GameplaySettingsApplier : MonoBehaviour
    {
        [Required, SerializeField] MatchSettings _settings;

        [Tooltip("Core-count rows to keep in step with the saved value.")]
        [SerializeField] MenuSelectorRow[] _rows;

        [Title("Runtime"), ShowInInspector, ReadOnly]
        public int BallCount => _settings != null ? _settings.BallCount : 1;

        /// <summary>
        /// Shows the saved core count on every row that offers it.
        /// </summary>
        /// <remarks>
        /// The rows were built with whatever the value happened to be at author time, so without
        /// this a screen could open reading "1" while the asset held 4 - and the first nudge of the
        /// row would then jump from a number nobody chose.
        /// </remarks>
        void OnEnable()
        {
            if (_settings == null || _rows == null) return;

            foreach (MenuSelectorRow row in _rows)
                if (row != null) row.SetIndexSilently(_settings.BallCount - 1);
        }

        /// <summary>Takes a zero-based row index, since that is what an option row reports.</summary>
        public void SetBallCountIndex(int index)
        {
            if (_settings == null) return;

            _settings.BallCount = index + 1;

#if UNITY_EDITOR
            // Otherwise the choice is lost when play mode ends, which makes it look broken while
            // it is being tuned in the editor.
            UnityEditor.EditorUtility.SetDirty(_settings);
#endif
        }
    }
}
