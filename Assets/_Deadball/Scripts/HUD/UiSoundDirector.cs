using Ami.BroAudio;
using Sirenix.OdinInspector;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Deadball.HUD
{
    /// <summary>
    /// Hover and click sounds for every menu control in the scene.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Driven off selection rather than off the mouse. The cursor is locked and hidden, and the
    /// menus are built to be driven by a pad or the arrow keys, so "hover" here means the highlight
    /// moved - which is the same event whether it was a stick, a key or a mouse that moved it.
    /// Hooking pointer-enter instead would have left pad users in silence.
    /// </para>
    /// <para>
    /// Buttons are found and hooked at runtime rather than wired per control. Every screen in the
    /// game is built by script, and a wiring step that has to be repeated for each new button is a
    /// step that eventually gets missed.
    /// </para>
    /// </remarks>
    public class UiSoundDirector : MonoBehaviour
    {
        [Title("Cues")]
        [Tooltip("Played when the highlight moves to a different control.")]
        [SerializeField] SoundID _hover;

        [Tooltip("Played when a control is activated.")]
        [SerializeField] SoundID _click;

        [Title("Runtime"), ShowInInspector, ReadOnly]
        public string Selected => _selected != null ? _selected.name : "(none)";

        GameObject _selected;
        bool _armed;

        void OnEnable()
        {
            HookEverything();

            // The first selection of a screen is seated by code, not by the player moving onto it,
            // so it must not sound like a move they made.
            _selected = EventSystem.current != null
                ? EventSystem.current.currentSelectedGameObject
                : null;

            _armed = true;
        }

        void OnDisable() => _armed = false;

        void Update()
        {
            if (!_armed || EventSystem.current == null) return;

            GameObject current = EventSystem.current.currentSelectedGameObject;
            if (current == _selected) return;

            _selected = current;

            // Selection going null happens on every panel switch and is not a move.
            if (current != null) Play(_hover);
        }

        /// <summary>
        /// Adds a click sound to every button and option row in the scene.
        /// </summary>
        /// <remarks>
        /// Inactive objects are included: the settings and pause panels are switched off at author
        /// time, and their controls would otherwise never be hooked.
        /// </remarks>
        void HookEverything()
        {
            foreach (Button button in FindObjectsByType<Button>(
                FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                Button captured = button;
                captured.onClick.AddListener(() => Play(_click));
            }

            foreach (MenuSelectorRow row in FindObjectsByType<MenuSelectorRow>(
                FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                // The change is the action on an option row - there is nothing else to press.
                row.onValueChanged.AddListener(_ => Play(_click));
            }
        }

        void Play(SoundID id)
        {
            if (id.IsValid()) BroAudio.Play(id);
        }
    }
}
