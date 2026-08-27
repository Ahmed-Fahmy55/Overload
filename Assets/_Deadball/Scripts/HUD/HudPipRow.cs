using System.Collections.Generic;
using Sirenix.OdinInspector;
using UnityEngine;
using UnityEngine.UI;

namespace Deadball.HUD
{
    /// <summary>
    /// A row of pips - knocks remaining, or rounds won (GDD section 19).
    /// </summary>
    /// <remarks>
    /// The HUD is deliberately four things and nothing else: knock pips, a round timer, round-win
    /// pips and the names. No minimap, no ammo, no meters. The charge ring lives on the character,
    /// not up here.
    /// </remarks>
    public class HudPipRow : MonoBehaviour
    {
        [Required, Tooltip("Disabled in the scene; cloned once per pip.")]
        [SerializeField] Image _template;

        [SerializeField] Color _emptyColour = new(1f, 1f, 1f, 0.18f);

        readonly List<Image> _pips = new(4);
        Color _filledColour = Color.white;
        int _filled;

        void Awake()
        {
            if (_template != null)
                _template.gameObject.SetActive(false);
        }

        public void SetColour(Color colour)
        {
            _filledColour = colour;
            Refresh();
        }

        /// <summary>Grows or shrinks the row to <paramref name="count"/> pips, all filled.</summary>
        public void Configure(int count)
        {
            while (_pips.Count < count)
            {
                Image pip = Instantiate(_template, _template.transform.parent);
                pip.gameObject.SetActive(true);
                _pips.Add(pip);
            }

            for (int i = 0; i < _pips.Count; i++)
                _pips[i].gameObject.SetActive(i < count);

            SetFilled(count);
        }

        public void SetFilled(int filled)
        {
            _filled = filled;
            Refresh();
        }

        void Refresh()
        {
            for (int i = 0; i < _pips.Count; i++)
                _pips[i].color = i < _filled ? _filledColour : _emptyColour;
        }
    }
}
