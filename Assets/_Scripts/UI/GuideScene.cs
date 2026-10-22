using System;
using System.Collections.Generic;
using _Scripts.Controller;
using UnityEngine;
using UnityEngine.UI;

namespace _Scripts.UI
{
    public sealed class GuideScene : MonoBehaviour, IScene
    {
        [SerializeField] private ScrollRect _scrollView;
        [SerializeField] private Button _btnContinue;
        [SerializeField] private Button _btnBack;

        [Header("Controller Tutorial")]
        [SerializeField] private GuideControllerTutorialView _controllerTutorial;
        [SerializeField] private GuideTrackedControllerVisual _trackedControllerVisualPrefab;
        [SerializeField, Min(0f)] private float _stepAdvanceDelay = 2f;

        private readonly Dictionary<HandController, bool> _triggerPressed = new();
        private readonly Dictionary<HandController, Action<float>> _triggerHandlers = new();
        private int _currentTutorialStep;
        private float _advanceUnlockTime;
        private bool _isShowing;
        private bool _tutorialComplete;
        private bool _enableContinueAfterRelease;
        private HandController _leftController;
        private GuideTrackedControllerVisual _trackedControllerVisual;

        private void Awake()
        {
            if (_btnContinue == null) _btnContinue = UIComponentLookup.FindButton(this, "btnContinue");
            if (_btnBack == null) _btnBack = UIComponentLookup.FindButton(this, "btnBack");
            if (_btnContinue != null) _btnContinue.onClick.AddListener(OnClickContinueButton);
            if (_btnBack != null) _btnBack.onClick.AddListener(OnClickBackButton);
        }

        private void OnDestroy()
        {
            UnsubscribeFromControllerInput();
            if (_trackedControllerVisual != null) Destroy(_trackedControllerVisual.gameObject);
            if (_btnContinue != null) _btnContinue.onClick.RemoveListener(OnClickContinueButton);
            if (_btnBack != null) _btnBack.onClick.RemoveListener(OnClickBackButton);
        }

        private void Update()
        {
            if (!_isShowing) return;

            if (!_tutorialComplete && Time.unscaledTime >= _advanceUnlockTime)
                _controllerTutorial?.SetCanAdvance(true);

            if (!_enableContinueAfterRelease || IsAnyTriggerPressed()) return;
            _enableContinueAfterRelease = false;
            if (_btnContinue != null) _btnContinue.interactable = true;
        }

        public void Show()
        {
            if (_scrollView != null) _scrollView.verticalNormalizedPosition = 1f;
            _isShowing = true;
            _tutorialComplete = false;
            _enableContinueAfterRelease = false;
            _currentTutorialStep = 0;

            if (_btnContinue != null)
            {
                _btnContinue.interactable = false;
                _btnContinue.gameObject.SetActive(false);
            }

            SubscribeToControllerInput();
            EnsureTrackedControllerVisual();
            ShowTutorialStep(_currentTutorialStep);
        }

        public void Hide()
        {
            _isShowing = false;
            _enableContinueAfterRelease = false;
            _trackedControllerVisual?.Hide();
            UnsubscribeFromControllerInput();
        }

        private static void OnClickContinueButton()
        {
            ApplicationManager.Instance.SetState(ApplicationState.Explore);
        }

        private void OnClickBackButton()
        {
            ApplicationManager.Instance.SetState(ApplicationState.Ready);
        }

        private void ShowTutorialStep(int stepIndex)
        {
            _currentTutorialStep = stepIndex;
            _advanceUnlockTime = Time.unscaledTime + _stepAdvanceDelay;
            _controllerTutorial?.ShowStep(stepIndex);
            _trackedControllerVisual?.ShowStep(stepIndex);
        }

        private void TryAdvanceTutorial()
        {
            if (!_isShowing || _tutorialComplete || Time.unscaledTime < _advanceUnlockTime) return;

            int nextStep = _currentTutorialStep + 1;
            if (nextStep < GuideControllerTutorialView.StepCount)
            {
                ShowTutorialStep(nextStep);
                return;
            }

            _tutorialComplete = true;
            _controllerTutorial?.ShowCompleted();
            _trackedControllerVisual?.Hide();
            if (_btnContinue == null) return;

            _btnContinue.gameObject.SetActive(true);
            _btnContinue.interactable = false;
            _enableContinueAfterRelease = true;
        }

        private void SubscribeToControllerInput()
        {
            UnsubscribeFromControllerInput();
            foreach (HandController controller in FindObjectsByType<HandController>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (!IsLeftController(controller)) continue;
                _leftController = controller;
                HandController source = controller;
                Action<float> handler = value => OnTriggerValueChanged(source, value);
                _triggerPressed[source] = false;
                _triggerHandlers[source] = handler;
                source.onPress += handler;
                break;
            }
        }

        private void EnsureTrackedControllerVisual()
        {
            if (_trackedControllerVisual != null)
            {
                if (_leftController != null && _trackedControllerVisual.transform.parent != _leftController.transform)
                    _trackedControllerVisual.transform.SetParent(_leftController.transform, false);
                return;
            }

            if (_trackedControllerVisualPrefab == null || _leftController == null) return;
            _trackedControllerVisual = Instantiate(_trackedControllerVisualPrefab, _leftController.transform, false);
            _trackedControllerVisual.name = _trackedControllerVisualPrefab.name;
        }

        private static bool IsLeftController(HandController controller)
        {
            return controller != null && string.Equals(controller.gameObject.name, "Left", StringComparison.OrdinalIgnoreCase);
        }

        private void UnsubscribeFromControllerInput()
        {
            foreach (KeyValuePair<HandController, Action<float>> pair in _triggerHandlers)
            {
                if (pair.Key != null) pair.Key.onPress -= pair.Value;
            }

            _triggerHandlers.Clear();
            _triggerPressed.Clear();
        }

        private void OnTriggerValueChanged(HandController source, float value)
        {
            bool wasPressed = _triggerPressed.TryGetValue(source, out bool pressed) && pressed;
            bool isPressed = value >= 0.75f;
            _triggerPressed[source] = isPressed;
            if (isPressed && !wasPressed) TryAdvanceTutorial();
        }

        private bool IsAnyTriggerPressed()
        {
            foreach (bool isPressed in _triggerPressed.Values)
            {
                if (isPressed) return true;
            }

            return false;
        }
    }
}
