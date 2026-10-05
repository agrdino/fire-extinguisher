using System;
using UnityEngine;
using UnityEngine.Serialization;

namespace _Scripts.Fires
{
    [RequireComponent(typeof(SphereCollider))]
    public class Fire : MonoBehaviour
    {
        [Header("Type")]
        [SerializeField] private FireType _fireType = FireType.Electrical;

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

        [Header("Incompatible Extinguisher")]
        [FormerlySerializedAs("_flareUpIntensityRatio")]
        [SerializeField, Min(1f)] private float _dangerIntensityRatio = 2.5f;

        private float _remainingDeactivationDelay;
        private float _remainingRecoveryDelay;
        private bool _hasIncompatibleExposure;
        private bool _hasReachedDangerThreshold;

        public event Action<float> OnIntensityChanged;
        public event Action OnIncompatibleExposureStarted;
        public event Action OnDangerThresholdReached;

        public FireType FireType => _fireType;
        public float MaxIntensity => _maxIntensity;
        public float CurrentIntensity => _currentIntensity;
        public float IntensityRatio => _maxIntensity > 0f ? _currentIntensity / _maxIntensity : 0f;
        public bool IsExtinguished => _currentIntensity <= 0f;
        public bool HasIncompatibleExposure => _hasIncompatibleExposure;
        // Kept as compatibility aliases for the existing audio, VFX, and proximity-warning
        // components. A wrong-extinguisher exposure is now intensity-driven rather than timed.
        public bool IsFlaringUp => _hasIncompatibleExposure && !_hasReachedDangerThreshold;
        public float DangerIntensity => _maxIntensity * Mathf.Max(1f, _dangerIntensityRatio);
        public float DangerProgress => _hasIncompatibleExposure && DangerIntensity > _maxIntensity
            ? Mathf.InverseLerp(_maxIntensity, DangerIntensity, _currentIntensity)
            : 0f;
        public float FlareUpProgress => DangerProgress;

        private void Reset()
        {
            _collider = GetComponent<SphereCollider>();
        }

        private void OnEnable()
        {
            _currentIntensity = _maxIntensity;
            _remainingDeactivationDelay = _deactivationDelay;
            _remainingRecoveryDelay = _recoveryDelay;
            _hasIncompatibleExposure = false;
            _hasReachedDangerThreshold = false;
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
            if (amount <= 0f || IsExtinguished || _hasReachedDangerThreshold) return;

            _remainingRecoveryDelay = _recoveryDelay;
            _currentIntensity = Mathf.Max(0f, _currentIntensity - amount);
            OnIntensityChanged?.Invoke(_currentIntensity);
        }

        public void IncreaseFromIncompatibleExtinguisher(float amount)
        {
            if (amount <= 0f || IsExtinguished || _hasReachedDangerThreshold) return;

            if (!_hasIncompatibleExposure)
            {
                _hasIncompatibleExposure = true;
                OnIncompatibleExposureStarted?.Invoke();
            }

            _remainingRecoveryDelay = _recoveryDelay;
            SetIntensity(Mathf.Min(DangerIntensity, _currentIntensity + amount));
        }

        private void RecoverIntensity()
        {
            float recoveryTarget = _hasIncompatibleExposure ? DangerIntensity : _maxIntensity;
            if (_currentIntensity >= recoveryTarget || _intensityRecoveryPerSecond <= 0f) return;

            _remainingRecoveryDelay -= Time.deltaTime;
            if (_remainingRecoveryDelay > 0f) return;

            SetIntensity(Mathf.Min(recoveryTarget, _currentIntensity + _intensityRecoveryPerSecond * Time.deltaTime));
        }

        private void SetIntensity(float intensity)
        {
            _currentIntensity = Mathf.Max(0f, intensity);
            OnIntensityChanged?.Invoke(_currentIntensity);
            if (_hasReachedDangerThreshold || !_hasIncompatibleExposure || _currentIntensity < DangerIntensity) return;

            _hasReachedDangerThreshold = true;
            OnDangerThresholdReached?.Invoke();
        }

    }
}
