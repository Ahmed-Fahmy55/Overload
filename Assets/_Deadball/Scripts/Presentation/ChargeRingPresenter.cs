using Deadball.Fighters;
using Sirenix.OdinInspector;
using UnityEngine;

namespace Deadball.Presentation
{
    /// <summary>
    /// The charge ring on the character, plus the catch and lockout tells.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The ring lives on the character rather than in the HUD (GDD section 19), and it is telegraph
    /// layer 1: it tells the other player that a throw is coming and roughly how hard, so they start
    /// repositioning before the ball exists.
    /// </para>
    /// <para>
    /// It draws as an arc on a <see cref="LineRenderer"/> rather than as a radial-fill shader, which
    /// keeps it working on greybox capsules with no material authoring at all.
    /// </para>
    /// </remarks>
    [RequireComponent(typeof(LineRenderer))]
    public class ChargeRingPresenter : MonoBehaviour
    {
        [Required, SerializeField] FighterThrower _thrower;
        [Required, SerializeField] FighterCatcher _catcher;

        [Title("Ring")]
        [SuffixLabel("m", true), MinValue(0.1f), SerializeField] float _radius = 0.75f;
        [MinValue(8), SerializeField] int _segments = 48;
        [SuffixLabel("m", true), SerializeField] float _height = 0.05f;

        [Title("Colours")]
        [SerializeField] Color _chargeLow = new(1f, 0.95f, 0.8f);
        [SerializeField] Color _chargeHigh = new(1f, 0.45f, 0.1f);
        [SerializeField] Color _catchActive = Color.white;
        [SerializeField] Color _lockedOut = new(0.35f, 0.35f, 0.4f);

        [Title("Width")]
        [SerializeField] float _minWidth = 0.05f;
        [SerializeField] float _maxWidth = 0.14f;

        [Title("Max-Charge Snap", "22 - the ring snaps shut at full charge")]
        [Tooltip("How long the snap reads for. Short on purpose: it is a punctuation mark, not a state.")]
        [SuffixLabel("s", true), MinValue(0f), SerializeField] float _snapDuration = 0.18f;

        [Tooltip("How far the ring overshoots outwards before settling.")]
        [SuffixLabel("m", true), SerializeField] float _snapOvershoot = 0.22f;

        [SerializeField] Color _snapColour = Color.white;

        LineRenderer _line;
        float _snapEndsAt = -1f;
        bool _wasMaxed;

        void Awake()
        {
            _line = GetComponent<LineRenderer>();
            _line.useWorldSpace = false;
            _line.loop = false;
        }

        void LateUpdate()
        {
            // Three mutually exclusive states share the ring, in priority order: a lockout is the
            // most urgent thing a player needs to know, then an open catch window, then charge.
            if (_catcher.IsLockedOut)
            {
                _snapEndsAt = -1f;
                _wasMaxed = false;
                Draw(_catcher.LockoutProgress, _lockedOut, _minWidth);
                return;
            }

            if (_catcher.IsWindowActive)
            {
                Draw(1f, _catchActive, _maxWidth);
                return;
            }

            if (_thrower.Charge01 <= 0.001f)
            {
                _line.enabled = false;
                return;
            }

            float charge = _thrower.Charge01;

            // The crossing is detected here rather than by listening for the global event, because
            // this presenter only ever cares about its own fighter and would otherwise have to
            // filter by slot to ignore the opponent's snap.
            bool maxed = charge >= 1f;
            if (maxed && !_wasMaxed) _snapEndsAt = Time.time + _snapDuration;
            _wasMaxed = maxed;

            if (_snapEndsAt > 0f && Time.time < _snapEndsAt)
            {
                // Overshoot and fall back, so the ring visibly slams shut instead of just changing
                // colour: an eased return reads as a snap at this duration.
                float t = 1f - Mathf.Clamp01((_snapEndsAt - Time.time) / Mathf.Max(0.0001f, _snapDuration));
                float ease = 1f - (1f - t) * (1f - t);
                Draw(1f, Color.Lerp(_snapColour, _chargeHigh, ease), _maxWidth * 1.6f,
                    _radius + _snapOvershoot * (1f - ease));
                return;
            }

            Draw(charge, Color.Lerp(_chargeLow, _chargeHigh, charge), Mathf.Lerp(_minWidth, _maxWidth, charge));
        }

        void Draw(float fill01, Color colour, float width) => Draw(fill01, colour, width, _radius);

        void Draw(float fill01, Color colour, float width, float radius)
        {
            _line.enabled = true;

            int count = Mathf.Max(2, Mathf.CeilToInt(_segments * Mathf.Clamp01(fill01)) + 1);
            float sweep = Mathf.Clamp01(fill01) * Mathf.PI * 2f;

            _line.positionCount = count;

            for (int i = 0; i < count; i++)
            {
                float angle = sweep * (i / (float)(count - 1));
                _line.SetPosition(i, new Vector3(Mathf.Sin(angle) * radius, _height, Mathf.Cos(angle) * radius));
            }

            _line.startColor = colour;
            _line.endColor = colour;
            _line.widthMultiplier = width;
        }
    }
}
