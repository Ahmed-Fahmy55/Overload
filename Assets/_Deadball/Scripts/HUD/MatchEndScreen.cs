using Core.Events;
using Deadball.Config;
using Deadball.Events;
using Deadball.Match;
using Sirenix.OdinInspector;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Deadball.HUD
{
    /// <summary>
    /// The match-end card, over the blacked-out sector (OVERLOAD GDD section 21, screen 5).
    /// </summary>
    /// <remarks>
    /// Deliberately delayed: the blackout and the emergency-red flip are the payoff shot, and
    /// dropping a menu over them immediately would step on the moment the whole title is named
    /// after. Rematch is one button because local versus lives or dies on how fast the next match
    /// starts (11.2).
    /// </remarks>
    public class MatchEndScreen : MonoBehaviour
    {
        [Title("References")]
        [Required, SerializeField] CanvasGroup _group;
        [Required, SerializeField] TMP_Text _winnerLabel;
        [Required, SerializeField] FighterPalette _palette;
        [Required, SerializeField] MatchManager _match;

        [Title("Flow")]
        [Tooltip("Long enough for the blackout and the emergency-red flip to land first (3).")]
        [SuffixLabel("s", true), MinValue(0f), SerializeField] float _delay = 1.4f;

        [SerializeField] string _menuScene = "Menu";

        [Title("Runtime"), ShowInInspector, ReadOnly]
        public bool IsShowing { get; private set; }

        EventBinding<MatchEnded> _matchEnded;
        float _showAt = -1f;

        void OnEnable()
        {
            _matchEnded = new EventBinding<MatchEnded>(OnMatchEnded);
            EventBus<MatchEnded>.Register(_matchEnded);
            Hide();
        }

        void OnDisable() => EventBus<MatchEnded>.Deregister(_matchEnded);

        void Update()
        {
            if (IsShowing || _showAt < 0f || Time.unscaledTime < _showAt) return;

            _showAt = -1f;
            IsShowing = true;

            if (_group != null)
            {
                _group.alpha = 1f;
                _group.interactable = true;
                _group.blocksRaycasts = true;
            }
        }

        void OnMatchEnded(MatchEnded evt)
        {
            if (_winnerLabel != null)
            {
                _winnerLabel.text = $"{_palette.DisplayName(evt.WinnerSlot)} WINS";
                _winnerLabel.color = _palette.BodyColour(evt.WinnerSlot);
            }

            // Unscaled: the KO slow-mo is still running and must not stretch this.
            _showAt = Time.unscaledTime + _delay;
        }

        /// <summary>Same players, same deck, immediately (11.2).</summary>
        [Button("Rematch"), DisableInEditorMode]
        public void Rematch()
        {
            Hide();
            _match.Rematch();
        }

        [Button("Back To Menu"), DisableInEditorMode]
        public void BackToMenu()
        {
            Time.timeScale = 1f;
            SceneManager.LoadScene(_menuScene);
        }

        void Hide()
        {
            IsShowing = false;
            _showAt = -1f;

            if (_group == null) return;

            _group.alpha = 0f;
            _group.interactable = false;
            _group.blocksRaycasts = false;
        }
    }
}
