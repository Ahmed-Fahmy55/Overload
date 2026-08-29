using Core.Events;
using Deadball.Events;
using Deadball.AI;
using Deadball.Match;
using Sirenix.OdinInspector;
using UnityEngine;
using UnityEngine.UI;
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

        [Tooltip("Title, Solo setup, Versus join. Index 0 is the screen the game opens on.")]
        [SerializeField] GameObject[] _panels;

        [Title("Difficulty Tiers", "13.3 - one AI, three profiles")]
        [SerializeField] AiProfile[] _tiers;

        [Title("Arenas", "15.3 - default SECTOR 9")]
        [SerializeField] string[] _arenaScenes = { "Arena_Greybox", "Arena_TheSpine" };
        [SerializeField] string[] _arenaNames = { "SECTOR 9", "THE SPINE" };

        [Title("Arena Cards", "22 - the picker shows the deck you are choosing")]
        [Tooltip("One preview per arena, in the same order as the scenes above.")]
        [SerializeField] Sprite[] _arenaCards;

        [Tooltip("Every Image that should show the chosen deck - the solo and versus panels each have one.")]
        [SerializeField] Image[] _arenaCardTargets;

        [Tooltip("The two claim slots. Versus will not start until both are held (21.3).")]
        [SerializeField] VersusJoinPanel _versusJoin;

        [Title("Runtime"), ShowInInspector, ReadOnly]
        public int SelectedTier { get; private set; }

        [ShowInInspector, ReadOnly]
        public int SelectedArena { get; private set; }

        void Start()
        {
            // Whatever the menu last chose is re-applied so the panels and the asset agree.
            SelectArena(SelectedArena);
            SelectTier(SelectedTier);
            ShowPanel(0);
        }

        /// <summary>
        /// Switches screens by activation rather than through Heat's PanelManager.
        /// </summary>
        /// <remarks>
        /// PanelManager drives an Animator per panel, which is more machinery than five static
        /// screens need - and Heat ships no assembly definition, so referencing its types from this
        /// assembly is not possible anyway. The Heat buttons and selectors still do all the visible
        /// work; only the switching is ours.
        /// </remarks>
        public void ShowPanel(int index)
        {
            if (_panels == null) return;

            for (int i = 0; i < _panels.Length; i++)
            {
                if (_panels[i] != null) _panels[i].SetActive(i == index);
            }
        }

        public void ShowTitle() => ShowPanel(0);

        public void ShowSoloSetup()
        {
            SetModeSolo();
            ShowPanel(1);
        }

        public void ShowVersusJoin()
        {
            SetModeVersus();
            ShowPanel(2);
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
            ShowArenaCard(SelectedArena);
        }

        /// <summary>
        /// Paints the chosen deck onto every card in the menu.
        /// </summary>
        /// <remarks>
        /// Both setup panels carry a card and both are driven from here, so the solo and versus
        /// screens can never disagree about which deck is selected.
        /// </remarks>
        void ShowArenaCard(int index)
        {
            if (_arenaCardTargets == null || _arenaCards == null || _arenaCards.Length == 0) return;

            Sprite card = _arenaCards[Mathf.Clamp(index, 0, _arenaCards.Length - 1)];

            foreach (Image target in _arenaCardTargets)
            {
                if (target == null) continue;

                target.sprite = card;
                target.enabled = card != null;
            }
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

            // Two humans means two devices. Starting without both claimed drops a player onto the
            // deck with nothing driving them, which reads as a broken build rather than a mistake.
            if (_settings.Mode == MatchMode.LocalVersus && _versusJoin != null && !_versusJoin.IsReady)
                return;

            EventBus<SceneLoadRequested>.Raise(new SceneLoadRequested(_settings.ArenaScene));
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
