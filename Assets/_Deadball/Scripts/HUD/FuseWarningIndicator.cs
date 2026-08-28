using Deadball.Ball;
using Deadball.Fighters;
using Sirenix.OdinInspector;
using TMPro;
using UnityEngine;

namespace Deadball.HUD
{
    /// <summary>
    /// Danger triangles over the runner who is holding a core that is about to go off.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The fuse is the one piece of danger in the game the victim cannot read off the core itself -
    /// heat has a colour ramp and an audio ramp, but "how long have I been holding this" is
    /// invisible. So it goes above the runner, where their own eyes already are, rather than into
    /// the HUD: section 21 keeps the HUD to knock pips and a timer, and the charge ring already set
    /// the precedent that state belongs on the character.
    /// </para>
    /// <para>
    /// It reads by flicker rate rather than by a bar. A bar asks you to measure; a flicker that
    /// keeps getting faster is understood without being read, which is the same trick the heat hum
    /// plays in audio.
    /// </para>
    /// </remarks>
    public class FuseWarningIndicator : MonoBehaviour
    {
        [Title("References")]
        [Required, SerializeField] Fighter _fighter;

        [Tooltip("The triangles. Hidden whenever this runner is not carrying a live core.")]
        [Required, SerializeField] TMP_Text _label;

        [Title("Look")]
        [SerializeField] string _glyphs = "▲ ▲ ▲";
        [SerializeField] Color _warnColour = new(1f, 0.72f, 0.1f);
        [SerializeField] Color _criticalColour = new(1f, 0.15f, 0.1f);

        [Title("Flicker")]
        [Tooltip("Flashes per second when the warning first appears.")]
        [SuffixLabel("Hz", true), MinValue(0.1f), SerializeField] float _slowestFlicker = 2.5f;

        [Tooltip("Flashes per second just before it goes off.")]
        [SuffixLabel("Hz", true), MinValue(0.1f), SerializeField] float _fastestFlicker = 14f;

        [Title("Runtime"), ShowInInspector, ReadOnly]
        public bool IsShowing { get; private set; }

        CoreFuse _fuse;
        float _phase;

        void Awake()
        {
            if (_label != null)
            {
                _label.text = _glyphs;
                _label.enabled = false;
            }
        }

        void LateUpdate()
        {
            if (_label == null || _fighter == null) return;

            // Found lazily: runners are spawned from a prefab into a scene that already owns the
            // core, so there is nothing to wire at author time.
            if (_fuse == null)
            {
                _fuse = Object.FindFirstObjectByType<CoreFuse>();
                if (_fuse == null) return;
            }

            bool mine = _fuse.ArmedSlot == _fighter.Slot;
            IsShowing = mine && _fuse.IsWarning && _fighter.IsInPlay;

            if (!IsShowing)
            {
                _phase = 0f;
                _label.enabled = false;
                return;
            }

            // Urgency runs 0 at the warning threshold to 1 at detonation, so the ramp covers the
            // visible part of the fuse rather than the whole of it.
            float urgency = 1f - Mathf.Clamp01(Mathf.InverseLerp(0f, WarnFraction(), _fuse.Remaining01));

            _phase += Time.deltaTime * Mathf.Lerp(_slowestFlicker, _fastestFlicker, urgency);

            _label.enabled = Mathf.Repeat(_phase, 1f) < 0.5f;
            _label.color = Color.Lerp(_warnColour, _criticalColour, urgency);

            // Face the camera: the runners rotate freely and the triangles must stay readable.
            if (Camera.main != null)
                _label.transform.rotation = Camera.main.transform.rotation;
        }

        /// <summary>
        /// The same threshold the fuse warns at, read once from the core's own config.
        /// </summary>
        /// <remarks>
        /// Taken from the config rather than duplicated here so the ramp starts exactly where the
        /// triangles appear; a second copy of the number would drift the moment either is tuned.
        /// </remarks>
        float WarnFraction()
        {
            if (_warnFraction > 0f) return _warnFraction;

            BallController core = _fuse.GetComponent<BallController>();
            _warnFraction = core != null && core.Config != null
                ? Mathf.Max(0.0001f, core.Config.FuseWarningFraction)
                : 0.6f;

            return _warnFraction;
        }

        float _warnFraction = -1f;
    }
}
