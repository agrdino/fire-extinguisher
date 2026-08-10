using System;
using UnityEngine;
using UnityEngine.Serialization;

namespace _Scripts.Fires
{
    [RequireComponent(typeof(SphereCollider))]
    public class Fire : MonoBehaviour
    {
        [SerializeField] private SphereCollider _collider;
        [FormerlySerializedAs("_hp")]
        [SerializeField, Min(0f)] private float _maxIntensity = 100f;
        [FormerlySerializedAs("_currentHP")]
        [SerializeField, Min(0f)] private float _currentIntensity = 100f;

        [Header("Extinguishing")]
        [SerializeField, Min(0f)] private float _deactivationDelay = 0.5f;

        private float _remainingDeactivationDelay;

        public event Action<float> OnIntensityChanged;

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
        }

        private void Update()
        {
            if (!IsExtinguished) return;

            _remainingDeactivationDelay -= Time.deltaTime;
            if (_remainingDeactivationDelay > 0f) return;
            gameObject.SetActive(false);
        }

        public void ReduceIntensity(float amount)
        {
            if (amount <= 0f || IsExtinguished) return;

            _currentIntensity = Mathf.Max(0f, _currentIntensity - amount);
            OnIntensityChanged?.Invoke(_currentIntensity);
        }

    }
}
