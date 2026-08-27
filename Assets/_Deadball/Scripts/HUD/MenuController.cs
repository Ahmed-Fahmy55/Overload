using Deadball.AI;
using Deadball.Match;
using Sirenix.OdinInspector;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Deadball.HUD
{
    /// <summary>
    /// Drives the front-end screens (OVERLOAD GDD section 21).
    /// </summary>
    /// <remarks>
    /// <para>
    /// Three screens live here - title, solo setup, versus join - and the match-end card lives in the
    /// arena scene where it can sit over the blacked-out sector. Panel switching is Heat's
    /// <see cref="PanelManager"/>; this only decides what the choices mean.
    /// </para>
    /// <para>
    /// Everything it collects goes into <see cref="MatchSettings"/>, which is what survives the scene
    /// load. Nothing else crosses the boundary.
    /// </para>
    /// </remarks>
    public class MenuController : MonoBehaviour
    {
        [Title("References")]
        [Required, SerializeField] MatchSettings _settings;

        [Title("Difficulty Tiers", "13.3 - one AI, three profiles")]
        [SerializeField] AiProfile[] _tiers;

        [Title("Arenas", "15.3 - default SECTOR 9")]
        [SerializeField] string[] _arenaScenes = { "Arena_Greybox", "Arena_TheSpine" };
        [SerializeField] string[] _arenaNames = { "SECTOR 9", "THE SPINE" };

        [Title("Runtime"), ShowInInspector, ReadOnly]
        public int SelectedTier { get; private set; }

        [ShowInInspector, ReadOnly]
        public int SelectedArena { get; private set; }

        void Start()
        {
            // Whatever the menu last chose is re-applied so the panels and the asset agree.
            SelectArena(SelectedArena);
            SelectTier(SelectedTier);
        }

        /// <summary>
        /// Mode setters, called from the same button that opens the panel.
        /// </summary>
        /// <remarks>
        /// Panel navigation is left to Heat's own PanelManager, wired button-to-manager in the
        /// inspector. Heat ships no assembly definition, so its types live in Assembly-CSharp and
        /// cannot be referenced from here - and adding an asmdef to a vendor package to work around
        /// that is a worse trade than one extra UnityEvent entry per button.
        /// </remarks>
        public void SetModeSolo() => _settings.Mode = MatchMode.Solo;

        public void SetModeVersus() => _settings.Mode = MatchMode.LocalVersus;

        /// <summary>Arena picker. Index 0 is SECTOR 9, the default a new player should meet first.</summary>
        public void SelectArena(int index)
        {
            if (_arenaScenes == null || _arenaScenes.Length == 0) return;

            SelectedArena = Mathf.Clamp(index, 0, _arenaScenes.Length - 1);
            _settings.SetArena(_arenaScenes[SelectedArena], _arenaNames[SelectedArena]);
        }

        /// <summary>Difficulty picker: ROOKIE, OPERATOR, GHOST.</summary>
        public void SelectTier(int index)
        {
            if (_tiers == null || _tiers.Length == 0) return;

            SelectedTier = Mathf.Clamp(index, 0, _tiers.Length - 1);
            _settings.AiProfile = _tiers[SelectedTier];
        }

        [Button("Fight"), DisableInEditorMode]
        public void Fight()
        {
            if (string.IsNullOrEmpty(_settings.ArenaScene))
            {
                Debug.LogError("[Overload] No arena selected.", this);
                return;
            }

            SceneManager.LoadScene(_settings.ArenaScene);
        }

        public void Quit()
        {
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }
    }
}
