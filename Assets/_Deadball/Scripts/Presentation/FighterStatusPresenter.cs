using Deadball.Fighters;
using Sirenix.OdinInspector;
using UnityEngine;
using UnityEngine.UI;

namespace Deadball.Presentation
{
    /// <summary>
    /// The wind-up and clamp state, on a bar above the runner (OVERLOAD GDD 19).
    /// </summary>
    /// <remarks>
    /// <para>
    /// The ring at the runner's feet is drawn flat on the deck, so from this camera it is both tiny
    /// and hidden behind the runner's own body. The same three states are far easier to read as a
    /// bar floating over the head, which is what this draws. The ring stays as the close-up detail;
    /// this is the one you can read from across the deck.
    /// </para>
    /// <para>
    /// Charge only. The bar used to multiplex three states - lockout, open catch window, charge -
    /// so the thing above a runner changed meaning depending on what they had just done, and you
    /// had to read the colour to know which. The two cooldowns are their own icons at the bottom of
    /// the HUD now, and this is left saying exactly one thing: how hard the throw will be.
    /// </para>
    /// </remarks>
    public class FighterStatusPresenter : MonoBehaviour
    {
        [Title("References")]
        [Required, SerializeField] FighterThrower _thrower;
        [Required, SerializeField] RectTransform _root;
        [Required, SerializeField] Image _fill;
        [SerializeField] Image _background;

        [Title("Colours")]
        [SerializeField] Color _chargeLow = new(1f, 0.95f, 0.8f);
        [SerializeField] Color _chargeHigh = new(1f, 0.45f, 0.1f);

        [Title("Placement")]
        [Tooltip("Height above the runner's feet.")]
        [SuffixLabel("m", true), SerializeField] float _height = 2.35f;

        Camera _camera;
        Canvas _canvas;

        void LateUpdate()
        {
            if (_root == null) return;
            if (_canvas == null) _canvas = _root.GetComponent<Canvas>();

            float charge = _thrower != null ? _thrower.Charge01 : 0f;

            if (charge <= 0.001f) { SetVisible(false); return; }

            Show(charge, Color.Lerp(_chargeLow, _chargeHigh, charge));

            SetVisible(true);
            Face();
        }

        void Show(float fill01, Color colour)
        {
            _fill.fillAmount = Mathf.Clamp01(fill01);
            _fill.color = colour;
        }

        void SetVisible(bool visible)
        {
            if (_root.gameObject.activeSelf != visible) _root.gameObject.SetActive(visible);
        }

        /// <summary>
        /// Billboards the bar and pins it above the runner.
        /// </summary>
        /// <remarks>
        /// The position is set in world space rather than parented directly, because the runner
        /// turns to face their throw and a parented bar would spin with them.
        /// </remarks>
        void Face()
        {
            if (_camera == null)
            {
                _camera = Camera.main;

                // A world-space canvas left without an event camera renders nothing in URP, and
                // the failure is silent: the transform, culling and fill are all still correct.
                if (_camera != null && _canvas != null) _canvas.worldCamera = _camera;
            }

            if (_camera == null) return;

            _root.position = transform.position + Vector3.up * _height;
            _root.rotation = _camera.transform.rotation;
        }
    }
}
