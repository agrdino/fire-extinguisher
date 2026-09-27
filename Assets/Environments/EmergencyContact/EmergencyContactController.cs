using System;
using _Scripts.Controller;
using _Scripts.UI;
using UnityEngine;

namespace _Scripts.Environments.EmergencyContact
{
    public enum EmergencyContactStep
    {
        None,
        InProgress,
        Completed
    }

    [DisallowMultipleComponent]
    public sealed class EmergencyContactController : MonoBehaviour
    {
        [Tooltip("Legacy ordered interactions. Used only when no keypad controller is assigned.")]
        [SerializeField] private EmergencyContactInteractable[] _steps = Array.Empty<EmergencyContactInteractable>();
        [SerializeField] private EmergencyPhoneKeypadController _keypad;
        [SerializeField] private Transform _uiAnchor;

        private ApplicationManager _applicationManager;
        private int _currentStepIndex = -1;

        public EmergencyContactStep CurrentStep { get; private set; }
        public int CurrentStepIndex => _currentStepIndex;
        public Transform CurrentHintTarget => _keypad != null ? _keypad.HintTarget : GetCurrentInteractable()?.HintTarget;
        public Transform UIAnchor => _uiAnchor != null ? _uiAnchor : transform;

        public event Action<int, EmergencyContactInteractable> OnInteractionCompleted;
        public event Action<int> OnStepChanged;

        private void Start()
        {
            _applicationManager = ApplicationManager.Instance;
            if (_keypad == null) _keypad = GetComponent<EmergencyPhoneKeypadController>();
            if (_keypad != null) _keypad.ValidCallSubmitted += HandleValidCallSubmitted;
            if (_applicationManager == null) return;
            _applicationManager.OnStateChanged += HandleApplicationStateChanged;
            HandleApplicationStateChanged(_applicationManager.State);
        }

        private void OnDestroy()
        {
            if (_keypad != null) _keypad.ValidCallSubmitted -= HandleValidCallSubmitted;
            if (_applicationManager != null) _applicationManager.OnStateChanged -= HandleApplicationStateChanged;
        }

        public void HandleInteraction(EmergencyContactInteractable interactable)
        {
            EmergencyContactInteractable currentInteractable = GetCurrentInteractable();
            if (CurrentStep != EmergencyContactStep.InProgress || interactable == null || interactable != currentInteractable) return;

            int completedStepIndex = _currentStepIndex;
            interactable.SetInteractionEnabled(false);
            OnInteractionCompleted?.Invoke(completedStepIndex, interactable);
            _currentStepIndex++;

            if (_currentStepIndex < _steps.Length)
            {
                EnableCurrentStep();
                return;
            }

            CurrentStep = EmergencyContactStep.Completed;
            OnStepChanged?.Invoke(_currentStepIndex);
            IdleHintController.Instance?.NotifyActivity();
            _applicationManager.CompleteEmergencyContact();
        }

        private void HandleApplicationStateChanged(ApplicationState state)
        {
            if (state == ApplicationState.ContactEmergencyTeam)
            {
                BeginSequence();
                return;
            }
            ResetSequence();
        }

        private void BeginSequence()
        {
            ResetSequence();
            if (_keypad != null)
            {
                CurrentStep = EmergencyContactStep.InProgress;
                _currentStepIndex = 0;
                _keypad.BeginSession();
                OnStepChanged?.Invoke(_currentStepIndex);
                IdleHintController.Instance?.NotifyActivity();
                return;
            }

            if (_steps == null || _steps.Length == 0)
            {
                Debug.LogError($"{name} has no emergency contact interaction steps.", this);
                return;
            }
            CurrentStep = EmergencyContactStep.InProgress;
            _currentStepIndex = 0;
            EnableCurrentStep();
        }

        private void EnableCurrentStep()
        {
            DisableAllSteps();
            EmergencyContactInteractable currentInteractable = GetCurrentInteractable();
            if (currentInteractable == null)
            {
                Debug.LogError($"{name} has a missing emergency contact interaction at index {_currentStepIndex}.", this);
                return;
            }
            currentInteractable.SetInteractionEnabled(true);
            OnStepChanged?.Invoke(_currentStepIndex);
            IdleHintController.Instance?.NotifyActivity();
        }

        private void ResetSequence()
        {
            _keypad?.EndSession();
            DisableAllSteps();
            if (_steps != null)
            {
                for (int index = 0; index < _steps.Length; index++) _steps[index]?.ResetInteractionState();
            }
            _currentStepIndex = -1;
            CurrentStep = EmergencyContactStep.None;
        }

        private void HandleValidCallSubmitted(EmergencyContactEntry contact)
        {
            if (CurrentStep != EmergencyContactStep.InProgress || _applicationManager == null) return;

            CurrentStep = EmergencyContactStep.Completed;
            _currentStepIndex = 1;
            OnStepChanged?.Invoke(_currentStepIndex);
            IdleHintController.Instance?.NotifyActivity();
            _applicationManager.CompleteEmergencyContact();
        }

        private void DisableAllSteps()
        {
            if (_steps == null) return;
            for (int index = 0; index < _steps.Length; index++) _steps[index]?.SetInteractionEnabled(false);
        }

        private EmergencyContactInteractable GetCurrentInteractable()
        {
            return _steps != null && _currentStepIndex >= 0 && _currentStepIndex < _steps.Length ? _steps[_currentStepIndex] : null;
        }
    }
}
