using Deadball.Ball;
using Sirenix.OdinInspector;
using UnityEngine;

namespace Deadball.Presentation
{
    /// <summary>
    /// The ball's drop shadow (GDD section 8.6). Readability, not polish - build it Day 1.
    /// </summary>
    /// <remarks>
    /// Depth is unreadable from a top-down camera: a ball flying along the camera axis barely moves
    /// on screen, so a player cannot tell whether it is coming at them or sailing past. The shadow's
    /// motion across the floor reads perfectly from above even when the ball itself looks static,
    /// which is why the design lists this as one of the four things never to cut.
    /// </remarks>
    [ExecuteAlways]
    public class BallShadowPresenter : MonoBehaviour
    {
        [Required, SerializeField] BallController _ball;
        [Required, SerializeField] Transform _shadow;

        [Title("Projection")]
        [SuffixLabel("m", true), SerializeField] float _groundHeight = 0.02f;
        [SuffixLabel("m", true), MinValue(0.01f), SerializeField] float _baseScale = 0.6f;

        [Tooltip("How much the blob shrinks as the ball climbs, per metre of height.")]
        [PropertyRange(0f, 1f), SerializeField] float _shrinkPerMetre = 0.12f;

        [Tooltip("Height at which the shadow has fully faded out.")]
        [SuffixLabel("m", true), MinValue(0.1f), SerializeField] float _fadeHeight = 6f;

        Renderer _renderer;
        MaterialPropertyBlock _block;
        static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        Color _tint = Color.black;

        // [ExecuteAlways] means Awake also runs the moment the component is added, which for the
        // scene builder is before the shadow reference has been wired. Resolution is therefore
        // deferred to the first frame that actually has both references.
        void CacheRenderer()
        {
            _block ??= new MaterialPropertyBlock();

            if (_renderer != null || _shadow == null) return;

            _renderer = _shadow.GetComponent<Renderer>();

            if (_renderer != null)
                _tint = _renderer.sharedMaterial != null ? _renderer.sharedMaterial.color : Color.black;
        }

        void LateUpdate()
        {
            if (_ball == null || _shadow == null) return;

            CacheRenderer();

            Vector3 ballPosition = _ball.transform.position;
            float height = Mathf.Max(0f, ballPosition.y - _groundHeight);

            _shadow.position = new Vector3(ballPosition.x, _groundHeight, ballPosition.z);
            _shadow.rotation = Quaternion.Euler(90f, 0f, 0f);

            float scale = _baseScale * Mathf.Max(0.2f, 1f - height * _shrinkPerMetre);
            _shadow.localScale = new Vector3(scale, scale, scale);

            ApplyAlpha(Mathf.Clamp01(1f - height / _fadeHeight));
        }

        void ApplyAlpha(float alpha)
        {
            if (_renderer == null) return;

            _renderer.GetPropertyBlock(_block);
            _block.SetColor(BaseColorId, new Color(_tint.r, _tint.g, _tint.b, _tint.a * alpha));
            _renderer.SetPropertyBlock(_block);
        }
    }
}
