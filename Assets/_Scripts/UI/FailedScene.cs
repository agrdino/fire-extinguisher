using _Scripts.Controller;
using _Scripts.Fires;
using _Scripts.SceneManagement;
using TMPro;
using UnityEngine;
using UnityEngine.Localization;
using UnityEngine.Localization.Components;
using UnityEngine.Localization.Settings;
using UnityEngine.UI;

namespace _Scripts.UI
{
    public sealed class FailedScene : MonoBehaviour, IScene
    {
        [SerializeField] private Button _btnRetry;
        [SerializeField] private TMP_Text _txtMessage;
        [SerializeField] private LocalizedString _escapeTimedOutMessage = new("UI", "failed.reason.escape_timeout");
        [SerializeField] private LocalizedString _firefightingTimedOutMessage = new("UI", "failed.reason.firefighting_timeout");
        [SerializeField] private LocalizedString _extinguisherDepletedMessage = new("UI", "failed.reason.extinguisher_depleted");
        [SerializeField] private LocalizedString _fireNotExtinguishedMessage = new("UI", "failed.reason.fire_not_extinguished");
        [SerializeField] private LocalizedString _electricalWrongExtinguisherTip = new("UI", "failed.tip.wrong_extinguisher.electrical");
        [SerializeField] private LocalizedString _liquidWrongExtinguisherTip = new("UI", "failed.tip.wrong_extinguisher.liquid");

        private IApplicationNavigator _navigator;

        public void Initialize(IApplicationNavigator navigator)
        {
            _navigator = navigator;
        }

        private void Awake()
        {
            if (_btnRetry == null) _btnRetry = UIComponentLookup.FindButton(this, "btnRetry");
            if (_txtMessage == null) _txtMessage = UIComponentLookup.FindText(this, "txtGuide");
            DisableStaticMessageLocalizer();
            ConfigureMessageText();
            if (_btnRetry != null) _btnRetry.onClick.AddListener(OnRetryClicked);
        }

        private void OnEnable()
        {
            LocalizationSettings.SelectedLocaleChanged += OnSelectedLocaleChanged;
        }

        private void OnDisable()
        {
            LocalizationSettings.SelectedLocaleChanged -= OnSelectedLocaleChanged;
        }

        private void OnDestroy()
        {
            if (_btnRetry != null) _btnRetry.onClick.RemoveListener(OnRetryClicked);
        }

        public void Show()
        {
            RefreshMessage();
            RefreshButton();
        }

        public void Hide()
        {
        }

        private void OnRetryClicked()
        {
            if (_navigator == null)
            {
                Debug.LogError("FailedScene has no application navigator.", this);
                return;
            }

            _navigator.TryRestartCurrentEnvironment();
            RefreshButton();
        }

        private void RefreshButton()
        {
            if (_btnRetry != null)
                _btnRetry.interactable = _navigator != null && !_navigator.IsTransitioning;
        }

        private void RefreshMessage()
        {
            if (_txtMessage == null) return;

            ApplicationManager applicationManager = ApplicationManager.Instance;
            TrainingFailureReason reasons = applicationManager != null ? applicationManager.FailureReasons : TrainingFailureReason.FireNotExtinguished;
            string message = GetPrimaryReason(reasons).GetLocalizedString();
            if ((reasons & TrainingFailureReason.IncompatibleExtinguisherSelected) != 0)
                message = $"{message}\n\n{GetWrongExtinguisherTip().GetLocalizedString()}";
            _txtMessage.SetText(message);
        }

        private LocalizedString GetPrimaryReason(TrainingFailureReason reasons)
        {
            if ((reasons & TrainingFailureReason.EscapeTimedOut) != 0) return _escapeTimedOutMessage;
            if ((reasons & TrainingFailureReason.FirefightingTimedOut) != 0) return _firefightingTimedOutMessage;
            if ((reasons & TrainingFailureReason.ExtinguisherDepleted) != 0) return _extinguisherDepletedMessage;
            return _fireNotExtinguishedMessage;
        }

        private LocalizedString GetWrongExtinguisherTip()
        {
            FireController fireController = FireController.Instance;
            return fireController != null && fireController.CurrentFireType == FireType.Liquid
                ? _liquidWrongExtinguisherTip
                : _electricalWrongExtinguisherTip;
        }

        private void DisableStaticMessageLocalizer()
        {
            if (_txtMessage == null) return;
            LocalizeStringEvent localizer = _txtMessage.GetComponent<LocalizeStringEvent>();
            if (localizer != null) localizer.enabled = false;
        }

        private void ConfigureMessageText()
        {
            if (_txtMessage == null) return;
            _txtMessage.enableAutoSizing = true;
            _txtMessage.fontSizeMin = 18f;
            _txtMessage.fontSizeMax = 42f;
        }

        private void OnSelectedLocaleChanged(Locale locale) => RefreshMessage();
    }
}
