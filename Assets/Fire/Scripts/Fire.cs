using System;
using UnityEngine;
using UnityEngine.Serialization;

namespace _Scripts.Fires
{
    [RequireComponent(typeof(SphereCollider))]
    public class Fire : MonoBehaviour
    {
        [Header("Type")]
        [SerializeField] private FireType _fireType = FireType.Solid;

        [SerializeField] private SphereCollider _collider;
        [FormerlySerializedAs("_hp")]
        [SerializeField, Min(0f)] private float _maxIntensity = 100f;
        [FormerlySerializedAs("_currentHP")]
        [SerializeField, Min(0f)] private float _currentIntensity = 100f;

        [Header("Extinguishing")]
        [SerializeField, Min(0f)] private float _deactivationDelay = 0.5f;

        [Header("Recovery")]
        [SerializeField, Min(0f)] private float _recoveryDelay = 1f;
        [SerializeField, Min(0f)] private float _intensityRecoveryPerSecond = 10f;

        private float _remainingDeactivationDelay;
        private float _remainingRecoveryDelay;

        public event Action<float> OnIntensityChanged;

        public FireType FireType => _fireType;
        public float MaxIntensity => _maxIntensity;
        public float CurrentIntensity => _currentIntensity;
        public float IntensityRatio => _maxIntensity > 0f ? _currentIntensity / _maxIntensity : 0f;
        public bool IsExtinguished => _currentIntensity <= 0f;

        private void Reset()
        {
            _collider = GetComponent<SphereCollider>();
        }

        private void OnEnable()
        {
            _currentIntensity = _maxIntensity;
            _remainingDeactivationDelay = _deactivationDelay;
            _remainingRecoveryDelay = _recoveryDelay;
        }

        private void Update()
        {
            if (!IsExtinguished)
            {
                RecoverIntensity();
                return;
            }

            _remainingDeactivationDelay -= Time.deltaTime;
            if (_remainingDeactivationDelay > 0f) return;
            gameObject.SetActive(false);
        }

        public void ReduceIntensity(float amount)
        {
            if (amount <= 0f || IsExtinguished) return;

            _remainingRecoveryDelay = _recoveryDelay;
            _currentIntensity = Mathf.Max(0f, _currentIntensity - amount);
            OnIntensityChanged?.Invoke(_currentIntensity);
        }

        private void RecoverIntensity()
        {
            if (_currentIntensity >= _maxIntensity || _intensityRecoveryPerSecond <= 0f) return;

            _remainingRecoveryDelay -= Time.deltaTime;
            if (_remainingRecoveryDelay > 0f) return;

            _currentIntensity = Mathf.Min(_maxIntensity, _currentIntensity + _intensityRecoveryPerSecond * Time.deltaTime);
            OnIntensityChanged?.Invoke(_currentIntensity);
        }

    }
}
