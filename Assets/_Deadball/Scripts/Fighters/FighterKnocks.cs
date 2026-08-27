using System;
using Core.Events;
using Deadball.Config;
using Deadball.Events;
using Sirenix.OdinInspector;
using UnityEngine;

namespace Deadball.Fighters
{
    /// <summary>
    /// Knock count and knockout (GDD sections 9 and 10).
    /// </summary>
    /// <remarks>
    /// There is no health bar and no chip damage on purpose: you are knocked out of the round, and
    /// because every knock was avoidable - you had a dodge and you had a catch - losing reads as your
    /// read rather than as the game's dice.
    /// </remarks>
    public class FighterKnocks : MonoBehaviour, IKnockable
    {
        [Required, SerializeField] MatchConfig _config;

        [Title("Runtime"), ShowInInspector, ReadOnly]
        public int KnocksTaken { get; private set; }

        [ShowInInspector, ReadOnly]
        public int KnocksAllowed
        {
            // Defaults on first read rather than in Awake, so a fighter queried before its Awake
            // has run does not read as already knocked out.
            get => _knocksAllowed > 0 ? _knocksAllowed : _knocksAllowed = _config.KnocksToKo;
            private set => _knocksAllowed = value;
        }

        int _knocksAllowed;

        public int KnocksRemaining => Mathf.Max(0, KnocksAllowed - KnocksTaken);

        [ShowInInspector, ReadOnly]
        public bool IsOut => KnocksRemaining <= 0;

        /// <summary>Wired to the motor's i-frames by <see cref="Fighter"/>.</summary>
        public Func<bool> ExternalImmunity { get; set; }

        public bool IsImmune => IsOut || (ExternalImmunity?.Invoke() ?? false);

        /// <summary>Raised locally so the fighter can drop the ball and stop taking input.</summary>
        public event Action KnockedOut;

        /// <summary>Raised on every knock that did not finish the fighter, for hitstop and shake.</summary>
        public event Action<int, float> Knocked;

        int _slot;

        public void Initialise(int slot) => _slot = slot;

        /// <summary>Restores full knocks for a new round, or fewer when overtime tightens them (10).</summary>
        public void ResetForRound(int knocksAllowed = -1)
        {
            KnocksAllowed = knocksAllowed > 0 ? knocksAllowed : _config.KnocksToKo;
            KnocksTaken = 0;
        }

        public void TakeKnock(int knocks, Vector3 direction, float charge01)
        {
            if (IsImmune || knocks <= 0) return;

            KnocksTaken += knocks;

            if (IsOut)
            {
                EventBus<FighterKnockedOut>.Raise(new FighterKnockedOut(_slot, transform.position));
                KnockedOut?.Invoke();
                return;
            }

            EventBus<FighterKnocked>.Raise(
                new FighterKnocked(_slot, knocks, KnocksRemaining, charge01, transform.position));
            Knocked?.Invoke(knocks, charge01);
        }
    }
}
