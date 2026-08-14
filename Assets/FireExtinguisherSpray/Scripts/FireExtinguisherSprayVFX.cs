using _Scripts.ParticleSystemLerps;
using UnityEngine;

namespace _Scripts.FireExtinguishers.Visualizes
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(ParticleSystemLerpGroup))]
    public sealed class FireExtinguisherSprayVFX : MonoBehaviour
    {
        [SerializeField] private FireExtinguisher _fireExtinguisher;
        [SerializeField] private ParticleSystemLerpGroup _sprayEffect;
        [SerializeField, Min(0f)] private float _blendDuration = 1f;

        [Header("Capacity Fade")]
        [SerializeField, Range(0f, 1f)] private float _fadeStartCapacityRatio = 0.15f;

        private float _currentBlend;
        private float _targetBlend;

        private void Reset()
        {
            _fireExtinguisher = GetComponentInParent<FireExtinguisher>();
            _sprayEffect = GetComponent<ParticleSystemLerpGroup>();
        }

        private void OnEnable()
        {
            _fireExtinguisher.OnCanSprayChanged += FireExtinguisher_OnCanSprayChanged;
            _fireExtinguisher.OnRemainingAmountChanged += FireExtinguisher_OnRemainingAmountChanged;
            UpdateTargetBlend();

            _sprayEffect.SetBlend(_currentBlend);
        }

        private void OnDisable()
        {
            _fireExtinguisher.OnCanSprayChanged -= FireExtinguisher_OnCanSprayChanged;
            _fireExtinguisher.OnRemainingAmountChanged -= FireExtinguisher_OnRemainingAmountChanged;
        }

        private void Update()
        {
            if (Mathf.Approximately(_currentBlend, _targetBlend)) return;
            if (_blendDuration <= 0f) _currentBlend = _targetBlend;
            else _currentBlend = Mathf.MoveTowards(_currentBlend, _targetBlend, Time.deltaTime / _blendDuration);

            _sprayEffect.SetBlend(_currentBlend);
        }

        private void UpdateTargetBlend()
        {
            _targetBlend = _fireExtinguisher.CanSpray ? GetMaximumBlend() : 0f;
            if (_blendDuration > 0f) return;

            _currentBlend = _targetBlend;
            _sprayEffect.SetBlend(_currentBlend);
        }

        private float GetMaximumBlend()
        {
            if (_fadeStartCapacityRatio <= 0f) return 1f;
            return Mathf.InverseLerp(0f, _fadeStartCapacityRatio, _fireExtinguisher.RemainingRatio);
        }

        private void FireExtinguisher_OnCanSprayChanged(bool canSpray) => UpdateTargetBlend();
        private void FireExtinguisher_OnRemainingAmountChanged(float remainingAmount, float remainingRatio) => UpdateTargetBlend();

    }
}
