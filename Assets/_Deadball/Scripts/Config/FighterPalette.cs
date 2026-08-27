using Sirenix.OdinInspector;
using UnityEngine;

namespace Deadball.Config
{
    /// <summary>
    /// Per-slot colours for the readability rules in GDD section 11.2.
    /// </summary>
    /// <remarks>
    /// Player distinction is called out as non-negotiable, and it is applied to five separate things
    /// (rim light, ground ring, held-ball tint, trail, knock pips). Those all read from here so a
    /// colour change is one edit instead of five.
    /// </remarks>
    [CreateAssetMenu(menuName = "Deadball/Fighter Palette", fileName = "FighterPalette")]
    public class FighterPalette : ScriptableObject
    {
        [System.Serializable]
        public class SlotColours
        {
            [HorizontalGroup("Slot"), HideLabel, PreviewField(40)]
            public Color Body = Color.cyan;

            [VerticalGroup("Slot/Right"), LabelWidth(70)]
            public string DisplayName = "P1";

            [VerticalGroup("Slot/Right"), LabelWidth(70)]
            public Color Trail = Color.cyan;
        }

        [ListDrawerSettings(DefaultExpandedState = true, ShowFoldout = true)]
        [InfoBox("Slot 0 is Player 1 (cyan), slot 1 is Player 2 (orange).")]
        [SerializeField]
        SlotColours[] _slots =
        {
            new() { DisplayName = "P1", Body = new Color(0.15f, 0.85f, 1f), Trail = new Color(0.4f, 0.95f, 1f) },
            new() { DisplayName = "P2", Body = new Color(1f, 0.5f, 0.1f), Trail = new Color(1f, 0.65f, 0.2f) }
        };

        [Title("Neutral")]
        [SerializeField] Color _looseBallColour = new(1f, 0.95f, 0.75f);

        public Color LooseBallColour => _looseBallColour;

        public SlotColours For(int slot) =>
            _slots is { Length: > 0 } ? _slots[Mathf.Abs(slot) % _slots.Length] : new SlotColours();

        public Color BodyColour(int slot) => For(slot).Body;
        public Color TrailColour(int slot) => For(slot).Trail;
        public string DisplayName(int slot) => For(slot).DisplayName;
    }
}
