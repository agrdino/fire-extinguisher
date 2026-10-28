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
                    _lights[index].enabled = isActive && CanEnableAtRuntime(_lights[index]);
            }
        }

        private static bool CanEnableAtRuntime(Light light)
        {
#if UNITY_EDITOR
            return light.lightmapBakeType != LightmapBakeType.Baked;
#else
            // Factory emergency lights are baked into the dark lightmaps. Player builds
            // must keep their Light components disabled; lightmapBakeType is Editor-only.
            return false;
#endif
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
