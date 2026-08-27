using System;
using System.Collections.Generic;
using Deadball.Fighters;
using Sirenix.OdinInspector;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Deadball.Input
{
    /// <summary>
    /// The Local Versus join flow (GDD section 11.2): two slots, press any button to claim one.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Gamepads are the intended setup and get the free path - <c>PlayerInputManager</c> in
    /// join-on-button-press mode handles slot claiming with no code. The keyboard split is the
    /// documented fallback for a one-pad machine and is joined explicitly, because two players on
    /// one device cannot be auto-detected from a button press.
    /// </para>
    /// <para>
    /// It is also what lets a solo developer playtest both slots on Day 1 without owning a second
    /// pad, which is the whole reason Local Versus ships before the AI does.
    /// </para>
    /// </remarks>
    [RequireComponent(typeof(PlayerInputManager))]
    public class FighterJoinManager : MonoBehaviour, IFighterRoster
    {
        /// <remarks>
        /// Values are pinned because this is serialized on the fighter prefab and in the scene -
        /// renumbering would silently change the mode of an already-built arena.
        /// </remarks>
        public enum JoinMode
        {
            [Tooltip("Two gamepads. Each player presses any button to claim a slot.")]
            GamepadsPressToJoin = 0,

            [Tooltip("One keyboard, split WASD / arrows. The documented fallback.")]
            KeyboardSplit = 1,

            [Tooltip("Whichever the machine can actually do right now.")]
            Auto = 2,

            [Tooltip("One gamepad on P1, the keyboard on P2. The solo-developer playtest setup.")]
            GamepadAndKeyboard = 3
        }

        [Title("Join")]
        [EnumToggleButtons, SerializeField] JoinMode _mode = JoinMode.Auto;
        [MinValue(1), SerializeField] int _requiredPlayers = 2;

        [Title("Schemes")]
        [SerializeField] string _gamepadScheme = "Gamepad";
        [SerializeField] string _keyboardSchemeP1 = "KeyboardP1";
        [SerializeField] string _keyboardSchemeP2 = "KeyboardP2";

        [Title("Runtime")]
        [ShowInInspector, ReadOnly]
        public IReadOnlyList<Fighter> Fighters => _fighters;

        [ShowInInspector, ReadOnly]
        public bool IsReady => _fighters.Count >= _requiredPlayers;

        /// <summary>Raised per fighter as it claims a slot, and once more when the roster is full.</summary>
        public event Action<Fighter> FighterJoined;
        public event Action RosterComplete;

        readonly List<Fighter> _fighters = new(2);
        PlayerInputManager _manager;

        void Awake() => _manager = GetComponent<PlayerInputManager>();

        void OnEnable() => _manager.onPlayerJoined += OnPlayerJoined;

        void OnDisable() => _manager.onPlayerJoined -= OnPlayerJoined;

        void Start()
        {
            switch (ResolveMode())
            {
                case JoinMode.KeyboardSplit:
                    JoinKeyboardSplit();
                    break;

                case JoinMode.GamepadAndKeyboard:
                    JoinGamepadAndKeyboard();
                    break;

                default:
                    _manager.EnableJoining();
                    break;
            }
        }

        /// <summary>
        /// Claims P1 with the first gamepad and P2 with the keyboard.
        /// </summary>
        /// <remarks>
        /// This is the setup a solo developer actually has at 2am: one pad and one keyboard. It also
        /// happens to be the best way to check that the pad feel and the keyboard feel agree, since
        /// both are on screen at once.
        /// </remarks>
        [Button("Join Gamepad + Keyboard"), DisableInEditorMode]
        public void JoinGamepadAndKeyboard()
        {
            if (Gamepad.all.Count == 0 || Keyboard.current == null)
            {
                Debug.LogWarning("[Deadball] Need one gamepad and a keyboard for the mixed setup.", this);
                return;
            }

            _manager.DisableJoining();
            _manager.JoinPlayer(-1, -1, _gamepadScheme, Gamepad.all[0]);

            // P2 gets the WASD half rather than the numpad: with no second player contending for the
            // keyboard there is no reason to hand them the worse set of keys.
            if (_fighters.Count < _requiredPlayers)
                _manager.JoinPlayer(-1, -1, _keyboardSchemeP1, Keyboard.current);
        }

        [Button("Join Keyboard Split"), DisableInEditorMode]
        public void JoinKeyboardSplit()
        {
            if (Keyboard.current == null)
            {
                Debug.LogWarning("[Deadball] No keyboard present; cannot run the keyboard split fallback.", this);
                return;
            }

            _manager.DisableJoining();

            foreach (string scheme in new[] { _keyboardSchemeP1, _keyboardSchemeP2 })
            {
                if (_fighters.Count >= _requiredPlayers) break;
                _manager.JoinPlayer(-1, -1, scheme, Keyboard.current);
            }
        }

        System.Collections.IEnumerator AnnounceRosterNextFrame()
        {
            yield return null;
            RosterComplete?.Invoke();
        }

        /// <summary>Picks the best setup the connected devices can actually support.</summary>
        JoinMode ResolveMode()
        {
            if (_mode != JoinMode.Auto) return _mode;

            if (Gamepad.all.Count >= _requiredPlayers) return JoinMode.GamepadsPressToJoin;

            // One pad used to fall all the way through to the keyboard split, which quietly ignored
            // the controller entirely - the one device most likely to be plugged in while tuning.
            if (Gamepad.all.Count > 0 && Keyboard.current != null) return JoinMode.GamepadAndKeyboard;

            return JoinMode.KeyboardSplit;
        }

        void OnPlayerJoined(PlayerInput playerInput)
        {
            if (playerInput.GetComponent<Fighter>() is not { } fighter)
            {
                Debug.LogError($"[Deadball] Player prefab '{playerInput.name}' has no {nameof(Fighter)}.", playerInput);
                return;
            }

            if (playerInput.GetComponent<PlayerInputProvider>() is not { } provider)
            {
                Debug.LogError($"[Deadball] Player prefab '{playerInput.name}' has no {nameof(PlayerInputProvider)}.", playerInput);
                return;
            }

            fighter.Bind(playerInput.playerIndex, provider);
            _fighters.Add(fighter);
            FighterJoined?.Invoke(fighter);

            if (!IsReady) return;

            _manager.DisableJoining();

            // Deferred by a frame on purpose. This runs inside PlayerInput.OnEnable, which the Input
            // System raises before the rest of the spawned prefab has finished waking up - starting a
            // match from in here means the round manager touches half-initialised fighters.
            StartCoroutine(AnnounceRosterNextFrame());
        }
    }
}
