using Deadball.Fighters;
using Sirenix.OdinInspector;
using UnityEngine;

namespace Deadball.Presentation
{
    /// <summary>
    /// Picks which runner model this slot wears (OVERLOAD GDD section 11.2).
    /// </summary>
    /// <remarks>
    /// <para>
    /// Both runners share one prefab, because everything that makes a runner a runner - the collider,
    /// the hand anchor, the hitbox, the four parts - is identical. Only the mesh differs, so only the
    /// mesh is swapped, and the join flow can keep spawning a single prefab.
    /// </para>
    /// <para>
    /// Colour still does the heavy lifting for readability; the different silhouettes are a bonus on
    /// top of it, not a replacement for it.
    /// </para>
    /// </remarks>
    public class FighterModelSelector : MonoBehaviour
    {
        [Required, SerializeField] Fighter _fighter;

        [Tooltip("One model per slot. Index 0 is P1, index 1 is P2.")]
        [SerializeField] GameObject[] _models;

        [Title("Runtime"), ShowInInspector, ReadOnly]
        public int AppliedSlot { get; private set; } = -1;

        void LateUpdate()
        {
            if (_fighter == null || _models == null || _models.Length == 0) return;
            if (_fighter.Slot < 0 || _fighter.Slot == AppliedSlot) return;

            Apply(_fighter.Slot);
            AppliedSlot = _fighter.Slot;
        }

        /// <summary>Enables the model for <paramref name="slot"/> and hides the rest.</summary>
        public void Apply(int slot)
        {
            int chosen = Mathf.Abs(slot) % _models.Length;

            for (int i = 0; i < _models.Length; i++)
            {
                if (_models[i] != null)
                    _models[i].SetActive(i == chosen);
            }
        }

        /// <summary>Every renderer under the active model, for the colour presenter to tint.</summary>
        public Renderer[] ActiveRenderers()
        {
            if (_models == null) return System.Array.Empty<Renderer>();

            foreach (GameObject model in _models)
            {
                if (model != null && model.activeSelf)
                    return model.GetComponentsInChildren<Renderer>(includeInactive: false);
            }

            return System.Array.Empty<Renderer>();
        }
    }
}
