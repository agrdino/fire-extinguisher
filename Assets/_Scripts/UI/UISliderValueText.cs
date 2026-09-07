using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace _Scripts.UI
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Slider))]
    public sealed class UISliderValueText : MonoBehaviour
    {
        [SerializeField] private TMP_Text[] _targets;
        [SerializeField, Range(0, 5)] private int _decimals;
        [SerializeField] private string _prefix = string.Empty;
        [SerializeField] private string _suffix = string.Empty;

        private Slider _slider;

        private void Awake()
        {
            _slider = GetComponent<Slider>();
            if (_targets == null || _targets.Length == 0)
                _targets = GetComponentsInChildren<TMP_Text>(true);
        }

        private void OnEnable()
        {
            if (_slider == null) _slider = GetComponent<Slider>();
            _slider.onValueChanged.AddListener(UpdateText);
            UpdateText(_slider.value);
        }

        private void OnDisable()
        {
            if (_slider != null) _slider.onValueChanged.RemoveListener(UpdateText);
        }

        private void UpdateText(float value)
        {
            string formatted = _prefix + value.ToString("F" + _decimals) + _suffix;
            if (_targets == null) return;
            foreach (TMP_Text target in _targets)
                if (target != null) target.SetText(formatted);
        }
    }
}
