using _Scripts.ParticleSystemLerps;
using UnityEngine;

namespace _Scripts.FireExtinguishers
{
    [DisallowMultipleComponent]
    public sealed class FireExtinguisher : MonoBehaviour
    {
        [SerializeField] private GameObject _safetyPin;
        [SerializeField] private ParticleSystemLerpGroup _sprayEffect;

        [SerializeField, Min(0f)] private float _blendDuration = 1f;
        [SerializeField] private FireExtinguisherState _state;

        private float _currentBlend = 0f;

        public FireExtinguisherState CurrentState => _state;

        private void Reset()
        {
            _sprayEffect = GetComponentInChildren<ParticleSystemLerpGroup>(true);

            Transform safetyPinTransform = transform.Find("SafetyPin");
            if (safetyPinTransform != null) _safetyPin = safetyPinTransform.gameObject;
        }

        private void Awake()
        {
            bool isPinActive = _safetyPin == null || _safetyPin.activeSelf;
            SafetyPinState initialSafetyPin = isPinActive ? SafetyPinState.Inserted : SafetyPinState.Removed;
            _state = new FireExtinguisherState(initialSafetyPin, LeverState.Released);

            ApplySafetyPinVisual();
            if (_sprayEffect != null) _sprayEffect.SetBlend(_currentBlend);
        }

        private void Update()
        {
            UpdateSprayBlend();
        }

        public void ApplyState(FireExtinguisherState state)
        {
            _state = state;
            ApplySafetyPinVisual();
        }

        public void ApplyState(SafetyPinState safetyPin, LeverState lever)
        {
            ApplyState(new FireExtinguisherState(safetyPin, lever));
        }

        private void UpdateSprayBlend()
        {
            float targetBlend = _state.CanSpray ? 1f : 0f;
            if (_blendDuration <= 0f) _currentBlend = targetBlend;
            else
            {
                float delta = Time.deltaTime / _blendDuration;
                _currentBlend = Mathf.MoveTowards(_currentBlend, targetBlend, delta);
            }

            if (_sprayEffect != null) _sprayEffect.SetBlend(_currentBlend);
        }

        private void ApplySafetyPinVisual()
        {
            if (_safetyPin == null) return;

            bool isActive = _state.SafetyPin == SafetyPinState.Inserted;
            _safetyPin.SetActive(isActive);
        }

    }
}
