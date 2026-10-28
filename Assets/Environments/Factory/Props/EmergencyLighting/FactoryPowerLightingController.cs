using System;
using _Scripts.Environments.EmergencyResponse;
using UnityEngine;

namespace _Scripts.Environments.Factory
{
    [DisallowMultipleComponent]
    public sealed class FactoryPowerLightingController : MonoBehaviour, IEmergencyPowerController
    {
        [SerializeField, Range(0f, 1f)] private float _powerOffBrightness = 0.1f;
        [SerializeField, Range(0f, 1f)] private float _powerOffAmbientBrightness = 0.02f;
        [SerializeField] private Light[] _environmentLights = Array.Empty<Light>();
        [SerializeField] private FactoryEmergencyLightFixture[] _emergencyFixtures =
            Array.Empty<FactoryEmergencyLightFixture>();

        [Header("Ceiling Lights")]
        [SerializeField] private CeilLight[] _ceilingLights = Array.Empty<CeilLight>();

        [Header("Baked Lighting Scenarios")]
        [SerializeField] private FactoryBakedLightingSwitcher _bakedLighting;

        private float[] _initialLightIntensities = Array.Empty<float>();
        private bool[] _initialCeilingLightPowerStates = Array.Empty<bool>();
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

            _initialCeilingLightPowerStates = new bool[_ceilingLights.Length];
            for (int index = 0; index < _ceilingLights.Length; index++)
            {
                if (_ceilingLights[index] != null)
                    _initialCeilingLightPowerStates[index] = _ceilingLights[index].IsPowered;
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

            if (_bakedLighting != null)
                _bakedLighting.SetBlackout(isPowerOff);
            else
                RenderSettings.ambientIntensity = _initialAmbientIntensity
                    * (isPowerOff ? _powerOffAmbientBrightness : 1f);
            ApplyCeilingLightState(isPowerOff);

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

            if (_bakedLighting != null)
                _bakedLighting.SetBlackout(false);
            else
                RenderSettings.ambientIntensity = _initialAmbientIntensity;
            RestoreCeilingLightState();

            for (int index = 0; index < _emergencyFixtures.Length; index++)
            {
                if (_emergencyFixtures[index] != null)
                    _emergencyFixtures[index].SetEmergencyActive(false);
            }

            IsPowerOff = false;
        }

        private void ApplyCeilingLightState(bool isPowerOff)
        {
            for (int index = 0; index < _ceilingLights.Length; index++)
            {
                CeilLight ceilingLight = _ceilingLights[index];
                if (ceilingLight != null)
                    ceilingLight.SetPowered(!isPowerOff);
            }
        }

        private void RestoreCeilingLightState()
        {
            for (int index = 0; index < _ceilingLights.Length; index++)
            {
                CeilLight ceilingLight = _ceilingLights[index];
                if (ceilingLight != null)
                    ceilingLight.SetPowered(_initialCeilingLightPowerStates[index]);
            }
        }
    }
}
