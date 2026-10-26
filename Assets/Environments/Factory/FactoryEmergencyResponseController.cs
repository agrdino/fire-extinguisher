using System;
using _Scripts.Controller;
using _Scripts.Fires;
using _Scripts.UI;
using UnityEngine;

namespace _Scripts.Environments.Factory
{
    public enum FactoryEmergencyResponseStep
    {
        None,
        SwitchOffPower,
        ActivateFireAlarm,
        Completed
    }

    [DisallowMultipleComponent]
    public sealed class FactoryEmergencyResponseController : MonoBehaviour
    {
        private ApplicationManager _applicationManager;
        [SerializeField] private FactoryEmergencyInteractable[] _interactables = Array.Empty<FactoryEmergencyInteractable>();
        [SerializeField] private Transform _circuitBreakerUIAnchor;
        [SerializeField] private FactoryPowerLightingController _powerLightingController;
        private FactoryEmergencyInteractable _activeCircuitBreaker;

        public FactoryEmergencyResponseStep CurrentStep { get; private set; }
        public Transform CurrentHintTarget { get; private set; }
        public Transform CurrentUIAnchor { get; private set; }

        public event Action<FactoryEmergencyResponseStep> OnStepChanged;
        public event Action<FactoryEmergencyInteractable> OnInteractionCompleted;

        private void Start()
        {
            _applicationManager = ApplicationManager.Instance;
            if (_applicationManager == null)
            {
                return;
            }

            _applicationManager.OnStateChanged += HandleApplicationStateChanged;
            HandleApplicationStateChanged(_applicationManager.State);
        }

        private void OnDestroy()
        {
            if (_applicationManager != null) _applicationManager.OnStateChanged -= HandleApplicationStateChanged;
        }

        public void HandleInteraction(FactoryEmergencyInteractable interactable)
        {
            if (interactable == null || !interactable.IsInteractionEnabled) return;

            if (CurrentStep == FactoryEmergencyResponseStep.SwitchOffPower && interactable == _activeCircuitBreaker)
            {
                FireController.Instance?.StopElectricalSparks();
                _powerLightingController?.SetPowerOff(true);
                OnInteractionCompleted?.Invoke(interactable);
                BeginFireAlarmStep();
                return;
            }

            if (CurrentStep == FactoryEmergencyResponseStep.ActivateFireAlarm && interactable.Kind == FactoryEmergencyInteractableKind.FireAlarm)
            {
                OnInteractionCompleted?.Invoke(interactable);
                CompleteResponse();
            }
        }

        private void HandleApplicationStateChanged(ApplicationState state)
        {
            if (state == ApplicationState.Ready)
            {
                ResetResponse();
                _powerLightingController?.SetPowerOff(false);
                return;
            }

            if (state == ApplicationState.FactoryResponse)
            {
                ResetResponse();
                BeginCircuitBreakerStep();
                return;
            }

            if (state == ApplicationState.Completed || state == ApplicationState.Escaped || state == ApplicationState.Failed)
            {
                ResetResponse();
                return;
            }

            DisableInteractions();
        }

        private void BeginCircuitBreakerStep()
        {
            _activeCircuitBreaker = null;
            for (int index = 0; index < _interactables.Length; index++)
            {
                FactoryEmergencyInteractable interactable = _interactables[index];
                if (interactable == null) continue;
                bool isCircuitBreaker = interactable.Kind == FactoryEmergencyInteractableKind.CircuitBreaker;
                bool isSharedCircuitBreaker = isCircuitBreaker && _activeCircuitBreaker == null;
                interactable.SetInteractionEnabled(isSharedCircuitBreaker);
                if (isSharedCircuitBreaker) _activeCircuitBreaker = interactable;
            }

            if (_activeCircuitBreaker == null)
            {
                return;
            }

            SetStep(
                FactoryEmergencyResponseStep.SwitchOffPower,
                _activeCircuitBreaker.HintTarget,
                _circuitBreakerUIAnchor);
        }

        private void BeginFireAlarmStep()
        {
            FireSpawnPoint selectedSpawnPoint = FireController.Instance?.SelectedSpawnPoint;
            Transform playerView = _applicationManager?.PlayerView;
            FactoryEmergencyInteractable nearestAlarm = null;
            float nearestDistance = float.PositiveInfinity;

            for (int index = 0; index < _interactables.Length; index++)
            {
                FactoryEmergencyInteractable interactable = _interactables[index];
                if (interactable == null || !interactable.gameObject.activeInHierarchy) continue;

                bool isAlarm = interactable.Kind == FactoryEmergencyInteractableKind.FireAlarm;
                interactable.SetInteractionEnabled(isAlarm);
                if (!isAlarm) continue;

                if (nearestAlarm == null)
                    nearestAlarm = interactable;

                float distance = CombinedHorizontalDistance(
                    interactable.HintTarget.position,
                    selectedSpawnPoint != null ? selectedSpawnPoint.transform : null,
                    playerView);
                if (distance >= nearestDistance) continue;

                nearestDistance = distance;
                nearestAlarm = interactable;
            }

            if (nearestAlarm == null)
            {
                return;
            }

            Transform fireAlarmUIAnchor = FireController.Instance?.SelectedSpawnPoint?.FireAlarmUIPoint;
            SetStep(
                FactoryEmergencyResponseStep.ActivateFireAlarm,
                nearestAlarm.HintTarget,
                fireAlarmUIAnchor);
        }

        private void CompleteResponse()
        {
            for (int index = 0; index < _interactables.Length; index++)
                _interactables[index]?.SetInteractionEnabled(false);
            SetStep(FactoryEmergencyResponseStep.Completed, null, null);
            _applicationManager.CompleteFactoryResponse();
        }

        private void DisableInteractions()
        {
            if (_interactables != null)
            {
                for (int index = 0; index < _interactables.Length; index++)
                    _interactables[index]?.SetInteractionEnabled(false);
            }

            _activeCircuitBreaker = null;
        }

        private void ResetResponse()
        {
            if (_interactables != null)
            {
                for (int index = 0; index < _interactables.Length; index++)
                {
                    FactoryEmergencyInteractable interactable = _interactables[index];
                    if (interactable == null) continue;
                    interactable.ResetInteractionState();
                    interactable.SetInteractionEnabled(false);
                }
            }

            _activeCircuitBreaker = null;
            SetStep(FactoryEmergencyResponseStep.None, null, null);
        }

        private static float CombinedHorizontalDistance(
            Vector3 candidatePosition,
            Transform firstReference,
            Transform secondReference)
        {
            float distance = 0f;
            int referenceCount = 0;

            if (firstReference != null)
            {
                distance += HorizontalDistance(candidatePosition, firstReference.position);
                referenceCount++;
            }

            if (secondReference != null)
            {
                distance += HorizontalDistance(candidatePosition, secondReference.position);
                referenceCount++;
            }

            return referenceCount > 0 ? distance : float.PositiveInfinity;
        }

        private static float HorizontalDistance(Vector3 from, Vector3 to)
        {
            float x = from.x - to.x;
            float z = from.z - to.z;
            return Mathf.Sqrt(x * x + z * z);
        }

        private void SetStep(
            FactoryEmergencyResponseStep step,
            Transform hintTarget,
            Transform uiAnchor)
        {
            CurrentStep = step;
            CurrentHintTarget = hintTarget;
            CurrentUIAnchor = uiAnchor;
            OnStepChanged?.Invoke(step);
            IdleHintController.Instance?.NotifyActivity();
        }
    }
}
