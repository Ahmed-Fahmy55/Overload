using Core.Events;
using Deadball.Config;
using Deadball.Events;
using Sirenix.OdinInspector;
using UnityEngine;

namespace Deadball.Presentation
{
    /// <summary>
    /// The fixed top-down camera, and the one exception to it (GDD section 14).
    /// </summary>
    /// <remarks>
    /// <para>
    /// The camera does not follow, zoom or rotate. That is a design decision rather than a shortcut,
    /// and it pays for itself three times: Local Versus needs no extra camera code because both
    /// fighters are always on screen, dodge and catch reads are fair because you can always see the
    /// ball and both fighters, and there are no occlusion, collision or motion-sickness bugs to fix
    /// during a jam.
    /// </para>
    /// <para>
    /// The single allowance is a punch on impacts and catches that returns to the fixed transform.
    /// That is juice, not a camera system.
    /// </para>
    /// </remarks>
    [RequireComponent(typeof(Camera))]
    public class FixedArenaCamera : MonoBehaviour
    {
        [Title("Framing")]
        [Required, SerializeField] MatchConfig _config;
        [Required, SerializeField] Transform _lookTarget;

        [SuffixLabel("deg", true), PropertyRange(20f, 89f), SerializeField] float _tilt = 50f;
        [Tooltip("Extra room around the arena so fighters never sit on the screen edge.")]
        [PropertyRange(1f, 2f), SerializeField] float _framingMargin = 1.15f;

        [Title("Punch")]
        [SuffixLabel("m", true), SerializeField] float _punchOnCatch = 0.35f;
        [SuffixLabel("m", true), SerializeField] float _punchOnKnock = 0.25f;
        [SuffixLabel("s", true), MinValue(0.01f), SerializeField] float _punchDecay = 0.22f;

        /// <summary>Headroom for the walls and a fighter standing against the far side.</summary>
        const float WallClearance = 3f;

        Camera _camera;
        Vector3 _restPosition;
        Quaternion _restRotation;
        Vector3 _punchOffset;
        float _punchRemaining;

        EventBinding<BallCaught> _caught;
        EventBinding<FighterKnocked> _knocked;
        EventBinding<FighterKnockedOut> _knockedOut;

        void Awake()
        {
            _camera = GetComponent<Camera>();
            Frame();
        }

        void OnEnable()
        {
            _caught = new EventBinding<BallCaught>(evt => Punch(_punchOnCatch * Mathf.Lerp(0.6f, 1.4f, evt.Charge01)));
            _knocked = new EventBinding<FighterKnocked>(evt => Punch(_punchOnKnock * Mathf.Lerp(0.6f, 1.4f, evt.Charge01)));
            _knockedOut = new EventBinding<FighterKnockedOut>(() => Punch(_punchOnKnock * 1.6f));

            EventBus<BallCaught>.Register(_caught);
            EventBus<FighterKnocked>.Register(_knocked);
            EventBus<FighterKnockedOut>.Register(_knockedOut);
        }

        void OnDisable()
        {
            EventBus<BallCaught>.Deregister(_caught);
            EventBus<FighterKnocked>.Deregister(_knocked);
            EventBus<FighterKnockedOut>.Deregister(_knockedOut);
        }

        void LateUpdate()
        {
            if (_punchRemaining > 0f)
            {
                // Unscaled, so the punch still reads while hitstop has the game frozen.
                _punchRemaining -= Time.unscaledDeltaTime;
                float t = Mathf.Clamp01(_punchRemaining / _punchDecay);
                transform.position = _restPosition + _punchOffset * t;
                return;
            }

            transform.SetPositionAndRotation(_restPosition, _restRotation);
        }

        /// <summary>Places the camera so the whole lot fits on screen at the configured tilt.</summary>
        [Button("Frame Arena")]
        public void Frame()
        {
            if (_camera == null) _camera = GetComponent<Camera>();
            if (_config == null || _lookTarget == null) return;

            float half = _config.ArenaSize * 0.5f * _framingMargin;
            float radians = _tilt * Mathf.Deg2Rad;

            float halfFovV = _camera.fieldOfView * 0.5f * Mathf.Deg2Rad;
            float halfFovH = Mathf.Atan(Mathf.Tan(halfFovV) * _camera.aspect);

            // The depth axis is foreshortened by the tilt, so the arena needs less vertical frame
            // than its footprint suggests. Ignoring that pushes the camera roughly a third further
            // back than it needs to be and wastes most of the screen on empty ground.
            float verticalExtent = half * Mathf.Sin(radians) + WallClearance * Mathf.Cos(radians);
            float distance = Mathf.Max(verticalExtent / Mathf.Tan(halfFovV), half / Mathf.Tan(halfFovH));

            Vector3 back = new(0f, Mathf.Sin(radians), -Mathf.Cos(radians));

            _restPosition = _lookTarget.position + back * distance;
            _restRotation = Quaternion.LookRotation(_lookTarget.position - _restPosition);

            transform.SetPositionAndRotation(_restPosition, _restRotation);
        }

        public void Punch(float magnitude)
        {
            if (magnitude <= 0f) return;

            // Offset in camera space so the shake reads as screen movement rather than as the
            // camera wandering around the lot.
            Vector2 jitter = Random.insideUnitCircle.normalized;
            _punchOffset = _restRotation * (new Vector3(jitter.x, jitter.y * 0.5f, 0f) * magnitude);
            _punchRemaining = _punchDecay;
        }
    }
}
