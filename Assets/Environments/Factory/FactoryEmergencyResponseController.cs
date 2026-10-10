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
            FactoryEmergencyInteractable primaryAlarm = null;
            FactoryEmergencyInteractable fallbackAlarm = null;
            for (int index = 0; index < _interactables.Length; index++)
            {
                FactoryEmergencyInteractable interactable = _interactables[index];
                bool isAlarm = interactable.Kind == FactoryEmergencyInteractableKind.FireAlarm;
                interactable.SetInteractionEnabled(isAlarm);
                if (!isAlarm) continue;
                fallbackAlarm ??= interactable;
                if (interactable.IsPrimaryHintTarget) primaryAlarm = interactable;
            }

            FactoryEmergencyInteractable hintAlarm = primaryAlarm != null ? primaryAlarm : fallbackAlarm;
            if (hintAlarm == null)
            {
                return;
            }

            Transform fireAlarmUIAnchor = FireController.Instance?.SelectedSpawnPoint?.FireAlarmUIPoint;
            SetStep(
                FactoryEmergencyResponseStep.ActivateFireAlarm,
                hintAlarm.HintTarget,
                fireAlarmUIAnchor);
        }

        private void CompleteResponse()
        {
            for (int index = 0; index < _interactables.Length; index++)
                _interactables[index].SetInteractionEnabled(false);
            SetStep(FactoryEmergencyResponseStep.Completed, null, null);
            _applicationManager.CompleteFactoryResponse();
        }

        private void DisableInteractions()
        {
            if (_interactables != null)
            {
                for (int index = 0; index < _interactables.Length; index++)
                    _interactables[index].SetInteractionEnabled(false);
            }

            _activeCircuitBreaker = null;
        }

        private void ResetResponse()
        {
            if (_interactables != null)
            {
                for (int index = 0; index < _interactables.Length; index++)
                {
                    _interactables[index].ResetInteractionState();
                    _interactables[index].SetInteractionEnabled(false);
                }
            }

            _activeCircuitBreaker = null;
            SetStep(FactoryEmergencyResponseStep.None, null, null);
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
