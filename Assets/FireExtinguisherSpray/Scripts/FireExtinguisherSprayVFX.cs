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

        private float _currentBlend;
        private float _targetBlend;

        private void Reset()
        {
            _fireExtinguisher = GetComponentInParent<FireExtinguisher>();
            _sprayEffect = GetComponent<ParticleSystemLerpGroup>();
        }

        private void OnEnable()
        {
            _fireExtinguisher.OnStateChanged += FireExtinguisher_OnStateChanged;
            FireExtinguisher_OnStateChanged(_fireExtinguisher.CurrentState);

            _sprayEffect.SetBlend(_currentBlend);
        }

        private void OnDisable()
        {
            _fireExtinguisher.OnStateChanged -= FireExtinguisher_OnStateChanged;
        }

        private void Update()
        {
            if (Mathf.Approximately(_currentBlend, _targetBlend)) return;
            if (_blendDuration <= 0f) _currentBlend = _targetBlend;
            else _currentBlend = Mathf.MoveTowards(_currentBlend, _targetBlend, Time.deltaTime / _blendDuration);

            _sprayEffect.SetBlend(_currentBlend);
        }

        private void FireExtinguisher_OnStateChanged(FireExtinguisherState state)
        {
            _targetBlend = state.CanSpray ? 1f : 0f;
            if (_blendDuration > 0f) return;

            _currentBlend = _targetBlend; 
            _sprayEffect.SetBlend(_currentBlend);
        }

    }
}
