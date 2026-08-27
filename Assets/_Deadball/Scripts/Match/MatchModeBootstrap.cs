using Deadball.AI;
using Deadball.Fighters;
using Deadball.Input;
using Sirenix.OdinInspector;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Deadball.Match
{
    /// <summary>
    /// Applies the menu's choices when an arena scene opens (OVERLOAD GDD section 21).
    /// </summary>
    /// <remarks>
    /// <para>
    /// Solo and Local Versus differ only in what drives player two, so the whole of that difference
    /// is one decision made here: spawn a house runner, or wait for a second device. Everything
    /// downstream sees two runners either way.
    /// </para>
    /// <para>
    /// The losing mode is switched off by <em>component</em>, never by GameObject. The join manager
    /// shares "Systems" with the match and round managers, so deactivating its object would take the
    /// round clock down with it - which shows up as the HUD throwing every frame rather than as
    /// anything that looks like a mode bug. Disabling the component is enough because all of its
    /// joining work happens in Start, which a disabled component never reaches.
    /// </para>
    /// </remarks>
    [DefaultExecutionOrder(-100)]
    public class MatchModeBootstrap : MonoBehaviour
    {
        [Title("Choices")]
        [Required, SerializeField] MatchSettings _settings;

        [Title("Mode Owners")]
        [Tooltip("Spawns one human and one house runner. Solo only. Has its own GameObject.")]
        [Required, SerializeField] SoloRoster _soloRoster;

        [Tooltip("Waits for two devices to claim a slot. Local Versus only.")]
        [Required, SerializeField] FighterJoinManager _versusJoin;

        [Tooltip("Told which roster won, before it falls back to its own serialized field.")]
        [Required, SerializeField] MatchManager _match;

        [Title("Runtime")]
        [ShowInInspector, ReadOnly]
        public MatchMode Mode => _settings != null ? _settings.Mode : MatchMode.LocalVersus;

        void Awake()
        {
            bool solo = Mode == MatchMode.Solo;

            if (solo && _soloRoster != null)
            {
                // The tier is chosen on the setup screen, so it is pushed in before the roster is
                // allowed to wake and read it.
                AiProfile chosen = _settings.AiProfile;
                if (chosen != null) _soloRoster.Profile = chosen;
            }

            // Set before MatchManager wakes: its own lookup only runs if nothing has claimed the
            // slot, so assigning here wins without the two of them disagreeing.
            if (_match != null)
            {
                IFighterRoster winner = solo ? _soloRoster : _versusJoin;
                if (winner != null) _match.Roster = winner;
            }

            if (_soloRoster != null) _soloRoster.gameObject.SetActive(solo);

            if (_versusJoin != null)
            {
                _versusJoin.enabled = !solo;

                // Solo builds its own runner, so nothing should be listening for a device to join.
                if (_versusJoin.TryGetComponent(out PlayerInputManager joining))
                    joining.enabled = !solo;
            }
        }
    }
}
