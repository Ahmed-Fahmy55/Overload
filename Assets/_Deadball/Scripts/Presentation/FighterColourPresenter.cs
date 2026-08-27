using Deadball.Config;
using Deadball.Fighters;
using Sirenix.OdinInspector;
using UnityEngine;

namespace Deadball.Presentation
{
    /// <summary>
    /// Applies a slot's colour to the body and the ground ring (GDD section 11.2).
    /// </summary>
    /// <remarks>
    /// Player distinction is non-negotiable for readability, and it has to hold on capsules before it
    /// holds on Synty characters - if you cannot tell the fighters apart in greybox, no amount of art
    /// on Day 2 will save it.
    /// </remarks>
    public class FighterColourPresenter : MonoBehaviour
    {
        [Required, SerializeField] Fighter _fighter;
        [Required, SerializeField] FighterPalette _palette;

        [Title("Renderers")]
        [Tooltip("Body meshes tinted to the slot colour.")]
        [SerializeField] Renderer[] _bodyRenderers;

        [Tooltip("Ground ring under the feet.")]
        [SerializeField] Renderer _groundRing;

        [Tooltip("Suit light. On a near-black deck this is what stops a dark suit disappearing.")]
        [SerializeField] Light _suitLight;

        [Title("Feedback")]
        [PropertyRange(0f, 1f), SerializeField] float _knockedFlashFade = 0.6f;

        static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        static readonly int EmissionColorId = Shader.PropertyToID("_EmissionColor");

        MaterialPropertyBlock _block;
        int _appliedSlot = int.MinValue;

        void Awake() => _block = new MaterialPropertyBlock();

        void LateUpdate()
        {
            if (_fighter.Slot < 0 || _fighter.Slot == _appliedSlot) return;

            Apply(_fighter.Slot);
            _appliedSlot = _fighter.Slot;
        }

        void Apply(int slot)
        {
            Color colour = _palette.BodyColour(slot);

            for (int i = 0; i < _bodyRenderers.Length; i++)
                Tint(_bodyRenderers[i], colour, emissive: true);

            // The ring is dimmer than the body so it frames the silhouette instead of competing
            // with it - the ball has to stay the brightest thing on screen.
            Tint(_groundRing, colour * _knockedFlashFade, emissive: false);

            // Standing in your own coloured light is the cheapest possible substitute for the rim
            // lighting section 17 asks for, and it doubles as identity.
            if (_suitLight != null) _suitLight.color = colour;
        }

        void Tint(Renderer target, Color colour, bool emissive)
        {
            if (target == null) return;

            target.GetPropertyBlock(_block);
            _block.SetColor(BaseColorId, colour);
            if (emissive) _block.SetColor(EmissionColorId, colour * 0.6f);
            target.SetPropertyBlock(_block);
        }
    }
}
