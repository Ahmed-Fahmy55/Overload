using Core.Events;
using Deadball.Config;
using Deadball.Events;
using Deadball.Match;
using Sirenix.OdinInspector;
using TMPro;
using UnityEngine;

namespace Deadball.HUD
{
    /// <summary>
    /// One corner of the in-match HUD: a name, knock pips, and round-win pips (GDD section 19).
    /// </summary>
    /// <remarks>
    /// Slot 0 sits top-left and slot 1 top-right, matching the fighters' own colours, so a player
    /// never has to work out which half of the screen is theirs.
    /// </remarks>
    public class FighterHudPanel : MonoBehaviour
    {
        [Title("Identity")]
        [MinValue(0), SerializeField] int _slot;
        [Required, SerializeField] FighterPalette _palette;
        [Required, SerializeField] MatchConfig _config;

        [Title("Widgets")]
        [Required, SerializeField] TMP_Text _nameLabel;
        [Required, SerializeField] HudPipRow _knockPips;
        [Required, SerializeField] HudPipRow _roundWinPips;

        [Title("Match")]
        [Required, SerializeField] MatchManager _match;

        EventBinding<FighterKnocked> _knocked;
        EventBinding<FighterKnockedOut> _knockedOut;
        EventBinding<RoundStarting> _roundStarting;
        EventBinding<RoundEnded> _roundEnded;
        EventBinding<OvertimeStarted> _overtime;

        void Awake()
        {
            Color colour = _palette.BodyColour(_slot);
            _nameLabel.text = _palette.DisplayName(_slot);
            _nameLabel.color = colour;

            _knockPips.SetColour(colour);
            _roundWinPips.SetColour(colour);

            _knockPips.Configure(_config.KnocksToKo);
            _roundWinPips.Configure(_config.RoundWinsToTakeMatch);
            _roundWinPips.SetFilled(0);
        }

        void OnEnable()
        {
            _knocked = new EventBinding<FighterKnocked>(OnKnocked);
            _knockedOut = new EventBinding<FighterKnockedOut>(OnKnockedOut);
            _roundStarting = new EventBinding<RoundStarting>(() => ResetPips(_config.KnocksToKo));
            _roundEnded = new EventBinding<RoundEnded>(OnRoundEnded);
            _overtime = new EventBinding<OvertimeStarted>(() => ResetPips(_config.OvertimeKnocksRemaining));

            EventBus<FighterKnocked>.Register(_knocked);
            EventBus<FighterKnockedOut>.Register(_knockedOut);
            EventBus<RoundStarting>.Register(_roundStarting);
            EventBus<RoundEnded>.Register(_roundEnded);
            EventBus<OvertimeStarted>.Register(_overtime);
        }

        void OnDisable()
        {
            EventBus<FighterKnocked>.Deregister(_knocked);
            EventBus<FighterKnockedOut>.Deregister(_knockedOut);
            EventBus<RoundStarting>.Deregister(_roundStarting);
            EventBus<RoundEnded>.Deregister(_roundEnded);
            EventBus<OvertimeStarted>.Deregister(_overtime);
        }

        void OnKnocked(FighterKnocked evt)
        {
            if (evt.Slot != _slot) return;

            _knockPips.SetFilled(evt.KnocksRemaining);
        }

        void OnKnockedOut(FighterKnockedOut evt)
        {
            if (evt.Slot != _slot) return;

            _knockPips.SetFilled(0);
        }

        void OnRoundEnded(RoundEnded evt) => _roundWinPips.SetFilled(_match.RoundWins(_slot));

        void ResetPips(int knocks)
        {
            _knockPips.Configure(knocks);
            _roundWinPips.SetFilled(_match.RoundWins(_slot));
        }
    }
}
