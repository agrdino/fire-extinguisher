using System;
using _Scripts.Controller;
using _Scripts.Fires;
using _Scripts.UI;
using UnityEngine;
using UnityEngine.Localization;

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
        [SerializeField] private LocalizedString _switchOffPowerMessage = new("UI", "factory.switch_off_power");
        [SerializeField] private LocalizedString _activateFireAlarmMessage = new("UI", "factory.activate_fire_alarm");

        private ApplicationManager _applicationManager;
        private FactoryEmergencyInteractable[] _interactables;
        private FactoryEmergencyInteractable _activeCircuitBreaker;

        public FactoryEmergencyResponseStep CurrentStep { get; private set; }
        public Transform CurrentHintTarget { get; private set; }

        public event Action<FactoryEmergencyResponseStep> OnStepChanged;

        private void Awake()
        {
            _interactables = GetComponentsInChildren<FactoryEmergencyInteractable>(true);
        }

        private void Start()
        {
            _applicationManager = ApplicationManager.Instance;
            if (_applicationManager == null)
            {
                Debug.LogError("FactoryEmergencyResponseController requires an ApplicationManager.", this);
                return;
            }

            _applicationManager.OnStateChanged += HandleApplicationStateChanged;
            HandleApplicationStateChanged(_applicationManager.State);
        }

        private void OnDestroy()
        {
            if (_applicationManager != null) _applicationManager.OnStateChanged -= HandleApplicationStateChanged;
            IdleHintController.Instance?.HidePersistentMessage(this);
        }

        public void HandleInteraction(FactoryEmergencyInteractable interactable)
        {
            if (interactable == null || !interactable.IsInteractionEnabled) return;

            if (CurrentStep == FactoryEmergencyResponseStep.SwitchOffPower && interactable == _activeCircuitBreaker)
            {
                FireController.Instance?.StopElectricalSparks();
                BeginFireAlarmStep();
                return;
            }

            if (CurrentStep == FactoryEmergencyResponseStep.ActivateFireAlarm && interactable.Kind == FactoryEmergencyInteractableKind.FireAlarm)
                CompleteResponse();
        }

        private void HandleApplicationStateChanged(ApplicationState state)
        {
            if (state == ApplicationState.FactoryResponse)
                BeginCircuitBreakerStep();
            else
                ResetResponse();
        }

        private void BeginCircuitBreakerStep()
        {
            FireSpawnPoint selectedSpawnPoint = FireController.Instance?.SelectedSpawnPoint;
            _activeCircuitBreaker = null;
            for (int index = 0; index < _interactables.Length; index++)
            {
                FactoryEmergencyInteractable interactable = _interactables[index];
                bool isSelectedCircuitBreaker = interactable.Kind == FactoryEmergencyInteractableKind.CircuitBreaker && interactable.FireSpawnPoint == selectedSpawnPoint;
                interactable.SetInteractionEnabled(isSelectedCircuitBreaker);
                if (isSelectedCircuitBreaker) _activeCircuitBreaker = interactable;
            }

            if (_activeCircuitBreaker == null)
            {
                Debug.LogError($"No circuit breaker is mapped to fire point {selectedSpawnPoint?.name ?? "<null>"}.", this);
                return;
            }

            SetStep(FactoryEmergencyResponseStep.SwitchOffPower, _activeCircuitBreaker.HintTarget);
            IdleHintController.Instance?.ShowPersistentMessage(this, _switchOffPowerMessage, CurrentHintTarget);
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
                Debug.LogError("FactoryEmergencyResponseController requires at least one fire alarm.", this);
                return;
            }

            SetStep(FactoryEmergencyResponseStep.ActivateFireAlarm, hintAlarm.HintTarget);
            IdleHintController.Instance?.ShowPersistentMessage(this, _activateFireAlarmMessage, CurrentHintTarget);
        }

        private void CompleteResponse()
        {
            for (int index = 0; index < _interactables.Length; index++)
                _interactables[index].SetInteractionEnabled(false);
            IdleHintController.Instance?.HidePersistentMessage(this);
            SetStep(FactoryEmergencyResponseStep.Completed, null);
            _applicationManager.CompleteFactoryResponse();
        }

        private void ResetResponse()
        {
            if (_interactables != null)
            {
                for (int index = 0; index < _interactables.Length; index++)
                    _interactables[index].SetInteractionEnabled(false);
            }

            _activeCircuitBreaker = null;
            IdleHintController.Instance?.HidePersistentMessage(this);
            SetStep(FactoryEmergencyResponseStep.None, null);
        }

        private void SetStep(FactoryEmergencyResponseStep step, Transform hintTarget)
        {
            CurrentStep = step;
            CurrentHintTarget = hintTarget;
            OnStepChanged?.Invoke(step);
        }
    }
}
