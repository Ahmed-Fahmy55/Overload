using Sirenix.OdinInspector;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Deadball.HUD
{
    /// <summary>
    /// Writes a slider's level next to it as text.
    /// </summary>
    /// <remarks>
    /// The slider itself is a stock <see cref="Slider"/>, which already handles its own fill rect
    /// and already steps on left and right while it holds focus - so nothing here re-implements
    /// either. This only keeps the caption honest.
    /// </remarks>
    [RequireComponent(typeof(Slider))]
    public class SliderLabel : MonoBehaviour
    {
        [Required, SerializeField] TextMeshProUGUI _label;

        [SerializeField] string _title = "MASTER";

        Slider _slider;

        void Awake()
        {
            _slider = GetComponent<Slider>();
            _slider.onValueChanged.AddListener(_ => Refresh());
            Refresh();
        }

        void OnEnable() => Refresh();

        void Refresh()
        {
            if (_label == null) return;
            if (_slider == null) _slider = GetComponent<Slider>();

            _label.text = $"{_title}   {Mathf.RoundToInt(_slider.value * 100f)}%";
        }
    }
}
