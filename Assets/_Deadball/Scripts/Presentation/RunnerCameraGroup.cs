using Core.Events;
using Deadball.Ball;
using Deadball.Events;
using Deadball.Fighters;
using Sirenix.OdinInspector;
using Unity.Cinemachine;
using UnityEngine;

namespace Deadball.Presentation
{
    /// <summary>
    /// Keeps both runners (and the core) inside a Cinemachine target group.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Runners are spawned at runtime by the join flow or the solo roster, so the group cannot be
    /// authored in the scene - it is filled as each fighter claims a slot.
    /// </para>
    /// <para>
    /// The core is included at a lower weight so the framing leans toward the action without a
    /// long throw yanking the camera across the deck.
    /// </para>
    /// </remarks>
    [RequireComponent(typeof(CinemachineTargetGroup))]
    public class RunnerCameraGroup : MonoBehaviour
    {
        [Title("Weights")]
        [Tooltip("Runners drive the framing.")]
        [MinValue(0f), SerializeField] float _runnerWeight = 1f;

        [Tooltip("Radius kept clear around each runner, in metres.")]
        [MinValue(0f), SerializeField] float _runnerRadius = 2.5f;

        [Tooltip("The core pulls the frame toward the action, but only gently.")]
        [PropertyRange(0f, 1f), SerializeField] float _coreWeight = 0.35f;

        [MinValue(0f), SerializeField] float _coreRadius = 1.5f;

        [Title("Scene References")]
        [Tooltip("Optional. Included in the framing at a reduced weight.")]
        [SerializeField] BallController _core;

        CinemachineTargetGroup _group;
        EventBinding<FighterRegistered> _registered;

        void Awake() => _group = GetComponent<CinemachineTargetGroup>();

        void OnEnable()
        {
            _registered = new EventBinding<FighterRegistered>(_ => Rebuild());
            EventBus<FighterRegistered>.Register(_registered);

            Rebuild();
        }

        void OnDisable() => EventBus<FighterRegistered>.Deregister(_registered);

        /// <summary>Rebuilds the group from whoever is currently in the arena.</summary>
        [Button("Rebuild Group"), DisableInEditorMode]
        public void Rebuild()
        {
            if (_group == null) _group = GetComponent<CinemachineTargetGroup>();

            _group.Targets.Clear();

            var targets = BallTargetRegistry.Targets;
            for (int i = 0; i < targets.Count; i++)
            {
                if (targets[i] is Fighter fighter && fighter != null)
                    _group.AddMember(fighter.transform, _runnerWeight, _runnerRadius);
            }

            // Every core is framed, not just the scene's original, or the camera would happily
            // leave three of them off screen.
            var cores = Deadball.Ball.CoreRegistry.Cores;
            if (cores.Count > 0)
            {
                for (int i = 0; i < cores.Count; i++)
                    if (cores[i] != null) _group.AddMember(cores[i].transform, _coreWeight, _coreRadius);
            }
            else if (_core != null)
            {
                _group.AddMember(_core.transform, _coreWeight, _coreRadius);
            }
        }
    }
}
