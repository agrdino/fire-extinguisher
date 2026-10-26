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

        [Header("Layout")]
        [SerializeField] private Vector2 _backPosition = new(25f, 25f);
        [SerializeField] private Vector2 _backSize = new(200f, 50f);
        [SerializeField] private Vector2 _singleActionPosition = new(0f, -200f);
        [SerializeField] private Vector2 _twoActionLeftPosition = new(-210f, -200f);
        [SerializeField] private Vector2 _twoActionRightPosition = new(210f, -200f);
        [SerializeField] private Vector2 _actionSize = new(320f, 76f);

        [Header("Labels")]
        [SerializeField] private LocalizedString _returnToStartLabel = new("UI", "common.back");
        [SerializeField] private LocalizedString _parkingAreaLabel = new("UI", "environment.parking_area");

        private IApplicationNavigator _navigator;
        private Button _btnRetry;

        public void Initialize(IApplicationNavigator navigator, Button btnRetry)
        {
            Unbind();
            _navigator = navigator;
            _btnRetry = btnRetry;

            if (_btnRetry != null) _btnRetry.onClick.AddListener(Retry);
            if (_btnReturnToStart != null) _btnReturnToStart.onClick.AddListener(ReturnToStart);
            if (_btnSwitchEnvironment != null) _btnSwitchEnvironment.onClick.AddListener(SwitchEnvironment);

            DisableStaticLocalizer(_txtReturnToStart);
            DisableStaticLocalizer(_txtSwitchEnvironment);
        }

        public void Unbind()
        {
            if (_btnRetry != null) _btnRetry.onClick.RemoveListener(Retry);
            if (_btnReturnToStart != null) _btnReturnToStart.onClick.RemoveListener(ReturnToStart);
            if (_btnSwitchEnvironment != null) _btnSwitchEnvironment.onClick.RemoveListener(SwitchEnvironment);
        }

        public void Refresh(bool isCompleted)
        {
            SceneId currentScene = _navigator?.CurrentEnvironmentScene ?? SceneId.Start;
            bool showParkingButton = isCompleted && currentScene == SceneId.Factory;

            if (_txtReturnToStart != null)
                _txtReturnToStart.SetText(_returnToStartLabel.GetLocalizedString());
            if (_txtSwitchEnvironment != null && showParkingButton)
                _txtSwitchEnvironment.SetText(_parkingAreaLabel.GetLocalizedString());

            ApplyLayout(showParkingButton);
            RefreshInteractableState();
        }

        private void Retry()
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
            _navigator.TryEnterEnvironment(SceneId.ParkingArea);
            RefreshInteractableState();
        }

        private void RefreshInteractableState()
        {
            bool interactable = _navigator != null && !_navigator.IsTransitioning;
            if (_btnRetry != null) _btnRetry.interactable = interactable;
            if (_btnReturnToStart != null) _btnReturnToStart.interactable = interactable;
            if (_btnSwitchEnvironment != null) _btnSwitchEnvironment.interactable = interactable;
        }

        private void ApplyLayout(bool showParkingButton)
        {
            RectTransform retry = _btnRetry != null ? _btnRetry.transform as RectTransform : null;
            RectTransform start = _btnReturnToStart != null ? _btnReturnToStart.transform as RectTransform : null;
            RectTransform switchEnvironment = _btnSwitchEnvironment != null
                ? _btnSwitchEnvironment.transform as RectTransform
                : null;

            if (_btnRetry != null) _btnRetry.gameObject.SetActive(true);
            if (_btnReturnToStart != null) _btnReturnToStart.gameObject.SetActive(true);
            if (_btnSwitchEnvironment != null)
                _btnSwitchEnvironment.gameObject.SetActive(showParkingButton);

            SetBottomLeftLayout(start, _backPosition, _backSize);

            if (showParkingButton)
            {
                SetCenteredLayout(retry, _twoActionLeftPosition, _actionSize);
                SetCenteredLayout(switchEnvironment, _twoActionRightPosition, _actionSize);
                return;
            }

            SetCenteredLayout(retry, _singleActionPosition, _actionSize);
        }

        private static void SetCenteredLayout(RectTransform rectTransform, Vector2 position, Vector2 size)
        {
            if (rectTransform == null) return;
            rectTransform.anchorMin = new Vector2(0.5f, 0.5f);
            rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
            rectTransform.pivot = new Vector2(0.5f, 0.5f);
            rectTransform.anchoredPosition = position;
            rectTransform.sizeDelta = size;
        }

        private static void SetBottomLeftLayout(RectTransform rectTransform, Vector2 position, Vector2 size)
        {
            if (rectTransform == null) return;
            rectTransform.anchorMin = Vector2.zero;
            rectTransform.anchorMax = Vector2.zero;
            rectTransform.pivot = Vector2.zero;
            rectTransform.anchoredPosition = position;
            rectTransform.sizeDelta = size;
        }

        private static void DisableStaticLocalizer(TMP_Text label)
        {
            if (label == null) return;
            LocalizeStringEvent localizer = label.GetComponent<LocalizeStringEvent>();
            if (localizer != null) localizer.enabled = false;
        }
    }
}
