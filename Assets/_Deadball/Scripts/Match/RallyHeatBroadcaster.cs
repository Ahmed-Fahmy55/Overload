using Deadball.Ball;
using Deadball.Presentation;
using Sirenix.OdinInspector;
using UnityEngine;

namespace Deadball.Match
{
    /// <summary>
    /// Pushes Rally Heat out to the systems that react to it (OVERLOAD GDD section 16.3).
    /// </summary>
    /// <remarks>
    /// <para>
    /// Heat has three consumers and they want completely different things from it: the core wants a
    /// one-hit rule, the visuals want a colour ramp, and the audio wants a pitch ramp. Rather than
    /// give <see cref="RallyHeat"/> references to all three - or make all three hunt for it - this
    /// sits in the middle and does the wiring.
    /// </para>
    /// <para>
    /// It is deliberately the only class that knows the full set, so adding the Day 3 screen-edge
    /// vignette is one line here rather than a new dependency inside the heat system.
    /// </para>
    /// </remarks>
    public class RallyHeatBroadcaster : MonoBehaviour
    {
        [Required, SerializeField] RallyHeat _heat;
        [Required, SerializeField] BallController _core;

        [Tooltip("Optional. Drives the hum pitch and the critical alarm bed.")]
        [SerializeField] OverloadAudioDirector _audio;

        [Tooltip("Optional. Drives the core's colour ramp.")]
        [SerializeField] BallVisualPresenter _visuals;

        [Tooltip("Burns the hold fuse faster as the core heats up.")]
        [SerializeField] Deadball.Ball.CoreFuse _fuse;

        void OnEnable()
        {
            _heat.HeatChanged += OnHeatChanged;
            _heat.CriticalChanged += OnCriticalChanged;

            // Push the current state immediately so a late-enabled consumer is never out of sync.
            OnHeatChanged(_heat.Heat, _heat.Heat01);
            OnCriticalChanged(_heat.IsCritical);
        }

        void OnDisable()
        {
            _heat.HeatChanged -= OnHeatChanged;
            _heat.CriticalChanged -= OnCriticalChanged;
        }

        void OnHeatChanged(float heat, float heat01)
        {
            if (_audio != null) _audio.SetHeat(heat01);
            if (_visuals != null) _visuals.SetHeat(heat01);
            if (_fuse != null) _fuse.SetHeat(heat01);
        }

        void OnCriticalChanged(bool critical)
        {
            // The core carries the flag itself so a hit can be resolved without a lookup (9).
            if (_core != null) _core.IsCritical = critical;
            if (_audio != null) _audio.SetCritical(critical);
            if (_visuals != null) _visuals.SetCritical(critical);
        }
    }
}
