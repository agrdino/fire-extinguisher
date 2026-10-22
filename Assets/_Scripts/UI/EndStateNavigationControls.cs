using System;
using _Scripts.SceneManagement;
using TMPro;
using UnityEngine;
using UnityEngine.Localization;
using UnityEngine.Localization.Components;
using UnityEngine.UI;

namespace _Scripts.UI
{
    [Serializable]
    public sealed class EndStateNavigationControls
    {
        [Header("Buttons")]
        [SerializeField] private Button _btnReturnToStart;
        [SerializeField] private Button _btnSwitchEnvironment;
        [SerializeField] private TMP_Text _txtReturnToStart;
        [SerializeField] private TMP_Text _txtSwitchEnvironment;

        [Header("Standard Layout")]
        [SerializeField] private Vector2 _startLeftPosition = new(-430f, -200f);
        [SerializeField] private Vector2 _restartCenterPosition = new(0f, -200f);
        [SerializeField] private Vector2 _switchRightPosition = new(430f, -200f);

        [Header("Factory Completed Layout")]
        [SerializeField] private Vector2 _factoryCompletedStartPosition = new(-430f, -200f);
        [SerializeField] private Vector2 _factoryCompletedRestartPosition = new(430f, -200f);
        [SerializeField] private Vector2 _factoryCompletedSwitchPosition = new(0f, -200f);

        [Header("Parking Completed Layout")]
        [SerializeField] private Vector2 _parkingCompletedRestartPosition = new(-430f, -200f);
        [SerializeField] private Vector2 _parkingCompletedStartPosition = new(0f, -200f);
        [SerializeField] private Vector2 _parkingCompletedSwitchPosition = new(430f, -200f);

        [Header("Button Sizes")]
        [SerializeField] private Vector2 _secondarySize = new(260f, 60f);
        [SerializeField] private Vector2 _primarySize = new(360f, 76f);

        [Header("Labels")]
        [SerializeField] private LocalizedString _returnToStartLabel = new("UI", "common.environment_selection");
        [SerializeField] private LocalizedString _factoryLabel = new("UI", "environment.factory");
        [SerializeField] private LocalizedString _parkingAreaLabel = new("UI", "environment.parking_area");

        private IApplicationNavigator _navigator;
        private Button _btnRestart;

        public void Initialize(IApplicationNavigator navigator, Button btnRestart)
        {
            Unbind();
            _navigator = navigator;
            _btnRestart = btnRestart;

            if (_btnRestart != null) _btnRestart.onClick.AddListener(Restart);
            if (_btnReturnToStart != null) _btnReturnToStart.onClick.AddListener(ReturnToStart);
            if (_btnSwitchEnvironment != null) _btnSwitchEnvironment.onClick.AddListener(SwitchEnvironment);

            DisableStaticLocalizer(_txtReturnToStart);
            DisableStaticLocalizer(_txtSwitchEnvironment);
        }

        public void Unbind()
        {
            if (_btnRestart != null) _btnRestart.onClick.RemoveListener(Restart);
            if (_btnReturnToStart != null) _btnReturnToStart.onClick.RemoveListener(ReturnToStart);
            if (_btnSwitchEnvironment != null) _btnSwitchEnvironment.onClick.RemoveListener(SwitchEnvironment);
        }

        public void Refresh(bool isCompleted)
        {
            SceneId currentScene = _navigator?.CurrentEnvironmentScene ?? SceneId.Start;
            SceneId targetScene = GetOtherEnvironment(currentScene);

            if (_txtReturnToStart != null)
                _txtReturnToStart.SetText(_returnToStartLabel.GetLocalizedString());
            if (_txtSwitchEnvironment != null)
            {
                LocalizedString targetLabel = targetScene == SceneId.Factory
                    ? _factoryLabel
                    : _parkingAreaLabel;
                _txtSwitchEnvironment.SetText(targetLabel.GetLocalizedString());
            }

            ApplyLayout(currentScene, isCompleted);
            RefreshInteractableState();
        }

        private void Restart()
        {
            if (_navigator == null) return;
            _navigator.TryRestartCurrentEnvironment();
            RefreshInteractableState();
        }

        private void ReturnToStart()
        {
            if (_navigator == null) return;
            _navigator.TryReturnToEnvironmentSelection();
            RefreshInteractableState();
        }

        private void SwitchEnvironment()
        {
            if (_navigator == null) return;
            _navigator.TryEnterEnvironment(GetOtherEnvironment(_navigator.CurrentEnvironmentScene));
            RefreshInteractableState();
        }

        private void RefreshInteractableState()
        {
            bool interactable = _navigator != null && !_navigator.IsTransitioning;
            if (_btnRestart != null) _btnRestart.interactable = interactable;
            if (_btnReturnToStart != null) _btnReturnToStart.interactable = interactable;
            if (_btnSwitchEnvironment != null) _btnSwitchEnvironment.interactable = interactable;
        }

        private void ApplyLayout(SceneId currentScene, bool isCompleted)
        {
            RectTransform restart = _btnRestart != null ? _btnRestart.transform as RectTransform : null;
            RectTransform start = _btnReturnToStart != null ? _btnReturnToStart.transform as RectTransform : null;
            RectTransform switchEnvironment = _btnSwitchEnvironment != null
                ? _btnSwitchEnvironment.transform as RectTransform
                : null;

            if (isCompleted && currentScene == SceneId.Factory)
            {
                SetLayout(start, _factoryCompletedStartPosition, _secondarySize);
                SetLayout(restart, _factoryCompletedRestartPosition, _secondarySize);
                SetLayout(switchEnvironment, _factoryCompletedSwitchPosition, _primarySize);
                return;
            }

            if (isCompleted && currentScene == SceneId.ParkingArea)
            {
                SetLayout(restart, _parkingCompletedRestartPosition, _secondarySize);
                SetLayout(start, _parkingCompletedStartPosition, _primarySize);
                SetLayout(switchEnvironment, _parkingCompletedSwitchPosition, _secondarySize);
                return;
            }

            SetLayout(start, _startLeftPosition, _secondarySize);
            SetLayout(restart, _restartCenterPosition, _primarySize);
            SetLayout(switchEnvironment, _switchRightPosition, _secondarySize);
        }

        private static void SetLayout(RectTransform rectTransform, Vector2 position, Vector2 size)
        {
            if (rectTransform == null) return;
            rectTransform.anchorMin = new Vector2(0.5f, 0.5f);
            rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
            rectTransform.pivot = new Vector2(0.5f, 0.5f);
            rectTransform.anchoredPosition = position;
            rectTransform.sizeDelta = size;
        }

        private static SceneId GetOtherEnvironment(SceneId currentScene)
        {
            return currentScene == SceneId.Factory ? SceneId.ParkingArea : SceneId.Factory;
        }

        private static void DisableStaticLocalizer(TMP_Text label)
        {
            if (label == null) return;
            LocalizeStringEvent localizer = label.GetComponent<LocalizeStringEvent>();
            if (localizer != null) localizer.enabled = false;
        }
    }
}
