using System;
using Core.Events;
using Deadball.Ball;
using Deadball.Config;
using Deadball.Events;
using Sirenix.OdinInspector;
using UnityEngine;

namespace Deadball.Match
{
    /// <summary>
    /// Rally Heat - the hook (OVERLOAD GDD section 16).
    /// </summary>
    /// <remarks>
    /// <para>
    /// One float. It climbs on every PERFECT clamp and bleeds off only while the core is LOOSE on the
    /// deck, which is what turns every incoming core into a three-way read instead of a two-way one:
    /// dodge for safety, clamp perfectly for tempo, or take the late clamp to cool the core down.
    /// That third option is the game - no other dodgeball has a reason to refuse the ball.
    /// </para>
    /// <para>
    /// It owns the number and nothing else. The colour ramp, the audio ramp and the one-hit rule all
    /// live in the systems that care about them and subscribe here, so heat can be re-tuned without
    /// touching any of them.
    /// </para>
    /// </remarks>
    public class RallyHeat : MonoBehaviour
    {
        [Title("Config")]
        [Required, SerializeField] MatchConfig _config;

        [Title("Scene References")]
        [Required, SerializeField] BallController _core;

        [Title("Runtime"), ShowInInspector, ReadOnly, ProgressBar(0, 100, ColorGetter = "HeatBarColour")]
        public float Heat { get; private set; }

        /// <summary>Heat as 0..1 against the maximum, for shader and audio ramps.</summary>
        public float Heat01 => _config != null ? Mathf.Clamp01(Heat / _config.MaxHeat) : 0f;

        /// <summary>At or above the threshold a single hit is a KO instead of two (9).</summary>
        [ShowInInspector, ReadOnly]
        public bool IsCritical { get; private set; }

        /// <summary>Raised whenever the number moves, with heat and its normalised form.</summary>
        public event Action<float, float> HeatChanged;

        /// <summary>Raised only on the threshold crossing, in either direction.</summary>
        public event Action<bool> CriticalChanged;

        EventBinding<BallCaught> _caught;
        EventBinding<RoundStarting> _roundStarting;

        void OnEnable()
        {
            // Only a perfect clamp adds heat. A late clamp adds nothing and drops the core loose,
            // which is what starts it bleeding (8.2).
            _caught = new EventBinding<BallCaught>(OnClamped);
            _roundStarting = new EventBinding<RoundStarting>(ResetHeat);

            EventBus<BallCaught>.Register(_caught);
            EventBus<RoundStarting>.Register(_roundStarting);
        }

        void OnDisable()
        {
            EventBus<BallCaught>.Deregister(_caught);
            EventBus<RoundStarting>.Deregister(_roundStarting);
        }

        /// <summary>True when every core on the deck is lying loose.</summary>
        bool AllCoresLoose()
        {
            var cores = Deadball.Ball.CoreRegistry.Cores;
            if (cores.Count == 0) return _core != null && _core.State == BallState.Loose;

            for (int i = 0; i < cores.Count; i++)
                if (cores[i] != null && cores[i].State != BallState.Loose) return false;

            return true;
        }

        void Update()
        {
            // Heat is a property of the rally, not of one ball, so with several cores in play it
            // bleeds only while none of them is being carried - otherwise a player could park one
            // core in hand and cool the deck off with the others (16).
            if (Heat <= 0f) return;
            if (!AllCoresLoose()) return;

            SetHeat(Heat - _config.HeatDecayPerSecond * Time.deltaTime);
        }

        /// <summary>Zeroes heat for a fresh round.</summary>
        public void ResetHeat() => SetHeat(0f);

        /// <summary>Adds heat directly. Exposed for tests and for the AI to reason about.</summary>
        public void Add(float amount) => SetHeat(Heat + amount);

        void OnClamped(BallCaught evt)
        {
            if (evt.Tier == Fighters.ClampTier.Perfect)
                SetHeat(Heat + _config.HeatPerPerfectClamp);
        }

        void SetHeat(float value)
        {
            float clamped = Mathf.Clamp(value, 0f, _config.MaxHeat);
            if (Mathf.Approximately(clamped, Heat)) return;

            Heat = clamped;
            HeatChanged?.Invoke(Heat, Heat01);

            bool critical = Heat >= _config.CriticalHeat;
            if (critical == IsCritical) return;

            IsCritical = critical;
            CriticalChanged?.Invoke(critical);
            EventBus<CriticalStateChanged>.Raise(new CriticalStateChanged(critical, Heat));
        }

        Color HeatBarColour(float _) => IsCritical
            ? Color.white
            : Color.Lerp(new Color(0.6f, 0.85f, 1f), new Color(1f, 0.6f, 0.1f), Heat01);
    }
}
