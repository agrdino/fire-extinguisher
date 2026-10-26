using System;
using UnityEngine;

namespace _Scripts.Environments.Factory
{
    [DisallowMultipleComponent]
    public sealed class FactoryPowerLightingController : MonoBehaviour
    {
        [SerializeField, Range(0f, 1f)] private float _powerOffBrightness = 0.1f;
        [SerializeField, Range(0f, 1f)] private float _powerOffAmbientBrightness = 0.02f;
        [SerializeField] private Light[] _environmentLights = Array.Empty<Light>();
        [SerializeField] private FactoryEmergencyLightFixture[] _emergencyFixtures =
            Array.Empty<FactoryEmergencyLightFixture>();

        [Header("Ceiling Lamp Meshes")]
        [Tooltip("The emissive lamp meshes shown while factory power is available.")]
        [SerializeField] private GameObject[] _poweredLampMeshes = Array.Empty<GameObject>();
        [Tooltip("Cloned lamp meshes using the non-emissive power-off material.")]
        [SerializeField] private GameObject _powerOffLampMeshesRoot;

        private float[] _initialLightIntensities = Array.Empty<float>();
        private bool[] _initialPoweredLampActiveStates = Array.Empty<bool>();
        private bool _initialPowerOffLampMeshesRootActive;
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

            _initialPoweredLampActiveStates = new bool[_poweredLampMeshes.Length];
            for (int index = 0; index < _poweredLampMeshes.Length; index++)
            {
                if (_poweredLampMeshes[index] != null)
                    _initialPoweredLampActiveStates[index] = _poweredLampMeshes[index].activeSelf;
            }
            if (_powerOffLampMeshesRoot != null)
                _initialPowerOffLampMeshesRootActive = _powerOffLampMeshesRoot.activeSelf;

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

            RenderSettings.ambientIntensity = _initialAmbientIntensity
                * (isPowerOff ? _powerOffAmbientBrightness : 1f);
            ApplyLampMeshState(isPowerOff);

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
            RestoreLampMeshState();

            for (int index = 0; index < _emergencyFixtures.Length; index++)
            {
                if (_emergencyFixtures[index] != null)
                    _emergencyFixtures[index].SetEmergencyActive(false);
            }

            IsPowerOff = false;
        }

        private void ApplyLampMeshState(bool isPowerOff)
        {
            for (int index = 0; index < _poweredLampMeshes.Length; index++)
            {
                GameObject poweredLampMesh = _poweredLampMeshes[index];
                if (poweredLampMesh != null)
                    poweredLampMesh.SetActive(!isPowerOff && _initialPoweredLampActiveStates[index]);
            }

            if (_powerOffLampMeshesRoot != null)
                _powerOffLampMeshesRoot.SetActive(isPowerOff);
        }

        private void RestoreLampMeshState()
        {
            for (int index = 0; index < _poweredLampMeshes.Length; index++)
            {
                GameObject poweredLampMesh = _poweredLampMeshes[index];
                if (poweredLampMesh != null)
                    poweredLampMesh.SetActive(_initialPoweredLampActiveStates[index]);
            }

            if (_powerOffLampMeshesRoot != null)
                _powerOffLampMeshesRoot.SetActive(_initialPowerOffLampMeshesRootActive);
        }
    }
}
