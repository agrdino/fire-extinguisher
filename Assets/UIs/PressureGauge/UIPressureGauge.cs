using _Scripts.Controller;
using _Scripts.FireExtinguishers;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit.UI;

namespace _Scripts.UI
{
    [DisallowMultipleComponent]
    public sealed class UIPressureGauge : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private GameObject _content;
        [SerializeField] private RectTransform _needle;
        [SerializeField] private LazyFollow _positionFollow;

        [Header("Pressure Targets")]
        [SerializeField] private Transform _co2Pressure;
        [SerializeField] private Transform _powderPressure;

        [Header("Gauge Range")]
        [SerializeField] private float _minimumRotation = -60f;
        [SerializeField] private float _maximumRotation = 80f;
        [SerializeField, Min(0f)] private float _minimumCapacity;
        [SerializeField, Min(0f)] private float _maximumCapacity = 30f;

        private ApplicationManager _applicationManager;
        private FireExtinguisher _fireExtinguisher;

        public float MinimumRotation => _minimumRotation;
        public float MaximumRotation => _maximumRotation;
        public float MinimumCapacity => _minimumCapacity;
        public float MaximumCapacity => _maximumCapacity;

        private void OnEnable()
        {
            _applicationManager = ApplicationManager.Instance;
            _fireExtinguisher = FireExtinguisherController.Instance.FireExtinguisher;

            _applicationManager.OnStateChanged += ApplicationManager_OnStateChanged;
            _applicationManager.OnExtinguisherSelected += ApplicationManager_OnExtinguisherSelected;
            
            _fireExtinguisher.OnRemainingAmountChanged += FireExtinguisher_OnRemainingAmountChanged;
            SetNeedleRotation(_fireExtinguisher.RemainingAmount);
            
            Refresh();
        }

        private void OnDisable()
        {
            if (_applicationManager != null)
            {
                _applicationManager.OnStateChanged -= ApplicationManager_OnStateChanged;
                _applicationManager.OnExtinguisherSelected -= ApplicationManager_OnExtinguisherSelected;
            }

            if (_fireExtinguisher != null) _fireExtinguisher.OnRemainingAmountChanged -= FireExtinguisher_OnRemainingAmountChanged;
        }

        private void Refresh()
        {
            ApplicationState state = _applicationManager.State;
            FireExtinguisherType extinguisherType = _applicationManager.SelectedExtinguisherType;
            bool isSupportedState = state == ApplicationState.SelectExtinguisher || state == ApplicationState.Fighting || state == ApplicationState.Escape;
            Transform pressureTarget = GetPressureTarget(extinguisherType);

            _positionFollow.target = pressureTarget;
            SetVisible(isSupportedState && extinguisherType != FireExtinguisherType.Unselect && pressureTarget != null);
        }

        private Transform GetPressureTarget(FireExtinguisherType extinguisherType)
        {
            return extinguisherType switch
            {
                FireExtinguisherType.CO2 => _co2Pressure,
                FireExtinguisherType.Powder => _powderPressure,
                _ => null
            };
        }

        private void SetVisible(bool isVisible)
        {
            if (_content.activeSelf == isVisible) return;
            _content.SetActive(isVisible);
        }

        private void SetNeedleRotation(float capacity)
        {
            float normalizedCapacity = Mathf.InverseLerp(_minimumCapacity, _maximumCapacity, capacity);
            float rotation = Mathf.Lerp(_minimumRotation, _maximumRotation, normalizedCapacity);
            _needle.localRotation = Quaternion.Euler(0f, 0f, rotation);
        }

        private void ApplicationManager_OnStateChanged(ApplicationState state) => Refresh();
        private void ApplicationManager_OnExtinguisherSelected(FireExtinguisherType extinguisherType) => Refresh();

        private void FireExtinguisher_OnRemainingAmountChanged(float remainingAmount, float remainingRatio)
        {
            SetNeedleRotation(remainingAmount);
        }

    }
}
