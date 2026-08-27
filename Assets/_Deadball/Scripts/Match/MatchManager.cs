using System.Collections;
using System.Collections.Generic;
using Core.Events;
using Deadball.Config;
using Deadball.Events;
using Deadball.Fighters;
using Sirenix.OdinInspector;
using UnityEngine;

namespace Deadball.Match
{
    /// <summary>
    /// Best of three, and the one-button rematch (GDD sections 10 and 11.2).
    /// </summary>
    /// <remarks>
    /// Local versus lives or dies on how fast you can start the next match, so a rematch resets the
    /// tally and goes straight back into round one - no menu, no re-join, no confirmation.
    /// </remarks>
    public class MatchManager : MonoBehaviour
    {
        [Title("Config")]
        [Required, SerializeField] MatchConfig _config;

        [Title("Scene References")]
        [Required, SerializeField] RoundManager _rounds;

        [Tooltip("Anything implementing IFighterRoster: the versus join screen, or a solo roster.")]
        [Required, SerializeField] MonoBehaviour _rosterSource;

        [Title("Flow")]
        [SuffixLabel("s", true), MinValue(0f)]
        [Tooltip("Beat between a round resolving and the next round card.")]
        [SerializeField] float _interRoundDelay = 1.5f;

        [Title("Runtime")]
        [ShowInInspector, ReadOnly]
        public bool IsMatchRunning { get; private set; }

        [ShowInInspector, ReadOnly]
        public int WinnerSlot { get; private set; } = -1;

        readonly Dictionary<int, int> _roundWins = new(2);
        IFighterRoster _roster;
        bool _subscribed;
        Coroutine _pending;
        int _roundNumber;
        int _lastRoundLoser = -1;

        /// <summary>
        /// The roster in play. Settable so a mode - or a test - can install one without a scene edit.
        /// </summary>
        public IFighterRoster Roster
        {
            get => _roster;
            set
            {
                Unsubscribe();
                _roster = value;
                Subscribe();
            }
        }

        void OnEnable()
        {
            if (_roster == null)
            {
                if (_rosterSource is IFighterRoster source)
                    _roster = source;
                else
                    Debug.LogError($"[Deadball] '{_rosterSource?.GetType().Name ?? "null"}' does not "
                        + $"implement {nameof(IFighterRoster)}.", this);
            }

            Subscribe();
            _rounds.RoundFinished += OnRoundFinished;
        }

        void OnDisable()
        {
            Unsubscribe();
            _rounds.RoundFinished -= OnRoundFinished;
        }

        void Subscribe()
        {
            if (_roster == null || _subscribed) return;

            _roster.RosterComplete += StartMatch;
            _subscribed = true;
        }

        void Unsubscribe()
        {
            if (_roster == null || !_subscribed) return;

            _roster.RosterComplete -= StartMatch;
            _subscribed = false;
        }

        public int RoundWins(int slot) => _roundWins.GetValueOrDefault(slot, 0);

        [Button("Start Match"), DisableInEditorMode]
        public void StartMatch()
        {
            if (_roster is not { IsReady: true })
            {
                Debug.LogWarning("[Deadball] Cannot start a match before both slots are claimed.", this);
                return;
            }

            CancelPending();
            _roundWins.Clear();
            _roundNumber = 0;
            _lastRoundLoser = -1;
            WinnerSlot = -1;
            IsMatchRunning = true;

            BeginNextRound();
        }

        /// <summary>Same players, instant. One button (11.2).</summary>
        [Button("Rematch"), DisableInEditorMode]
        public void Rematch() => StartMatch();

        void BeginNextRound()
        {
            _roundNumber++;
            _rounds.BeginRound(_roundNumber, _roster.Fighters, _lastRoundLoser);
        }

        void OnRoundFinished(int winnerSlot, RoundEndReason reason)
        {
            if (!IsMatchRunning) return;

            if (winnerSlot >= 0)
            {
                _roundWins[winnerSlot] = RoundWins(winnerSlot) + 1;
                _lastRoundLoser = OtherSlot(winnerSlot);
            }
            else
            {
                // A draw replays the round rather than awarding it, so a Bo3 cannot end 0-0.
                _lastRoundLoser = -1;
                _roundNumber--;
            }

            if (winnerSlot >= 0 && RoundWins(winnerSlot) >= _config.RoundWinsToTakeMatch)
            {
                EndMatch(winnerSlot);
                return;
            }

            CancelPending();
            _pending = StartCoroutine(NextRoundAfterDelay());
        }

        IEnumerator NextRoundAfterDelay()
        {
            yield return new WaitForSeconds(_interRoundDelay);
            _pending = null;
            BeginNextRound();
        }

        void EndMatch(int winnerSlot)
        {
            IsMatchRunning = false;
            WinnerSlot = winnerSlot;
            CancelPending();
            EventBus<MatchEnded>.Raise(new MatchEnded(winnerSlot));
        }

        void CancelPending()
        {
            if (_pending == null) return;

            StopCoroutine(_pending);
            _pending = null;
        }

        int OtherSlot(int slot)
        {
            IReadOnlyList<Fighter> fighters = _roster.Fighters;
            for (int i = 0; i < fighters.Count; i++)
            {
                if (fighters[i].Slot != slot)
                    return fighters[i].Slot;
            }

            return -1;
        }
    }
}
