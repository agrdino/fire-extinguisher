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
        [Tooltip("Ordered interactions required to complete the emergency call. The cube is the only step for now; handset and keypad actions can be appended later.")]
        [SerializeField] private EmergencyContactInteractable[] _steps = Array.Empty<EmergencyContactInteractable>();
        [SerializeField] private Transform _uiAnchor;

        private ApplicationManager _applicationManager;
        private int _currentStepIndex = -1;

        public EmergencyContactStep CurrentStep { get; private set; }
        public int CurrentStepIndex => _currentStepIndex;
        public Transform CurrentHintTarget => GetCurrentInteractable()?.HintTarget;
        public Transform UIAnchor => _uiAnchor != null ? _uiAnchor : transform;

        public event Action<int, EmergencyContactInteractable> OnInteractionCompleted;
        public event Action<int> OnStepChanged;

        private void Start()
        {
            _applicationManager = ApplicationManager.Instance;
            if (_applicationManager == null) return;
            _applicationManager.OnStateChanged += HandleApplicationStateChanged;
            HandleApplicationStateChanged(_applicationManager.State);
        }

        private void OnDestroy()
        {
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
            DisableAllSteps();
            if (_steps != null)
            {
                for (int index = 0; index < _steps.Length; index++) _steps[index]?.ResetInteractionState();
            }
            _currentStepIndex = -1;
            CurrentStep = EmergencyContactStep.None;
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
