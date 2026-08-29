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

        [Title("Runtime"), ShowInInspector, ReadOnly]
        public int BallCount => _settings != null ? _settings.BallCount : 1;

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
