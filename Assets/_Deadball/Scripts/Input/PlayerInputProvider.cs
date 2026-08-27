using Deadball.Fighters;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Deadball.Input
{
    /// <summary>
    /// Adapts Unity's Input System to <see cref="IFighterInput"/>.
    /// </summary>
    /// <remarks>
    /// Everything here is polled rather than callback-driven so that a human-driven fighter and an
    /// AI-driven one are read the same way, on the same frame, through the same interface.
    /// </remarks>
    [RequireComponent(typeof(PlayerInput))]
    public class PlayerInputProvider : MonoBehaviour, IFighterInput
    {
        [SerializeField] string _moveAction = "Move";
        [SerializeField] string _throwAction = "Throw";
        [SerializeField] string _dodgeAction = "Dodge";
        [SerializeField] string _catchAction = "Catch";

        PlayerInput _playerInput;
        InputAction _move;
        InputAction _throw;
        InputAction _dodge;
        InputAction _catch;
        int _suppressedFrame = -1;
        bool _awaitingThrowRelease;

        public string ControlScheme => _playerInput != null ? _playerInput.currentControlScheme : string.Empty;

        public Vector2 Move => _move?.ReadValue<Vector2>() ?? Vector2.zero;

        public bool ThrowHeld
        {
            get
            {
                bool held = _throw?.IsPressed() ?? false;

                // A player who was still holding throw when the round card came up has to let go
                // before the next round will charge - otherwise they start it mid-charge, rooted.
                if (_awaitingThrowRelease)
                {
                    if (held) return false;
                    _awaitingThrowRelease = false;
                }

                return held && !Suppressed;
            }
        }

        public bool DodgePressed => !Suppressed && (_dodge?.WasPressedThisFrame() ?? false);

        public bool CatchPressed => !Suppressed && (_catch?.WasPressedThisFrame() ?? false);

        bool Suppressed => Time.frameCount == _suppressedFrame;

        void Awake()
        {
            _playerInput = GetComponent<PlayerInput>();
            _move = _playerInput.actions.FindAction(_moveAction, throwIfNotFound: true);
            _throw = _playerInput.actions.FindAction(_throwAction, throwIfNotFound: true);
            _dodge = _playerInput.actions.FindAction(_dodgeAction, throwIfNotFound: true);
            _catch = _playerInput.actions.FindAction(_catchAction, throwIfNotFound: true);
        }

        /// <summary>
        /// Drops anything held or pressed for the current frame.
        /// </summary>
        /// <remarks>
        /// Called when control is handed back at the start of a round. Without it, a player still
        /// holding the throw button through the round card would begin the round mid-charge.
        /// </remarks>
        public void Clear()
        {
            _suppressedFrame = Time.frameCount;
            _awaitingThrowRelease = true;
        }
    }
}
