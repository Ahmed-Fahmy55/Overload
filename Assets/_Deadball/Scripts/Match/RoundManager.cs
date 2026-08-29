using System;
using System.Collections;
using System.Collections.Generic;
using Core.Events;
using Deadball.Ball;
using Deadball.Config;
using Deadball.Events;
using Deadball.Fighters;
using Sirenix.OdinInspector;
using UnityEngine;
using Zone8.ImprovedTimers;

namespace Deadball.Match
{
    /// <summary>
    /// One round: spawn, timer, knockout, and sudden death (GDD section 10).
    /// </summary>
    /// <remarks>
    /// <para>
    /// Rounds are short on purpose. A judge who dies in forty seconds hits retry; a judge who dies in
    /// four minutes closes the tab. Everything here is built around getting players back into play
    /// fast - the round card is two seconds and there is no menu, no loading and no confirmation.
    /// </para>
    /// <para>
    /// The manager owns the round only. Who won the match, and whether another round should start,
    /// is <see cref="MatchManager"/>'s question.
    /// </para>
    /// </remarks>
    public class RoundManager : MonoBehaviour
    {
        [Title("Config")]
        [Required, SerializeField] MatchConfig _config;

        [Title("Scene References")]
        [Required, SerializeField] ArenaReferences _arena;
        [Required, SerializeField] BallController _ball;

        [Tooltip("Optional. Present when the deck can hold more than one core; without it only "
            + "the scene's own core is placed.")]
        [SerializeField] Deadball.Ball.CoreSpawner _cores;

        [Title("Runtime")]
        [ShowInInspector, ReadOnly]
        public int RoundNumber { get; private set; }

        [ShowInInspector, ReadOnly]
        public bool IsRoundActive { get; private set; }

        [ShowInInspector, ReadOnly]
        public bool IsOvertime { get; private set; }

        [Tooltip("Logs a stack trace whenever a round starts. Temporary diagnostics.")]
        [SerializeField] bool _logRoundStarts;

        /// <summary>Seconds left on the clock, or 0 in overtime, which has no timer.</summary>
        public float TimeRemaining => IsOvertime ? 0f : _clock.CurrentTime;

        /// <summary>Raised with the winning slot (-1 for a draw) once the round has resolved.</summary>
        public event Action<int, RoundEndReason> RoundFinished;

        readonly List<Fighter> _fighters = new(2);
        EventBinding<FighterKnockedOut> _knockedOutBinding;
        CountdownTimer _clock;
        Coroutine _sequence;
        int _handicappedSlot = -1;

        void Awake()
        {
            _clock = new CountdownTimer(_config.RoundDuration);
            _clock.OnTimerStop += OnClockStopped;
        }

        void OnEnable()
        {
            _knockedOutBinding = new EventBinding<FighterKnockedOut>(OnFighterKnockedOut);
            EventBus<FighterKnockedOut>.Register(_knockedOutBinding);
        }

        void OnDisable() => EventBus<FighterKnockedOut>.Deregister(_knockedOutBinding);

        void OnDestroy()
        {
            _clock.OnTimerStop -= OnClockStopped;
            _clock.Dispose();
        }

        /// <summary>Runs a round from the intro card through to a winner.</summary>
        /// <param name="handicappedSlot">Slot that lost the previous round, or -1 for round one.</param>
        public void BeginRound(int roundNumber, IReadOnlyList<Fighter> fighters, int handicappedSlot)
        {
            if (_logRoundStarts)
                Debug.Log($"[Overload] ROUND BEGIN {roundNumber} with {fighters.Count} fighters via "
                    + new System.Diagnostics.StackTrace(true), this);

            Abort();

            RoundNumber = roundNumber;
            _handicappedSlot = handicappedSlot;
            _fighters.Clear();
            _fighters.AddRange(fighters);

            _sequence = StartCoroutine(RunRound());
        }

        /// <summary>Stops a round in progress without declaring a winner.</summary>
        public void Abort()
        {
            if (_sequence != null)
            {
                StopCoroutine(_sequence);
                _sequence = null;
            }

            IsRoundActive = false;
            _clock.Stop();
        }

        IEnumerator RunRound()
        {
            IsOvertime = false;
            SetOvertimeOnEveryCore(false);

            PlaceForRound(_config.KnocksToKo);

            // Primed before the card rather than after it, so the HUD shows the full round length
            // during the two-second intro instead of a zero.
            _clock.Reset(_config.RoundDuration);
            EventBus<RoundStarting>.Raise(new RoundStarting(RoundNumber, _config.RoundIntroDuration));

            yield return new WaitForSeconds(_config.RoundIntroDuration);

            HandControlToPlayers();
            IsRoundActive = true;
            _clock.Start();
            EventBus<RoundStarted>.Raise(new RoundStarted(RoundNumber));
        }

        IEnumerator RunOvertime()
        {
            IsOvertime = true;
            SetOvertimeOnEveryCore(true);

            PlaceForRound(_config.OvertimeKnocksRemaining);
            EventBus<OvertimeStarted>.Raise(new OvertimeStarted());

            yield return new WaitForSeconds(_config.RoundIntroDuration);

            HandControlToPlayers();
            IsRoundActive = true;

            // Overtime is a round starting, and everything that reacts to play resuming hangs off
            // this: the HUD only ever hides its card on RoundStarted, so without it the OVERTIME
            // card stayed on screen for the whole of sudden death. The audio beds restart here too.
            EventBus<RoundStarted>.Raise(new RoundStarted(RoundNumber));
        }

        void PlaceForRound(int knocksAllowed)
        {
            for (int i = 0; i < _fighters.Count; i++)
            {
                Fighter fighter = _fighters[i];
                _arena.GetSpawn(fighter.Slot, fighter.Slot == _handicappedSlot, out Vector3 pos, out Quaternion rot);

                fighter.SetControlEnabled(false);
                fighter.PrepareForRound(pos, rot, knocksAllowed);
            }

            // Teleport writes the transform, but the physics broadphase is only refreshed on the
            // next FixedUpdate. Without this the ball's grab trigger tests overlap against where the
            // runners *were*, so a runner who happened to be near the centre last round picks the
            // core up the instant it respawns - from across the deck.
            Physics.SyncTransforms();

            // The spawner owns every core once there is more than one; falling back keeps a scene
            // without one working exactly as it did.
            if (_cores != null)
            {
                _cores.ResetForRound(_arena.Centre, _arena.Size);
            }
            else
            {
                _ball.ArenaSize = _arena.Size;
                _ball.ResetForRound(_arena.Centre);
            }
        }

        /// <summary>
        /// Flags sudden death on every core on the deck.
        /// </summary>
        /// <remarks>
        /// Overtime raises throw speed, and it was only ever set on the core wired in the
        /// inspector. With several in play that made one of them quietly faster than the rest for
        /// the whole of sudden death - the tell that overtime has started would have applied to
        /// whichever core the scene happened to reference.
        /// </remarks>
        void SetOvertimeOnEveryCore(bool active)
        {
            var cores = Deadball.Ball.CoreRegistry.Cores;
            if (cores.Count > 0)
            {
                for (int i = 0; i < cores.Count; i++)
                    if (cores[i] != null) cores[i].OvertimeActive = active;

                return;
            }

            if (_ball != null) _ball.OvertimeActive = active;
        }

        void HandControlToPlayers()
        {
            for (int i = 0; i < _fighters.Count; i++)
                _fighters[i].SetControlEnabled(true);
        }

        void OnFighterKnockedOut(FighterKnockedOut evt)
        {
            if (!IsRoundActive) return;

            Finish(OtherSlot(evt.Slot), RoundEndReason.KnockOut);
        }

        void OnClockStopped()
        {
            // Stop() also raises this, so only a genuine expiry ends the round on time.
            if (!IsRoundActive || IsOvertime || !_clock.IsFinished) return;

            int leader = SlotWithFewestKnocks(out bool tied);

            if (!tied)
            {
                Finish(leader, RoundEndReason.TimeExpired);
                return;
            }

            if (_config.OvertimeEnabled)
            {
                // Sudden death. Usually resolves in under fifteen seconds (10).
                IsRoundActive = false;
                _sequence = StartCoroutine(RunOvertime());
                return;
            }

            Finish(-1, RoundEndReason.Draw);
        }

        void Finish(int winnerSlot, RoundEndReason reason)
        {
            IsRoundActive = false;
            _clock.Stop();

            for (int i = 0; i < _fighters.Count; i++)
                _fighters[i].SetControlEnabled(false);

            // The match tally runs before the broadcast so that anything listening for RoundEnded -
            // the HUD's round-win pips in particular - reads an already-updated score.
            RoundFinished?.Invoke(winnerSlot, reason);
            EventBus<RoundEnded>.Raise(new RoundEnded(winnerSlot, reason));

            // Cleared after the broadcast so listeners still see how the round ended. Overtime only
            // means anything while a round is running: a match that ended in sudden death used to
            // leave the flag set, and the HUD went on reading "SUDDEN DEATH" over the end card
            // forever because nothing started another round to clear it.
            IsOvertime = false;
            SetOvertimeOnEveryCore(false);
        }

        int SlotWithFewestKnocks(out bool tied)
        {
            int bestSlot = -1;
            int bestKnocks = int.MaxValue;
            tied = false;

            for (int i = 0; i < _fighters.Count; i++)
            {
                int knocks = _fighters[i].Knocks.KnocksTaken;

                if (knocks < bestKnocks)
                {
                    bestKnocks = knocks;
                    bestSlot = _fighters[i].Slot;
                    tied = false;
                }
                else if (knocks == bestKnocks)
                {
                    tied = true;
                }
            }

            return bestSlot;
        }

        int OtherSlot(int slot)
        {
            for (int i = 0; i < _fighters.Count; i++)
            {
                if (_fighters[i].Slot != slot)
                    return _fighters[i].Slot;
            }

            return -1;
        }
    }
}
