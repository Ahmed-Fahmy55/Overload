using System.Collections.Generic;
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

        [Tooltip("Optional. Drives the hum pitch. The alarm bed is the round clock's, not heat's.")]
        [SerializeField] OverloadAudioDirector _audio;

        [Tooltip("Optional. Drives the core's colour ramp.")]
        [SerializeField] BallVisualPresenter _visuals;

        [Tooltip("Burns the hold fuse faster as the core heats up.")]
        [SerializeField] Deadball.Ball.CoreFuse _fuse;

        // Rebuilt only when the set of cores changes - see RefreshCoreCache.
        readonly List<BallVisualPresenter> _coreVisuals = new();
        readonly List<CoreFuse> _coreFuses = new();
        int _cachedCoreCount = -1;

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

            // Every core again, for the same reason the critical flag goes to all of them: the
            // colour ramp and the hold fuse are per-core components that the clones already carry,
            // and feeding only the serialized one left the spares cold-coloured and slow-fused
            // through a rally that had heated the deck right up.
            RefreshCoreCache();

            if (_coreVisuals.Count > 0)
            {
                for (int i = 0; i < _coreVisuals.Count; i++) _coreVisuals[i].SetHeat(heat01);
            }
            else if (_visuals != null)
            {
                _visuals.SetHeat(heat01);
            }

            if (_coreFuses.Count > 0)
            {
                for (int i = 0; i < _coreFuses.Count; i++) _coreFuses[i].SetHeat(heat01);
            }
            else if (_fuse != null)
            {
                _fuse.SetHeat(heat01);
            }
        }

        /// <summary>Rebuilds the per-core component lists when the set of cores has changed.</summary>
        /// <remarks>
        /// Heat changes on every frame it bleeds, so this cannot be a GetComponent sweep. Cores are
        /// cloned once when a match loads and then live for its duration, so a rebuild is a
        /// per-match cost. The null check covers the round that follows a core being destroyed:
        /// the count alone would miss a swap that happened to keep it the same.
        /// </remarks>
        void RefreshCoreCache()
        {
            var cores = CoreRegistry.Cores;

            bool stale = cores.Count != _cachedCoreCount;
            for (int i = 0; !stale && i < _coreVisuals.Count; i++)
                if (_coreVisuals[i] == null) stale = true;
            for (int i = 0; !stale && i < _coreFuses.Count; i++)
                if (_coreFuses[i] == null) stale = true;

            if (!stale) return;

            _cachedCoreCount = cores.Count;
            _coreVisuals.Clear();
            _coreFuses.Clear();

            for (int i = 0; i < cores.Count; i++)
            {
                if (cores[i] == null) continue;

                var visuals = cores[i].GetComponent<BallVisualPresenter>();
                if (visuals != null) _coreVisuals.Add(visuals);

                var fuse = cores[i].GetComponent<CoreFuse>();
                if (fuse != null) _coreFuses.Add(fuse);
            }
        }

        void OnCriticalChanged(bool critical)
        {
            // The core carries the flag itself so a hit can be resolved without a lookup (9).
            //
            // Every core on the deck, not just the one wired in the inspector. Heat is a property
            // of the rally rather than of one ball, so with several in play the serialized
            // reference would have been the only one that killed in a touch and the rest would
            // have gone on dealing ordinary knocks through a critical rally.
            var cores = CoreRegistry.Cores;
            if (cores.Count > 0)
            {
                for (int i = 0; i < cores.Count; i++)
                    if (cores[i] != null) cores[i].IsCritical = critical;
            }
            else if (_core != null)
            {
                _core.IsCritical = critical;
            }

            // Audio is deliberately not told. The containment alarm belongs to the round clock
            // now - see LowTimeAlarm - because a bed that fills the room has to mean something
            // both players share. Heat still drives the hum's pitch through OnHeatChanged.
            if (_visuals != null) _visuals.SetCritical(critical);
        }
    }
}
