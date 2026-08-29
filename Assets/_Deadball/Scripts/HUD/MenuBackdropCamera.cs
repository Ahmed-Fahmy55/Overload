using Sirenix.OdinInspector;
using UnityEngine;

namespace Deadball.HUD
{
    /// <summary>
    /// Keeps the menu's backdrop deck alive behind the UI.
    /// </summary>
    /// <remarks>
    /// <para>
    /// A still render of an arena reads as a screenshot pasted behind the buttons. Two cheap motions
    /// fix that: a slow orbit that never repeats on a round number, and a small continuous shake so
    /// the deck feels like a structure under load rather than a photograph.
    /// </para>
    /// <para>
    /// Both are deliberately under-driven. The menu is where the player reads text, so the motion
    /// has to be felt rather than watched - anything faster and the buttons start to swim.
    /// </para>
    /// </remarks>
    public class MenuBackdropCamera : MonoBehaviour
    {
        [Title("Framing")]
        [Tooltip("What the camera orbits and looks at. Usually the centre of the deck.")]
        [SerializeField] Transform _target;

        [SuffixLabel("m", true), SerializeField] float _distance = 26f;
        [SuffixLabel("m", true), SerializeField] float _height = 13f;

        [Title("Drift")]
        [Tooltip("Degrees per second. Slow enough that it reads as drift, not as a turntable.")]
        [SuffixLabel("deg/s", true), SerializeField] float _orbitSpeed = 2.2f;

        [Tooltip("How far the height sways over the cycle.")]
        [SuffixLabel("m", true), SerializeField] float _bobAmplitude = 1.1f;

        [SuffixLabel("Hz", true), SerializeField] float _bobSpeed = 0.07f;

        [Title("Shake")]
        [Tooltip("A structure under load, not an earthquake.")]
        [SuffixLabel("m", true), SerializeField] float _shakeAmplitude = 0.05f;

        [SuffixLabel("Hz", true), SerializeField] float _shakeSpeed = 1.4f;

        float _angle;

        void Start() => _angle = 35f;

        void LateUpdate()
        {
            if (_target == null) return;

            _angle += _orbitSpeed * Time.unscaledDeltaTime;

            float t = Time.unscaledTime;
            float height = _height + Mathf.Sin(t * _bobSpeed * Mathf.PI * 2f) * _bobAmplitude;

            Vector3 orbit = Quaternion.Euler(0f, _angle, 0f) * new Vector3(0f, 0f, -_distance);
            Vector3 position = _target.position + orbit + Vector3.up * height;

            // Perlin rather than a sine so the two axes never line up into a visible circle.
            position += new Vector3(
                (Mathf.PerlinNoise(t * _shakeSpeed, 0f) - 0.5f) * 2f,
                (Mathf.PerlinNoise(0f, t * _shakeSpeed) - 0.5f) * 2f,
                (Mathf.PerlinNoise(t * _shakeSpeed, t * _shakeSpeed) - 0.5f) * 2f) * _shakeAmplitude;

            transform.position = position;
            transform.LookAt(_target.position + Vector3.up * 1.5f);
        }
    }
}
