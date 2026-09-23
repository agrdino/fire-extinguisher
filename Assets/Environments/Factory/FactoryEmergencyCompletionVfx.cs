using System.Collections.Generic;
using UnityEngine;

namespace _Scripts.Environments.Factory
{
    [DisallowMultipleComponent]
    public sealed class FactoryEmergencyCompletionVfx : MonoBehaviour
    {
        [SerializeField] private FactoryEmergencyResponseController _responseController;
        [SerializeField] private GameObject _checkVfxPrefab;

        [Header("Circuit Breaker Placement")]
        [SerializeField] private Vector3 _circuitBreakerLocalPosition = new(0f, 0.45f, 0.25f);
        [SerializeField] private Vector3 _circuitBreakerLocalEulerAngles = Vector3.zero;

        [Header("Fire Alarm Placement")]
        [SerializeField] private Vector3 _fireAlarmLocalPosition = new(0f, 0.8f, 0.75f);
        [SerializeField] private Vector3 _fireAlarmLocalEulerAngles = Vector3.zero;

        private readonly Dictionary<FactoryEmergencyInteractable, GameObject> _instances = new();

        private void OnEnable()
        {
            if (_responseController == null)
            {
                return;
            }

            if (_checkVfxPrefab == null)
            {
                return;
            }

            _responseController.OnInteractionCompleted += HandleInteractionCompleted;
            _responseController.OnStepChanged += HandleStepChanged;
        }

        private void OnDisable()
        {
            if (_responseController != null)
            {
                _responseController.OnInteractionCompleted -= HandleInteractionCompleted;
                _responseController.OnStepChanged -= HandleStepChanged;
            }

            HideAll();
        }

        private void HandleInteractionCompleted(FactoryEmergencyInteractable interactable)
        {
            if (interactable == null || _checkVfxPrefab == null) return;

            Vector3 localPosition;
            Vector3 localEulerAngles;
            if (interactable.Kind == FactoryEmergencyInteractableKind.CircuitBreaker)
            {
                localPosition = _circuitBreakerLocalPosition;
                localEulerAngles = _circuitBreakerLocalEulerAngles;
            }
            else
            {
                localPosition = _fireAlarmLocalPosition;
                localEulerAngles = _fireAlarmLocalEulerAngles;
            }

            Transform target = interactable.transform;
            Vector3 position = target.TransformPoint(localPosition);
            Quaternion rotation = target.rotation * Quaternion.Euler(localEulerAngles);

            if (!_instances.TryGetValue(interactable, out GameObject instance) || instance == null)
            {
                instance = Instantiate(_checkVfxPrefab, position, rotation);
                instance.transform.SetParent(transform, true);
                instance.name = $"Check VFX - {interactable.name}";
                _instances[interactable] = instance;
            }
            else
            {
                instance.transform.SetPositionAndRotation(position, rotation);
                instance.SetActive(true);
            }

            RestartParticles(instance);
        }

        private void HandleStepChanged(FactoryEmergencyResponseStep step)
        {
            if (step == FactoryEmergencyResponseStep.None
                || step == FactoryEmergencyResponseStep.SwitchOffPower)
            {
                HideAll();
            }
        }

        private void HideAll()
        {
            foreach (GameObject instance in _instances.Values)
            {
                if (instance != null) instance.SetActive(false);
            }
        }

        private static void RestartParticles(GameObject instance)
        {
            ParticleSystem[] particleSystems = instance.GetComponentsInChildren<ParticleSystem>(true);
            for (int index = 0; index < particleSystems.Length; index++)
            {
                particleSystems[index].Clear(true);
                particleSystems[index].Play(true);
            }
        }
    }
}
