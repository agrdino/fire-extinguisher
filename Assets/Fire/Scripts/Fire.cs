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

        [Header("Incompatible Extinguisher")]
        [FormerlySerializedAs("_flareUpDuration")]
        [SerializeField, Min(0f)] private float _flareUpGrowthDuration = 3f;
        [SerializeField, Min(0f)] private float _wrongExtinguisherEscapeDelay = 6f;
        [SerializeField, Min(1f)] private float _flareUpIntensityRatio = 2.5f;

        private float _remainingDeactivationDelay;
        private float _remainingRecoveryDelay;
        private float _flareUpElapsedTime;
        private float _flareUpStartIntensity;
        private bool _isFlaringUp;

        public event Action<float> OnIntensityChanged;
        public event Action OnFlareUpStarted;
        public event Action OnFlareUpCompleted;

        public FireType FireType => _fireType;
        public float MaxIntensity => _maxIntensity;
        public float CurrentIntensity => _currentIntensity;
        public float IntensityRatio => _maxIntensity > 0f ? _currentIntensity / _maxIntensity : 0f;
        public bool IsExtinguished => _currentIntensity <= 0f;
        public bool IsFlaringUp => _isFlaringUp;
        public float FlareUpProgress => _isFlaringUp
            ? (_flareUpGrowthDuration > 0f ? Mathf.Clamp01(_flareUpElapsedTime / _flareUpGrowthDuration) : 1f)
            : (_currentIntensity > _maxIntensity ? 1f : 0f);

        private void Reset()
        {
            _collider = GetComponent<SphereCollider>();
        }

        private void OnEnable()
        {
            _currentIntensity = _maxIntensity;
            _remainingDeactivationDelay = _deactivationDelay;
            _remainingRecoveryDelay = _recoveryDelay;
            _flareUpElapsedTime = 0f;
            _flareUpStartIntensity = _currentIntensity;
            _isFlaringUp = false;
        }

        private void Update()
        {
            if (_isFlaringUp)
            {
                UpdateFlareUp();
                return;
            }

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
            if (amount <= 0f || IsExtinguished || _isFlaringUp) return;

            _remainingRecoveryDelay = _recoveryDelay;
            _currentIntensity = Mathf.Max(0f, _currentIntensity - amount);
            OnIntensityChanged?.Invoke(_currentIntensity);
        }

        public void BeginFlareUp()
        {
            if (_isFlaringUp || IsExtinguished) return;

            _isFlaringUp = true;
            _flareUpElapsedTime = 0f;
            _flareUpStartIntensity = _currentIntensity;
            OnFlareUpStarted?.Invoke();

            if (_wrongExtinguisherEscapeDelay <= 0f) CompleteFlareUp();
        }

        private void UpdateFlareUp()
        {
            _flareUpElapsedTime += Time.deltaTime;
            float progress = _flareUpGrowthDuration > 0f
                ? Mathf.Clamp01(_flareUpElapsedTime / _flareUpGrowthDuration)
                : 1f;
            float targetIntensity = _maxIntensity * Mathf.Max(1f, _flareUpIntensityRatio);
            _currentIntensity = Mathf.Lerp(_flareUpStartIntensity, targetIntensity, progress);
            OnIntensityChanged?.Invoke(_currentIntensity);

            float escapeDelay = Mathf.Max(_flareUpGrowthDuration, _wrongExtinguisherEscapeDelay);
            if (_flareUpElapsedTime >= escapeDelay) CompleteFlareUp();
        }

        private void CompleteFlareUp()
        {
            _currentIntensity = _maxIntensity * Mathf.Max(1f, _flareUpIntensityRatio);
            _isFlaringUp = false;
            OnIntensityChanged?.Invoke(_currentIntensity);
            OnFlareUpCompleted?.Invoke();
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
