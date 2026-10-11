using System;
using UnityEngine;

namespace _Scripts.Environments.Factory
{
    [DisallowMultipleComponent]
    public sealed class FactoryPowerLightingController : MonoBehaviour
    {
    [SerializeField, Range(0f, 1f)] private float _powerOffBrightness = 0.1f;
        [SerializeField] private Light[] _environmentLights = Array.Empty<Light>();
        [SerializeField] private FactoryEmergencyLightFixture[] _emergencyFixtures =
            Array.Empty<FactoryEmergencyLightFixture>();

        private float[] _initialLightIntensities = Array.Empty<float>();
        private float _initialAmbientIntensity;
        private bool _hasCapturedInitialState;

        public bool IsPowerOff { get; private set; }

        private void Awake()
        {
            CaptureInitialState();
            ApplyPowerState(false);
        }

        private void OnDisable()
        {
            RestoreInitialState();
        }

        private void OnDestroy()
        {
            RestoreInitialState();
        }

        public void SetPowerOff(bool isPowerOff)
        {
            CaptureInitialState();
            ApplyPowerState(isPowerOff);
        }

        private void CaptureInitialState()
        {
            if (_hasCapturedInitialState) return;

            _initialLightIntensities = new float[_environmentLights.Length];
            for (int index = 0; index < _environmentLights.Length; index++)
            {
                if (_environmentLights[index] != null)
                    _initialLightIntensities[index] = _environmentLights[index].intensity;
            }

            _initialAmbientIntensity = RenderSettings.ambientIntensity;
            _hasCapturedInitialState = true;
        }

        private void ApplyPowerState(bool isPowerOff)
        {
            IsPowerOff = isPowerOff;
            float brightnessMultiplier = isPowerOff ? _powerOffBrightness : 1f;

            for (int index = 0; index < _environmentLights.Length; index++)
            {
                if (_environmentLights[index] != null)
                    _environmentLights[index].intensity = _initialLightIntensities[index] * brightnessMultiplier;
            }

            RenderSettings.ambientIntensity = _initialAmbientIntensity * brightnessMultiplier;

            for (int index = 0; index < _emergencyFixtures.Length; index++)
            {
                if (_emergencyFixtures[index] != null)
                    _emergencyFixtures[index].SetEmergencyActive(isPowerOff);
            }
        }

        private void RestoreInitialState()
        {
            if (!_hasCapturedInitialState) return;

            for (int index = 0; index < _environmentLights.Length; index++)
            {
                if (_environmentLights[index] != null)
                    _environmentLights[index].intensity = _initialLightIntensities[index];
            }

            RenderSettings.ambientIntensity = _initialAmbientIntensity;

            for (int index = 0; index < _emergencyFixtures.Length; index++)
            {
                if (_emergencyFixtures[index] != null)
                    _emergencyFixtures[index].SetEmergencyActive(false);
            }

            IsPowerOff = false;
        }
    }
}
