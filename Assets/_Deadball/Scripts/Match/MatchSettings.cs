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

        [Title("Cores", "6.1 - the design says one; this lets a lobby disagree")]
        [Tooltip("How many cores are in play. One is the tuned game: section 6.1 calls a single "
            + "core a hard rule, because every read the player learns is built on there being "
            + "exactly one thing to watch. More is a party mode, not a balance change.")]
        [PropertyRange(1, MaxBallCount), SerializeField] int _ballCount = 2;

        [Title("Audio")]
        [PropertyRange(0f, 1f), SerializeField] float _masterVolume = 1f;
        [PropertyRange(0f, 1f), SerializeField] float _musicVolume = 0.8f;
        [PropertyRange(0f, 1f), SerializeField] float _sfxVolume = 1f;

        [Title("Solo Difficulty", "13.3")]
        [Tooltip("Only used in Solo. One float, three tiers.")]
        [SerializeField] AiProfile _aiProfile;

        /// <summary>The most cores the deck will ever hold.</summary>
        public const int MaxBallCount = 4;

        public MatchMode Mode { get => _mode; set => _mode = value; }

        public int BallCount
        {
            get => Mathf.Clamp(_ballCount, 1, MaxBallCount);
            set => _ballCount = Mathf.Clamp(value, 1, MaxBallCount);
        }

        public float MasterVolume { get => _masterVolume; set => _masterVolume = Mathf.Clamp01(value); }
        public float MusicVolume { get => _musicVolume; set => _musicVolume = Mathf.Clamp01(value); }
        public float SfxVolume { get => _sfxVolume; set => _sfxVolume = Mathf.Clamp01(value); }
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
