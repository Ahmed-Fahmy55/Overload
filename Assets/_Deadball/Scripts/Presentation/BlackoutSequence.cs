using System.Collections;
using System.Collections.Generic;
using Core.Events;
using Deadball.Events;
using Sirenix.OdinInspector;
using UnityEngine;
using UnityEngine.UI;

namespace Deadball.Presentation
{
    /// <summary>
    /// The blackout on the final blow (OVERLOAD GDD sections 3 and 22).
    /// </summary>
    /// <remarks>
    /// <para>
    /// "Every knockout in this game is also a blackout. That is the title, and it is also the
    /// mechanic." The core going critical takes the district's power with it: the deck lights cut to
    /// black for half a second, then flip to emergency red.
    /// </para>
    /// <para>
    /// It drives the real scene lights rather than just tinting the screen, so the deck genuinely
    /// goes dark and the runners lose their key light with it.
    /// </para>
    /// </remarks>
    public class BlackoutSequence : MonoBehaviour
    {
        [Title("Timing")]
        [SuffixLabel("s", true), MinValue(0f)]
        [Tooltip("How long the sector stays fully dark before emergency power kicks in.")]
        [SerializeField] float _blackoutDuration = 0.5f;

        [SuffixLabel("s", true), MinValue(0.01f), SerializeField] float _recoverDuration = 0.6f;

        [Title("Emergency Power")]
        [SerializeField] Color _emergencyColour = new(1f, 0.13f, 0.1f);

        [Tooltip("How much of the original brightness emergency power restores.")]
        [PropertyRange(0f, 1f), SerializeField] float _emergencyLevel = 0.55f;

        [Title("Scene References")]
        [Tooltip("Lights that lose power. Usually the deck lamps.")]
        [SerializeField] Transform _deckLights;

        [Tooltip("Full-screen image used to force true black during the cut.")]
        [SerializeField] Image _blackoutOverlay;

        [Title("Runtime"), ShowInInspector, ReadOnly]
        public bool IsBlackedOut { get; private set; }

        readonly List<Light> _lights = new();
        readonly List<Color> _originalColours = new();
        readonly List<float> _originalIntensities = new();

        EventBinding<MatchEnded> _matchEnded;
        EventBinding<RoundStarting> _roundStarting;
        Coroutine _running;
        Color _originalAmbient;

        void Awake()
        {
            if (_deckLights != null)
                _lights.AddRange(_deckLights.GetComponentsInChildren<Light>(true));

            foreach (Light l in _lights)
            {
                _originalColours.Add(l.color);
                _originalIntensities.Add(l.intensity);
            }

            _originalAmbient = RenderSettings.ambientSkyColor;
        }

        void OnEnable()
        {
            _matchEnded = new EventBinding<MatchEnded>(() => Play());
            _roundStarting = new EventBinding<RoundStarting>(() => Restore());

            EventBus<MatchEnded>.Register(_matchEnded);
            EventBus<RoundStarting>.Register(_roundStarting);
        }

        void OnDisable()
        {
            EventBus<MatchEnded>.Deregister(_matchEnded);
            EventBus<RoundStarting>.Deregister(_roundStarting);
        }

        /// <summary>Cuts the power, then brings it back on emergency red.</summary>
        [Button("Play Blackout"), DisableInEditorMode]
        public void Play()
        {
            if (_running != null) StopCoroutine(_running);
            _running = StartCoroutine(Run());
        }

        /// <summary>Puts the sector back on main power. Called when a new match starts.</summary>
        public void Restore()
        {
            if (_running != null) { StopCoroutine(_running); _running = null; }

            for (int i = 0; i < _lights.Count; i++)
            {
                if (_lights[i] == null) continue;
                _lights[i].color = _originalColours[i];
                _lights[i].intensity = _originalIntensities[i];
            }

            RenderSettings.ambientSkyColor = _originalAmbient;
            SetOverlay(0f);
            IsBlackedOut = false;
        }

        IEnumerator Run()
        {
            IsBlackedOut = true;

            // Cut. Everything off, including ambient, so the deck is genuinely dark rather than
            // dimmed - the overlay only guarantees true black on top of it.
            foreach (Light l in _lights)
                if (l != null) l.intensity = 0f;

            RenderSettings.ambientSkyColor = Color.black;
            SetOverlay(1f);

            // Unscaled: the KO slow-mo is still running and this should not stretch with it.
            yield return new WaitForSecondsRealtime(_blackoutDuration);

            // Emergency power: red, and dimmer than before.
            for (int i = 0; i < _lights.Count; i++)
            {
                if (_lights[i] == null) continue;
                _lights[i].color = _emergencyColour;
            }

            float elapsed = 0f;
            while (elapsed < _recoverDuration)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = Mathf.Clamp01(elapsed / _recoverDuration);

                for (int i = 0; i < _lights.Count; i++)
                {
                    if (_lights[i] == null) continue;
                    _lights[i].intensity = Mathf.Lerp(0f, _originalIntensities[i] * _emergencyLevel, t);
                }

                RenderSettings.ambientSkyColor = Color.Lerp(Color.black, _emergencyColour * 0.12f, t);
                SetOverlay(Mathf.Lerp(1f, 0f, t));

                yield return null;
            }

            SetOverlay(0f);
            _running = null;
        }

        void SetOverlay(float alpha)
        {
            if (_blackoutOverlay == null) return;

            Color c = _blackoutOverlay.color;
            _blackoutOverlay.color = new Color(0f, 0f, 0f, alpha);
            _blackoutOverlay.enabled = alpha > 0.001f;
        }
    }
}
