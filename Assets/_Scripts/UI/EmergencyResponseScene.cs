using _Scripts.Environments.EmergencyResponse;
using TMPro;
using UnityEngine;
using UnityEngine.Localization;
using UnityEngine.Localization.Settings;
using UnityEngine.Localization.Components;

namespace _Scripts.UI
{
    public sealed class EmergencyResponseScene : MonoBehaviour, IScene
    {
        [SerializeField] private TMP_Text _txtTitle;
        [SerializeField] private TMP_Text _txtGuide;
        [SerializeField] private LocalizedString _title = new("UI", "hint.title");
        [SerializeField] private LocalizedString _switchOffPowerMessage = new("UI", "factory.switch_off_power");
        [SerializeField] private LocalizedString _activateFireAlarmMessage = new("UI", "emergency.activate_fire_alarm");

        private EmergencyResponseController _responseController;

        private void Awake()
        {
            ResolveTextReferences();
        }

        public void Show()
        {
            BindResponseController();
            LocalizationSettings.SelectedLocaleChanged += HandleLocaleChanged;
            RefreshContent();
        }

        public void Hide()
        {
            LocalizationSettings.SelectedLocaleChanged -= HandleLocaleChanged;
            UnbindResponseController();
        }

        private void OnDestroy()
        {
            Hide();
        }

        private void BindResponseController()
        {
            UnbindResponseController();
            _responseController = FindFirstObjectByType<EmergencyResponseController>();
            if (_responseController != null) _responseController.OnStepChanged += HandleStepChanged;
        }

        private void UnbindResponseController()
        {
            if (_responseController != null) _responseController.OnStepChanged -= HandleStepChanged;
            _responseController = null;
        }

        private void HandleStepChanged(EmergencyResponseStep step)
        {
            RefreshContent();
            UIController.Instance?.RefreshCurrentScenePlacement();
        }

        private void HandleLocaleChanged(Locale locale)
        {
            RefreshContent();
        }

        private void RefreshContent()
        {
            ResolveTextReferences();
            if (_txtTitle != null) _txtTitle.SetText(_title.GetLocalizedString());
            if (_txtGuide == null) return;

            LocalizedString message = _responseController?.CurrentStep == EmergencyResponseStep.ActivateFireAlarm
                ? _activateFireAlarmMessage
                : _switchOffPowerMessage;
            _txtGuide.SetText(message.GetLocalizedString());
        }

        private void ResolveTextReferences()
        {
            if (_txtTitle == null) _txtTitle = transform.Find("Panel Canvas/Content/UI Guide Title/txtTitle")?.GetComponent<TMP_Text>();
            if (_txtGuide == null) _txtGuide = transform.Find("Panel Canvas/Content/UI Guide Title/txtGuide")?.GetComponent<TMP_Text>();
            DisableLocalizer(_txtTitle);
            DisableLocalizer(_txtGuide);
        }

        private static void DisableLocalizer(TMP_Text text)
        {
            if (text == null) return;
            LocalizeStringEvent localizer = text.GetComponent<LocalizeStringEvent>();
            if (localizer != null) localizer.enabled = false;
        }
    }
}
