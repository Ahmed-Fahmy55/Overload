using Deadball.Match;
using Sirenix.OdinInspector;
using UnityEngine;

namespace Deadball.HUD
{
    /// <summary>
    /// Puts the chosen quality tier onto Unity's quality settings.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Three tiers, not Unity's six. The built-in names (Fastest, Fast, Simple, Good, Beautiful,
    /// Fantastic) mean nothing to a player, and on a deck this simple the middle four are
    /// indistinguishable - so the options offered are the three that actually look different.
    /// </para>
    /// <para>
    /// Applied on enable as well as on change, because the setting has to survive the trip from the
    /// menu into an arena scene: <see cref="QualitySettings"/> is not serialized with the scene, so
    /// without this the arena would load at whatever tier the project was last built with.
    /// </para>
    /// </remarks>
    public class QualitySettingsApplier : MonoBehaviour
    {
        [Required, SerializeField] MatchSettings _settings;

        [Tooltip("Quality rows to keep in step with the saved value.")]
        [SerializeField] MenuSelectorRow[] _rows;

        /// <summary>Which of Unity's six levels each tier maps to.</summary>
        /// <remarks>
        /// Fast / Good / Fantastic. Fastest is skipped deliberately: it drops shadows entirely, and
        /// the runners' shadows are the only thing anchoring them to the deck.
        /// </remarks>
        static readonly int[] UnityLevels = { 1, 3, 5 };

        [Title("Runtime"), ShowInInspector, ReadOnly]
        public int Tier => _settings != null ? _settings.QualityTier : 2;

        void OnEnable()
        {
            Apply();

            if (_settings == null || _rows == null) return;

            foreach (MenuSelectorRow row in _rows)
                if (row != null) row.SetIndexSilently(_settings.QualityTier);
        }

        /// <summary>Takes a zero-based row index, since that is what an option row reports.</summary>
        public void SetQualityIndex(int index)
        {
            if (_settings == null) return;

            _settings.QualityTier = index;
            Apply();

#if UNITY_EDITOR
            // Otherwise the choice is lost when play mode ends, which makes it look broken while
            // it is being tuned in the editor.
            UnityEditor.EditorUtility.SetDirty(_settings);
#endif
        }

        void Apply()
        {
            if (_settings == null) return;

            int level = UnityLevels[Mathf.Clamp(_settings.QualityTier, 0, UnityLevels.Length - 1)];

            // Without the expensive changes the switch skips reapplying render pipeline assets,
            // which is exactly the part that has to change for the tier to mean anything.
            QualitySettings.SetQualityLevel(level, applyExpensiveChanges: true);
        }
    }
}
