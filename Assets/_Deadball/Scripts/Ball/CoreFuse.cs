using Core.Events;
using Deadball.Config;
using Deadball.Events;
using Sirenix.OdinInspector;
using UnityEngine;

namespace Deadball.Ball
{
    /// <summary>
    /// The core cooks whoever carries it, and detonates if they hold on too long.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Rally Heat made the core lethal to be <em>hit</em> by, but it was completely safe to
    /// <em>hold</em> - so the strongest play for whoever was ahead on knocks was to pick the core up
    /// and run out the round. Nobody can take it off you and nobody can hurt you while you have it.
    /// This closes that: possession is now a timer, not a shelter.
    /// </para>
    /// <para>
    /// It also gives the unarmed runner something to do besides wait. Staying out of throwing range
    /// is now an active way to cook the holder, which is the first real use the design has for the
    /// "mobile and useless" state.
    /// </para>
    /// </remarks>
    [RequireComponent(typeof(BallController))]
    public class CoreFuse : MonoBehaviour
    {
        [Title("References")]
        [Required, SerializeField] BallController _core;
        [Required, SerializeField] MatchConfig _config;

        [Title("Runtime")]
        [ShowInInspector, ReadOnly]
        public bool IsArmed => _core != null && _core.State == BallState.Held;

        /// <summary>Fuse left, 1 at pickup down to 0 at detonation.</summary>
        [ShowInInspector, ReadOnly]
        public float Remaining01 { get; private set; } = 1f;

        /// <summary>True once the fuse is short enough that the holder should be warned.</summary>
        [ShowInInspector, ReadOnly]
        public bool IsWarning => IsArmed && Remaining01 <= _config.FuseWarningFraction;

        [ShowInInspector, ReadOnly]
        public int ArmedSlot => _core != null ? _core.HolderSlot : -1;

        float _remaining;
        float _duration = 1f;
        float _heat01;
        bool _detonating;

        /// <summary>Drives how fast the fuse burns. Called by the heat broadcaster (16).</summary>
        public void SetHeat(float heat01) => _heat01 = Mathf.Clamp01(heat01);

        void Update()
        {
            if (!IsArmed)
            {
                Disarm();
                return;
            }

            // Re-armed on pickup, so every carrier gets a full fuse rather than inheriting whatever
            // the last one left on the clock.
            if (_duration <= 0f || _remaining <= 0f && !_detonating) Arm();

            if (_detonating)
            {
                Detonate();
                return;
            }

            _remaining -= Time.deltaTime;
            Remaining01 = Mathf.Clamp01(_remaining / Mathf.Max(0.0001f, _duration));

            if (_remaining > 0f) return;

            _detonating = true;
            Detonate();
        }

        void Arm()
        {
            _duration = Mathf.Max(0.0001f, _config.HoldFuseFor(_heat01));
            _remaining = _duration;
            Remaining01 = 1f;
            _detonating = false;
        }

        void Disarm()
        {
            if (_duration > 0f && !Mathf.Approximately(Remaining01, 1f)) Remaining01 = 1f;

            _duration = 0f;
            _remaining = 0f;
            _detonating = false;
        }

        /// <summary>
        /// Vaporises the carrier.
        /// </summary>
        /// <remarks>
        /// Retried rather than dropped when the holder happens to be mid-dodge: i-frames represent
        /// getting out of the way of a projectile, and there is nowhere to dodge to when the thing
        /// going off is in your own hands. Dodge i-frames last 0.20s against a 1.2s cooldown, so the
        /// retry cannot be chained into an escape - it only ever delays the blast by a fraction.
        /// </remarks>
        void Detonate()
        {
            int slot = _core.HolderSlot;
            if (slot < 0) { Disarm(); return; }

            IBallTarget holder = BallTargetRegistry.Find(slot);
            if (holder == null) { Disarm(); return; }

            if (holder.IsImmune) return;

            Remaining01 = 0f;
            EventBus<CoreDetonated>.Raise(new CoreDetonated(slot, transform.position));

            // A full KO's worth of knocks: holding too long ends you outright, exactly as being hit
            // by a critical core does.
            holder.TakeKnock(_config.KnocksToKo, Vector3.up, 1f);

            Disarm();
        }
    }
}
