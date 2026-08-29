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

        [Tooltip("Supplies the deck bounds. Without it every core is framed wherever it ends up.")]
        [SerializeField] Deadball.Match.ArenaReferences _arena;

        [Title("Bounds")]
        [Tooltip("How far past the containment field a core may drift and still be framed. Small, "
            + "so a core resting against the wall does not flicker in and out of the group.")]
        [SuffixLabel("m", true), MinValue(0f), SerializeField] float _boundsMargin = 1.5f;

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

            // Cores are added and dropped continuously by TrackCores, not fixed here: one that has
            // left the deck must stop dragging the frame with it.
            TrackCores();
        }

        void LateUpdate() => TrackCores();

        /// <summary>
        /// Keeps the group holding exactly the cores that are still on the deck.
        /// </summary>
        /// <remarks>
        /// A core that clears the containment field - through a gap, or off the edge - would
        /// otherwise pull the framing out toward it forever, shrinking the runners to specks over
        /// an empty deck. It is dropped while it is away and picked back up if it returns, so a
        /// core that bounces back into play is framed again without anything having to notice.
        /// </remarks>
        void TrackCores()
        {
            if (_group == null) return;

            var cores = Deadball.Ball.CoreRegistry.Cores;
            if (cores.Count == 0)
            {
                if (_core != null) Track(_core.transform, IsOnDeck(_core.transform.position));
                return;
            }

            for (int i = 0; i < cores.Count; i++)
            {
                var core = cores[i];
                if (core == null) continue;

                Track(core.transform, IsOnDeck(core.transform.position));
            }
        }

        void Track(Transform target, bool shouldBeFramed)
        {
            int index = _group.FindMember(target);

            if (shouldBeFramed && index < 0) _group.AddMember(target, _coreWeight, _coreRadius);
            else if (!shouldBeFramed && index >= 0) _group.RemoveMember(target);
        }

        bool IsOnDeck(Vector3 position)
        {
            // No arena reference means no way to judge, so everything counts as on the deck rather
            // than the camera silently dropping every core.
            if (_arena == null) return true;

            Vector3 centre = _arena.Centre;
            Vector2 half = _arena.Size * 0.5f;

            return Mathf.Abs(position.x - centre.x) <= half.x + _boundsMargin
                && Mathf.Abs(position.z - centre.z) <= half.y + _boundsMargin;
        }
    }
}
