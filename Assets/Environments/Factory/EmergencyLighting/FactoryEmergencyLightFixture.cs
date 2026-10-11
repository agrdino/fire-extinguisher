using System;
using UnityEngine;

namespace _Scripts.Environments.Factory
{
    [DisallowMultipleComponent]
    public sealed class FactoryEmergencyLightFixture : MonoBehaviour
    {
        [SerializeField] private Light[] _lights = Array.Empty<Light>();

        public bool IsEmergencyActive { get; private set; }

        private void Awake()
        {
            CacheLightsIfNeeded();
            SetEmergencyActive(false);
        }

        public void SetEmergencyActive(bool isActive)
        {
            CacheLightsIfNeeded();
            IsEmergencyActive = isActive;

            for (int index = 0; index < _lights.Length; index++)
            {
                if (_lights[index] != null)
                    _lights[index].enabled = isActive;
            }
        }

        private void CacheLightsIfNeeded()
        {
            if (_lights == null || _lights.Length == 0)
                _lights = GetComponentsInChildren<Light>(true);
        }

#if UNITY_EDITOR
        private void Reset()
        {
            _lights = GetComponentsInChildren<Light>(true);
        }
#endif
    }
}
