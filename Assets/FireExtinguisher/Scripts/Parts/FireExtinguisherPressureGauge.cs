using UnityEngine;

namespace _Scripts.FireExtinguishers.Visualizes
{
    [DisallowMultipleComponent]
    public sealed class FireExtinguisherPressureGauge : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private FireExtinguisher _fireExtinguisher;
        [SerializeField] private Transform _needle;

        [Header("Gauge Range")]
        [SerializeField] private float _minimumRotation = -60f;
        [SerializeField] private float _maximumRotation = 80f;
        [SerializeField, Min(0f)] private float _minimumAmount = 0f;
        [SerializeField, Min(0f)] private float _maximumAmount = 60f;

        public float MinimumRotation => _minimumRotation;
        public float MaximumRotation => _maximumRotation;
        public float MinimumAmount => _minimumAmount;
        public float MaximumAmount => _maximumAmount;

        private void Reset()
        {
            _fireExtinguisher = GetComponentInParent<FireExtinguisher>();
            _needle = transform.Find("Needle");
        }

        private void OnEnable()
        {
            if (_fireExtinguisher == null) return;
            if (_needle == null) return;
            
            _fireExtinguisher.OnRemainingAmountChanged += FireExtinguisher_OnRemainingAmountChanged;
            SetNeedleRotation(_fireExtinguisher.RemainingAmount);
        }

        private void OnDisable()
        {
            if (_fireExtinguisher == null) return;
            _fireExtinguisher.OnRemainingAmountChanged -= FireExtinguisher_OnRemainingAmountChanged;
        }

        private void SetNeedleRotation(float remainingAmount)
        {
            float normalizedAmount = Mathf.InverseLerp(_minimumAmount, _maximumAmount, remainingAmount);
            float rotation = Mathf.Lerp(_minimumRotation, _maximumRotation, normalizedAmount);
            Vector3 localEulerAngles = _needle.localEulerAngles;
            localEulerAngles.z = rotation;
            _needle.localEulerAngles = localEulerAngles;
        }

        private void FireExtinguisher_OnRemainingAmountChanged(float remainingAmount, float remainingRatio)
        {
            SetNeedleRotation(remainingAmount);
        }
    }
}
