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
    /// Round timer, round card, and the match-end card (GDD sections 10 and 19).
    /// </summary>
    /// <remarks>
    /// The round card is two seconds of text and nothing else - no menu, no loading, no confirmation.
    /// The whole point is getting players back into play fast.
    /// </remarks>
    public class MatchHud : MonoBehaviour
    {
        [Title("Scene References")]
        [Required, SerializeField] RoundManager _rounds;
        [Required, SerializeField] FighterPalette _palette;

        [Title("Widgets")]
        [Required, SerializeField] TMP_Text _timerLabel;
        [Required, SerializeField] TMP_Text _cardLabel;
        [Required, SerializeField] CanvasGroup _cardGroup;

        EventBinding<RoundStarting> _roundStarting;
        EventBinding<RoundStarted> _roundStarted;
        EventBinding<RoundEnded> _roundEnded;
        EventBinding<OvertimeStarted> _overtime;
        EventBinding<MatchEnded> _matchEnded;

        bool _matchOver;

        void OnEnable()
        {
            _roundStarting = new EventBinding<RoundStarting>(evt =>
            {
                _matchOver = false;
                ShowCard($"ROUND {evt.RoundNumber}", Color.white);
            });
            _roundStarted = new EventBinding<RoundStarted>(() => HideCard());
            _roundEnded = new EventBinding<RoundEnded>(OnRoundEnded);
            _overtime = new EventBinding<OvertimeStarted>(() => ShowCard("OVERTIME", new Color(1f, 0.85f, 0.2f)));
            _matchEnded = new EventBinding<MatchEnded>(OnMatchEnded);

            EventBus<RoundStarting>.Register(_roundStarting);
            EventBus<RoundStarted>.Register(_roundStarted);
            EventBus<RoundEnded>.Register(_roundEnded);
            EventBus<OvertimeStarted>.Register(_overtime);
            EventBus<MatchEnded>.Register(_matchEnded);

            HideCard();
        }

        void OnDisable()
        {
            EventBus<RoundStarting>.Deregister(_roundStarting);
            EventBus<RoundStarted>.Deregister(_roundStarted);
            EventBus<RoundEnded>.Deregister(_roundEnded);
            EventBus<OvertimeStarted>.Deregister(_overtime);
            EventBus<MatchEnded>.Deregister(_matchEnded);
        }

        void Update()
        {
            if (_rounds.IsOvertime)
            {
                _timerLabel.text = "SUDDEN DEATH";
                return;
            }

            _timerLabel.text = Mathf.CeilToInt(Mathf.Max(0f, _rounds.TimeRemaining)).ToString();
        }

        void OnRoundEnded(RoundEnded evt)
        {
            if (_matchOver) return;

            string text = evt.WinnerSlot >= 0
                ? $"{_palette.DisplayName(evt.WinnerSlot)} TAKES THE ROUND"
                : "DRAW";

            ShowCard(text, evt.WinnerSlot >= 0 ? _palette.BodyColour(evt.WinnerSlot) : Color.white);
        }

        void OnMatchEnded(MatchEnded evt)
        {
            _matchOver = true;
            ShowCard($"{_palette.DisplayName(evt.WinnerSlot)} WINS", _palette.BodyColour(evt.WinnerSlot));
        }

        void ShowCard(string text, Color colour)
        {
            _cardLabel.text = text;
            _cardLabel.color = colour;
            _cardGroup.alpha = 1f;
        }

        void HideCard() => _cardGroup.alpha = 0f;
    }
}
