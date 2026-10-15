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
        private FactoryEmergencyInteractable _mainFireAlarm;

        public FactoryEmergencyResponseStep CurrentStep { get; private set; }
        public Transform CurrentHintTarget { get; private set; }
        public Transform CurrentUIAnchor { get; private set; }

        public event Action<FactoryEmergencyResponseStep> OnStepChanged;
        public event Action<FactoryEmergencyInteractable> OnInteractionCompleted;

        public Transform GetPreferredFireAlarmHintTarget(
            Vector3 playerPosition,
            float mainTargetDistanceTolerance)
        {
            FactoryEmergencyInteractable nearestAlarm = null;
            float nearestDistance = float.PositiveInfinity;
            for (int index = 0; index < _interactables.Length; index++)
            {
                FactoryEmergencyInteractable interactable = _interactables[index];
                if (interactable == null
                    || interactable.Kind != FactoryEmergencyInteractableKind.FireAlarm
                    || !interactable.IsInteractionEnabled
                    || !interactable.gameObject.activeInHierarchy)
                    continue;

                float distance = HorizontalDistance(playerPosition, interactable.HintTarget.position);
                if (distance >= nearestDistance) continue;
                nearestDistance = distance;
                nearestAlarm = interactable;
            }

            if (_mainFireAlarm == null) return nearestAlarm != null ? nearestAlarm.HintTarget : null;
            if (nearestAlarm == null) return _mainFireAlarm.HintTarget;

            float mainDistance = HorizontalDistance(playerPosition, _mainFireAlarm.HintTarget.position);
            return mainDistance <= nearestDistance + Mathf.Max(0f, mainTargetDistanceTolerance)
                ? _mainFireAlarm.HintTarget
                : nearestAlarm.HintTarget;
        }

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
            _powerLightingController?.SetPowerOff(false);
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
            _mainFireAlarm = null;
            FactoryEmergencyInteractable fallbackAlarm = null;
            FireSpawnPoint selectedSpawnPoint = FireController.Instance?.SelectedSpawnPoint;
            for (int index = 0; index < _interactables.Length; index++)
            {
                FactoryEmergencyInteractable interactable = _interactables[index];
                bool isAlarm = interactable.Kind == FactoryEmergencyInteractableKind.FireAlarm;
                interactable.SetInteractionEnabled(isAlarm);
                if (!isAlarm) continue;
                fallbackAlarm ??= interactable;
                if (_mainFireAlarm == null && interactable.Matches(selectedSpawnPoint))
                    _mainFireAlarm = interactable;
            }

            FactoryEmergencyInteractable hintAlarm = _mainFireAlarm != null ? _mainFireAlarm : fallbackAlarm;
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
            _powerLightingController?.SetPowerOff(false);
            if (_interactables != null)
            {
                for (int index = 0; index < _interactables.Length; index++)
                    _interactables[index].SetInteractionEnabled(false);
            }

            _activeCircuitBreaker = null;
            _mainFireAlarm = null;
        }

        private void ResetResponse()
        {
            _powerLightingController?.SetPowerOff(false);
            if (_interactables != null)
            {
                for (int index = 0; index < _interactables.Length; index++)
                {
                    _interactables[index].ResetInteractionState();
                    _interactables[index].SetInteractionEnabled(false);
                }
            }

            _activeCircuitBreaker = null;
            _mainFireAlarm = null;
            SetStep(FactoryEmergencyResponseStep.None, null, null);
        }

        private static float HorizontalDistance(Vector3 from, Vector3 to)
        {
            Vector2 offset = new(to.x - from.x, to.z - from.z);
            return offset.magnitude;
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
