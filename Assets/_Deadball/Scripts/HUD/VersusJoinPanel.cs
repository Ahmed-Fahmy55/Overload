using System.Collections.Generic;
using Sirenix.OdinInspector;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Deadball.HUD
{
    /// <summary>
    /// The two slots on the Local Versus join screen (OVERLOAD GDD 21.3, 11.2).
    /// </summary>
    /// <remarks>
    /// <para>
    /// "Each player presses any button on their device to claim one." This is the claim step the
    /// spec asks for: until both slots are held, START does nothing, so nobody starts a two-player
    /// match with one pad plugged in and discovers it on the deck.
    /// </para>
    /// <para>
    /// Claiming here is a readiness gate, not a device binding. The arena's
    /// <see cref="Deadball.Input.FighterJoinManager"/> resolves the real pairing from the devices
    /// that are present, and it already handles pad+pad, pad+keyboard and the keyboard split. Two
    /// systems binding the same devices would be a race; one shows intent, the other does the work.
    /// </para>
    /// </remarks>
    public class VersusJoinPanel : MonoBehaviour
    {
        [Title("Slots")]
        [Required, SerializeField] TMP_Text _slotOneLabel;
        [Required, SerializeField] TMP_Text _slotTwoLabel;

        [Title("Copy")]
        [SerializeField] string _waitingText = "PRESS ANY BUTTON";
        [SerializeField] string _claimedText = "READY";

        [Title("Colours")]
        [SerializeField] Color _waitingColour = new(0.4f, 0.46f, 0.56f);
        [SerializeField] Color _slotOneColour = new(0.15f, 0.85f, 1f);
        [SerializeField] Color _slotTwoColour = new(1f, 0.16f, 0.9f);

        [Title("Runtime"), ShowInInspector, ReadOnly]
        public int ClaimedCount => _claimed.Count;

        [ShowInInspector, ReadOnly]
        public bool IsReady => _claimed.Count >= 2;

        readonly List<InputDevice> _claimed = new(2);

        void OnEnable()
        {
            _claimed.Clear();
            Refresh();
        }

        void OnDisable() => _claimed.Clear();

        void Update()
        {
            if (IsReady) return;

            // A keyboard and a gamepad both report through the same event, so one device cannot
            // claim both slots - two people, two devices, exactly as the mode requires.
            foreach (InputDevice device in InputSystem.devices)
            {
                if (device is not (Gamepad or Keyboard)) continue;
                if (_claimed.Contains(device)) continue;
                if (!WasPressedThisFrame(device)) continue;

                _claimed.Add(device);
                Refresh();
                return;
            }
        }

        /// <summary>Any button or key on the device, without binding an action map for it.</summary>
        static bool WasPressedThisFrame(InputDevice device) => device switch
        {
            Gamepad pad => pad.buttonSouth.wasPressedThisFrame
                || pad.buttonEast.wasPressedThisFrame
                || pad.buttonWest.wasPressedThisFrame
                || pad.buttonNorth.wasPressedThisFrame
                || pad.startButton.wasPressedThisFrame
                || pad.rightTrigger.wasPressedThisFrame
                || pad.leftTrigger.wasPressedThisFrame,

            // anyKey covers the split-keyboard fallback without caring which half was pressed.
            Keyboard keyboard => keyboard.anyKey.wasPressedThisFrame,

            _ => false
        };

        void Refresh()
        {
            Apply(_slotOneLabel, 0, _slotOneColour);
            Apply(_slotTwoLabel, 1, _slotTwoColour);
        }

        void Apply(TMP_Text label, int index, Color claimedColour)
        {
            if (label == null) return;

            bool claimed = _claimed.Count > index;
            label.text = claimed ? _claimedText : _waitingText;
            label.color = claimed ? claimedColour : _waitingColour;
        }
    }
}
