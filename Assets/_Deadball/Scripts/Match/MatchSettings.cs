using Deadball.AI;
using Sirenix.OdinInspector;
using UnityEngine;

namespace Deadball.Match
{
    /// <summary>What the menu chose, carried into the arena scene (OVERLOAD GDD section 21).</summary>
    public enum MatchMode
    {
        Solo,
        LocalVersus
    }

    /// <summary>
    /// The handful of choices the five screens exist to collect.
    /// </summary>
    /// <remarks>
    /// <para>
    /// A ScriptableObject rather than statics or PlayerPrefs: it survives the scene load between menu
    /// and arena, it is inspectable while debugging, and an arena scene opened directly still has
    /// sensible values - which matters because both decks are played from the editor constantly.
    /// </para>
    /// <para>
    /// Deliberately tiny. Section 21 allows five screens and this is everything they collect.
    /// </para>
    /// </remarks>
    [CreateAssetMenu(menuName = "Overload/Match Settings", fileName = "MatchSettings")]
    public class MatchSettings : ScriptableObject
    {
        [Title("Mode")]
        [EnumToggleButtons, SerializeField] MatchMode _mode = MatchMode.LocalVersus;

        [Title("Arena", "15.3 - a match is played on one deck")]
        [Tooltip("Scene name of the chosen deck.")]
        [SerializeField] string _arenaScene = "Arena_Greybox";

        [SerializeField] string _arenaDisplayName = "SECTOR 9";

        [Title("Solo Difficulty", "13.3")]
        [Tooltip("Only used in Solo. One float, three tiers.")]
        [SerializeField] AiProfile _aiProfile;

        public MatchMode Mode { get => _mode; set => _mode = value; }
        public string ArenaScene => _arenaScene;
        public string ArenaDisplayName => _arenaDisplayName;
        public AiProfile AiProfile { get => _aiProfile; set => _aiProfile = value; }

        /// <summary>Picks the deck this match is played on.</summary>
        public void SetArena(string sceneName, string displayName)
        {
            _arenaScene = sceneName;
            _arenaDisplayName = displayName;
        }
    }
}
