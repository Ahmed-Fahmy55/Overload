using Deadball.Fighters;
using UnityEngine;

namespace Deadball.Tests
{
    /// <summary>
    /// A fighter input driven by the test rather than by a device.
    /// </summary>
    /// <remarks>
    /// This is the payoff of routing control through <see cref="IFighterInput"/>: the entire Day 1
    /// loop can be exercised without a gamepad, a keyboard, or the Input System's device plumbing -
    /// and the same seam takes the Day 2 AI.
    /// </remarks>
    public class ScriptedInput : IFighterInput
    {
        public Vector2 Move { get; set; }
        public bool ThrowHeld { get; set; }

        public bool DodgePressed
        {
            get
            {
                bool pressed = _dodgeQueued;
                _dodgeQueued = false;
                return pressed;
            }
        }

        public bool CatchPressed
        {
            get
            {
                bool pressed = _catchQueued;
                _catchQueued = false;
                return pressed;
            }
        }

        bool _dodgeQueued;
        bool _catchQueued;

        public void PressDodge() => _dodgeQueued = true;

        public void PressCatch() => _catchQueued = true;

        public void Clear()
        {
            Move = Vector2.zero;
            ThrowHeld = false;
            _dodgeQueued = false;
            _catchQueued = false;
        }
    }
}
