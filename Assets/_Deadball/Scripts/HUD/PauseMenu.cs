using Core.Events;
using Deadball.Events;
using Deadball.Match;
using Sirenix.OdinInspector;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace Deadball.HUD
{
    /// <summary>
    /// Stops the match and offers a way out (resume, settings, quit).
    /// </summary>
    /// <remarks>
    /// <para>
    /// Opening it sets <see cref="Time.timeScale"/> to zero, which stops the round clock, the
    /// charge timers and the core mid-flight together - the whole match is on one clock, so nothing
    /// has to be told individually to hold still.
    /// </para>
    /// <para>
    /// Everything here runs on unscaled time and unscaled input for that reason. It also takes the
    /// selection when it opens: the cursor is locked away (12), so a pause screen nobody can
    /// navigate would trap the player in the match it just froze.
    /// </para>
    /// <para>
    /// It refuses to open once the match is over. The end card owns the screen at that point and
    /// offers the same way out, so a pause menu on top of it would be two navigable panels
    /// fighting for one selection - and the pause screen would be stealing focus from the card the
    /// player is actually trying to answer.
    /// </para>
    /// </remarks>
    public class PauseMenu : MonoBehaviour
    {
        [Title("Screens")]
        [Required, SerializeField] GameObject _root;

        [Tooltip("The resume/settings/quit list.")]
        [Required, SerializeField] GameObject _mainPanel;

        [Tooltip("Volumes and core count. Shown in place of the list.")]
        [SerializeField] GameObject _settingsPanel;

        [Title("Flow")]
        [SerializeField] string _menuScene = "Menu";

        [Title("Runtime"), ShowInInspector, ReadOnly]
        public bool IsPaused { get; private set; }

        float _restoreTimeScale = 1f;

        EventBinding<MatchEnded> _matchEnded;
        EventBinding<RoundStarting> _roundStarting;

        [ShowInInspector, ReadOnly]
        public bool IsMatchOver { get; private set; }

        void Awake()
        {
            if (_root != null) _root.SetActive(false);
        }

        void OnEnable()
        {
            // A rematch runs rounds again, so the gate lifts on the next round rather than
            // needing the scene to reload.
            _matchEnded = new EventBinding<MatchEnded>(OnMatchEnded);
            _roundStarting = new EventBinding<RoundStarting>(() => IsMatchOver = false);

            EventBus<MatchEnded>.Register(_matchEnded);
            EventBus<RoundStarting>.Register(_roundStarting);
        }

        void OnDisable()
        {
            EventBus<MatchEnded>.Deregister(_matchEnded);
            EventBus<RoundStarting>.Deregister(_roundStarting);

            // A scene change while paused would otherwise leave the next one frozen.
            if (IsPaused) Time.timeScale = _restoreTimeScale;
        }

        void OnMatchEnded(MatchEnded evt)
        {
            IsMatchOver = true;

            // The KO that ends a match and a pause press can land on the same frame. Closing an
            // already-open pause screen keeps the end card from appearing underneath it.
            if (IsPaused) Resume();
        }

        void Update()
        {
            if (!WasPausePressed()) return;

            // Resume still works, so a pause that somehow survived into the end card can be
            // dismissed - it is only opening that is refused.
            if (IsPaused) Resume();
            else if (!IsMatchOver) Pause();
        }

        static bool WasPausePressed()
        {
            bool keyboard = Keyboard.current != null
                && (Keyboard.current.escapeKey.wasPressedThisFrame
                    || Keyboard.current.pKey.wasPressedThisFrame);

            bool pad = Gamepad.current != null
                && (Gamepad.current.startButton.wasPressedThisFrame
                    || Gamepad.current.selectButton.wasPressedThisFrame);

            return keyboard || pad;
        }

        [Button("Pause"), DisableInEditorMode]
        public void Pause()
        {
            if (IsPaused || IsMatchOver) return;

            IsPaused = true;

            // Remembered rather than assumed to be 1: a KO's slow-mo may be running when the
            // player pauses, and resuming has to hand the match back exactly as it was.
            _restoreTimeScale = Time.timeScale;
            Time.timeScale = 0f;

            if (_root != null) _root.SetActive(true);
            ShowMain();
        }

        [Button("Resume"), DisableInEditorMode]
        public void Resume()
        {
            if (!IsPaused) return;

            IsPaused = false;
            Time.timeScale = _restoreTimeScale;

            if (_root != null) _root.SetActive(false);
            if (EventSystem.current != null) EventSystem.current.SetSelectedGameObject(null);
        }

        public void ShowMain() => Show(_mainPanel);

        public void ShowSettings() => Show(_settingsPanel);

        public void QuitToMenu()
        {
            // Unfrozen before leaving, or the menu inherits a stopped clock and nothing animates.
            Time.timeScale = 1f;
            IsPaused = false;

            EventBus<SceneLoadRequested>.Raise(new SceneLoadRequested(_menuScene));
        }

        void Show(GameObject panel)
        {
            if (_mainPanel != null) _mainPanel.SetActive(panel == _mainPanel);
            if (_settingsPanel != null) _settingsPanel.SetActive(panel == _settingsPanel);

            if (panel == null || EventSystem.current == null) return;

            Selectable first = null;
            foreach (Selectable candidate in panel.GetComponentsInChildren<Selectable>(false))
            {
                if (!candidate.IsInteractable()) continue;
                if (first == null || candidate.transform.position.y > first.transform.position.y)
                    first = candidate;
            }

            EventSystem.current.SetSelectedGameObject(first != null ? first.gameObject : null);
        }
    }
}
