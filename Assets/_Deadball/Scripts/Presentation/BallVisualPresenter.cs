using Core.Events;
using Deadball.Ball;
using Deadball.Config;
using Deadball.Events;
using Sirenix.OdinInspector;
using UnityEngine;

namespace Deadball.Presentation
{
    /// <summary>
    /// Ball tint, trail and flash - telegraph layers 2 and 3 (GDD sections 8.2 and 11.2).
    /// </summary>
    /// <remarks>
    /// <para>
    /// Three separate readability jobs happen to share one renderer, so they share one presenter.
    /// The tint tells you who is holding it. The trail tells you how hard it was thrown, and
    /// therefore how much time you have. The flash tells you to press now.
    /// </para>
    /// <para>
    /// The ball must be the brightest object on screen at all times - it is the thing the player's
    /// eye is never allowed to lose (17).
    /// </para>
    /// </remarks>
    public class BallVisualPresenter : MonoBehaviour
    {
        [Title("References")]
        [Required, SerializeField] BallController _ball;
        [Required, SerializeField] Renderer _renderer;
        [Required, SerializeField] TrailRenderer _trail;
        [Required, SerializeField] FighterPalette _palette;

        [Title("Trail")]
        [Tooltip("A soft lob is a thin white streak; a max-charge shot is a fat hot one (8.2).")]
        [SuffixLabel("m", true), SerializeField] float _minTrailWidth = 0.08f;
        [SuffixLabel("m", true), SerializeField] float _maxTrailWidth = 0.45f;
        [SuffixLabel("s", true), SerializeField] float _minTrailTime = 0.12f;
        [SuffixLabel("s", true), SerializeField] float _maxTrailTime = 0.35f;

        [Title("Flash")]
        [SuffixLabel("s", true), MinValue(0.01f), SerializeField] float _flashDuration = 0.12f;
        [SerializeField] Color _flashColour = Color.white;

        [Title("Loose Pulse")]
        [SuffixLabel("Hz", true), SerializeField] float _pulseSpeed = 2.2f;
        [PropertyRange(0f, 1f), SerializeField] float _pulseDepth = 0.35f;

        static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        static readonly int EmissionColorId = Shader.PropertyToID("_EmissionColor");

        MaterialPropertyBlock _block;
        EventBinding<BallThrown> _thrownBinding;
        float _flashRemaining;
        int _lastThrowerSlot;

        void Awake() => _block = new MaterialPropertyBlock();

        void OnEnable()
        {
            _ball.StateChanged += OnStateChanged;
            _ball.FlashCueFired += Flash;

            _thrownBinding = new EventBinding<BallThrown>(evt => _lastThrowerSlot = evt.ThrowerSlot);
            EventBus<BallThrown>.Register(_thrownBinding);
        }

        void OnDisable()
        {
            _ball.StateChanged -= OnStateChanged;
            _ball.FlashCueFired -= Flash;
            EventBus<BallThrown>.Deregister(_thrownBinding);
        }

        void LateUpdate()
        {
            if (_flashRemaining > 0f)
                _flashRemaining -= Time.unscaledDeltaTime;

            ApplyColour();
            ApplyTrail();
        }

        /// <summary>Punches the ball white. Used by the catch cue and by a successful catch.</summary>
        public void Flash() => _flashRemaining = _flashDuration;

        void OnStateChanged(BallState previous, BallState current)
        {
            if (current == BallState.Caught)
                Flash();

            if (current != BallState.Flying)
                _trail.Clear();

            _trail.emitting = current == BallState.Flying;
        }

        void ApplyColour()
        {
            Color target = OwnerColour();

            if (_ball.State == BallState.Loose)
            {
                float pulse = 1f + Mathf.Sin(Time.time * _pulseSpeed * Mathf.PI * 2f) * _pulseDepth;
                target *= pulse;
            }

            if (_flashRemaining > 0f)
                target = Color.Lerp(target, _flashColour, _flashRemaining / _flashDuration);

            _renderer.GetPropertyBlock(_block);
            _block.SetColor(BaseColorId, target);
            _block.SetColor(EmissionColorId, target * 2f);
            _renderer.SetPropertyBlock(_block);
        }

        void ApplyTrail()
        {
            float charge = _ball.Charge01;
            float width = Mathf.Lerp(_minTrailWidth, _maxTrailWidth, charge);

            _trail.widthMultiplier = width;
            _trail.time = Mathf.Lerp(_minTrailTime, _maxTrailTime, charge);

            Color trailColour = OwnerColour();
            _trail.startColor = trailColour;
            _trail.endColor = new Color(trailColour.r, trailColour.g, trailColour.b, 0f);
        }

        /// <summary>
        /// While flying, the ball keeps the thrower's colour so the target can read where it came
        /// from at a glance; loose, it goes neutral so neither player reads it as theirs.
        /// </summary>
        Color OwnerColour()
        {
            int slot = _ball.HolderSlot;
            if (slot >= 0) return _palette.BodyColour(slot);

            return _ball.State == BallState.Flying
                ? _palette.TrailColour(_lastThrowerSlot)
                : _palette.LooseBallColour;
        }
    }
}
