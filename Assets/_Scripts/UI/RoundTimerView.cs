using UnityEngine;
using UnityEngine.UI;
using _Scripts.Controller;

namespace _Scripts.UI
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Slider))]
    public sealed class RoundTimerView : MonoBehaviour
    {
        [SerializeField] private Slider _slider;

        private ApplicationManager _applicationManager;

        private void Awake()
        {
            if (_slider == null) _slider = GetComponent<Slider>();
        }

        private void OnEnable()
        {
            _applicationManager = ApplicationManager.Instance;
            if (_applicationManager == null || _slider == null) return;

            _slider.minValue = 0f;
            _slider.maxValue = Mathf.Max(0.01f, _applicationManager.RoundDuration);
            _applicationManager.OnRemainingTimeChanged += HandleRemainingTimeChanged;
            HandleRemainingTimeChanged(_applicationManager.RemainingTime);
        }

        private void OnDisable()
        {
            if (_applicationManager != null) _applicationManager.OnRemainingTimeChanged -= HandleRemainingTimeChanged;
            _applicationManager = null;
        }

        private void HandleRemainingTimeChanged(float remainingTime)
        {
            if (_slider != null) _slider.value = remainingTime;
        }
    }
}
