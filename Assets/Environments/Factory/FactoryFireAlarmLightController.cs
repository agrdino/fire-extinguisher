using System;
using UnityEngine;

namespace _Scripts.Environments.Factory
{
    [DisallowMultipleComponent]
    public sealed class FactoryFireAlarmLightController : MonoBehaviour
    {
        [SerializeField] private FactoryEmergencyResponseController _responseController;
        [SerializeField] private ParticleSystem[] _alarmLights = Array.Empty<ParticleSystem>();

        [Header("Ceiling Flash")]
        [SerializeField] private Light[] _ceilingLights = Array.Empty<Light>();
        [SerializeField] private Color _ceilingLightColor = new(1f, 0.02f, 0.01f, 1f);
        [SerializeField, Min(0f)] private float _ceilingLightPeakIntensity = 20f;
        [SerializeField, Min(0f)] private float _ceilingLightFrequency = 1.5f;

        private bool _isAlarmActive;

        private void OnEnable()
        {
            SetAlarmLightsActive(false);

            if (_responseController == null) return;
            _responseController.OnInteractionCompleted += HandleInteractionCompleted;
            _responseController.OnStepChanged += HandleStepChanged;
        }

        private void OnDisable()
        {
            SetAlarmLightsActive(false);

            if (_responseController == null) return;
            _responseController.OnInteractionCompleted -= HandleInteractionCompleted;
            _responseController.OnStepChanged -= HandleStepChanged;
        }

        private void Update()
        {
            if (!_isAlarmActive) return;

            float phase = Time.unscaledTime * _ceilingLightFrequency * Mathf.PI * 2f;
            float brightness = (Mathf.Cos(phase) + 1f) * 0.5f;
            SetCeilingLightIntensity(brightness * _ceilingLightPeakIntensity);
        }

        private void HandleInteractionCompleted(FactoryEmergencyInteractable interactable)
        {
            if (interactable == null || interactable.Kind != FactoryEmergencyInteractableKind.FireAlarm)
                return;

            SetAlarmLightsActive(true);
        }

        private void HandleStepChanged(FactoryEmergencyResponseStep step)
        {
            if (step == FactoryEmergencyResponseStep.None || step == FactoryEmergencyResponseStep.SwitchOffPower)
                SetAlarmLightsActive(false);
        }

        private void SetAlarmLightsActive(bool isActive)
        {
            _isAlarmActive = isActive;

            for (int index = 0; index < _alarmLights.Length; index++)
            {
                ParticleSystem alarmLight = _alarmLights[index];
                if (alarmLight == null) continue;

                if (isActive)
                {
                    alarmLight.Clear(true);
                    alarmLight.Play(true);
                }
                else
                {
                    alarmLight.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
                }
            }

            SetCeilingLightsActive(isActive);
        }

        private void SetCeilingLightsActive(bool isActive)
        {
            _isAlarmActive = isActive;

            for (int index = 0; index < _ceilingLights.Length; index++)
            {
                Light ceilingLight = _ceilingLights[index];
                if (ceilingLight == null) continue;

                ceilingLight.color = _ceilingLightColor;
                ceilingLight.intensity = isActive ? _ceilingLightPeakIntensity : 0f;
                ceilingLight.enabled = isActive;
            }
        }

        private void SetCeilingLightIntensity(float intensity)
        {
            for (int index = 0; index < _ceilingLights.Length; index++)
            {
                Light ceilingLight = _ceilingLights[index];
                if (ceilingLight != null) ceilingLight.intensity = intensity;
            }
        }
    }
}
