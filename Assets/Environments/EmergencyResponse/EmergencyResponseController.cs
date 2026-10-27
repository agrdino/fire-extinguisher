using System;
using _Scripts.Controller;
using _Scripts.Fires;
using _Scripts.UI;
using UnityEngine;
using UnityEngine.Serialization;

namespace _Scripts.Environments.EmergencyResponse
{
    public interface IEmergencyPowerController
    {
        void SetPowerOff(bool isPowerOff);
    }

    public enum EmergencyResponseStep
    {
        None,
        SwitchOffPower,
        ActivateFireAlarm,
        Completed
    }

    [DisallowMultipleComponent]
    public sealed class EmergencyResponseController : MonoBehaviour
    {
        private ApplicationManager _applicationManager;
        [SerializeField] private EmergencyInteractable[] _interactables = Array.Empty<EmergencyInteractable>();
        [SerializeField] private Transform _circuitBreakerUIAnchor;
        [FormerlySerializedAs("_powerLightingController")]
        [SerializeField] private MonoBehaviour _powerController;
        private EmergencyInteractable _activeCircuitBreaker;

        public EmergencyResponseStep CurrentStep { get; private set; }
        public Transform CurrentHintTarget { get; private set; }
        public Transform CurrentUIAnchor { get; private set; }

        public event Action<EmergencyResponseStep> OnStepChanged;
        public event Action<EmergencyInteractable> OnInteractionCompleted;

        private IEmergencyPowerController PowerController => _powerController as IEmergencyPowerController;

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

        public void HandleInteraction(EmergencyInteractable interactable)
        {
            if (interactable == null || !interactable.IsInteractionEnabled) return;

            if (CurrentStep == EmergencyResponseStep.SwitchOffPower && interactable == _activeCircuitBreaker)
            {
                FireController.Instance?.StopElectricalSparks();
                PowerController?.SetPowerOff(true);
                OnInteractionCompleted?.Invoke(interactable);
                BeginFireAlarmStep();
                return;
            }

            if (CurrentStep == EmergencyResponseStep.ActivateFireAlarm && interactable.Kind == EmergencyInteractableKind.FireAlarm)
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
                PowerController?.SetPowerOff(false);
                return;
            }

            if (state == ApplicationState.EmergencyResponse)
            {
                ResetResponse();
                if (!BeginCircuitBreakerStep()) BeginFireAlarmStep();
                return;
            }

            if (state == ApplicationState.Completed || state == ApplicationState.Escaped || state == ApplicationState.Failed)
            {
                ResetResponse();
                return;
            }

            DisableInteractions();
        }

        private bool BeginCircuitBreakerStep()
        {
            _activeCircuitBreaker = null;
            for (int index = 0; index < _interactables.Length; index++)
            {
                EmergencyInteractable interactable = _interactables[index];
                if (interactable == null) continue;
                bool isCircuitBreaker = interactable.Kind == EmergencyInteractableKind.CircuitBreaker;
                bool isSharedCircuitBreaker = isCircuitBreaker && _activeCircuitBreaker == null;
                interactable.SetInteractionEnabled(isSharedCircuitBreaker);
                if (isSharedCircuitBreaker) _activeCircuitBreaker = interactable;
            }

            if (_activeCircuitBreaker == null)
            {
                return false;
            }

            SetStep(
                EmergencyResponseStep.SwitchOffPower,
                _activeCircuitBreaker.HintTarget,
                _circuitBreakerUIAnchor != null
                    ? _circuitBreakerUIAnchor
                    : _activeCircuitBreaker.UIAnchor);
            return true;
        }

        private void BeginFireAlarmStep()
        {
            FireSpawnPoint selectedSpawnPoint = FireController.Instance?.SelectedSpawnPoint;
            Transform playerView = _applicationManager?.PlayerView;
            EmergencyInteractable nearestAlarm = null;
            float nearestDistance = float.PositiveInfinity;

            for (int index = 0; index < _interactables.Length; index++)
            {
                EmergencyInteractable interactable = _interactables[index];
                if (interactable == null || !interactable.gameObject.activeInHierarchy) continue;

                bool isAlarm = interactable.Kind == EmergencyInteractableKind.FireAlarm;
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
                EmergencyResponseStep.ActivateFireAlarm,
                nearestAlarm.HintTarget,
                fireAlarmUIAnchor != null ? fireAlarmUIAnchor : nearestAlarm.UIAnchor);
        }

        private void CompleteResponse()
        {
            for (int index = 0; index < _interactables.Length; index++)
                _interactables[index]?.SetInteractionEnabled(false);
            SetStep(EmergencyResponseStep.Completed, null, null);
            _applicationManager.CompleteEmergencyResponse();
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
                    EmergencyInteractable interactable = _interactables[index];
                    if (interactable == null) continue;
                    interactable.ResetInteractionState();
                    interactable.SetInteractionEnabled(false);
                }
            }

            _activeCircuitBreaker = null;
            SetStep(EmergencyResponseStep.None, null, null);
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
            EmergencyResponseStep step,
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
