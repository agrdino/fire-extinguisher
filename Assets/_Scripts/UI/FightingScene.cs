using _Scripts.FireExtinguishers;
using _Scripts.Controller;
using TMPro;
using UnityEngine;
using UnityEngine.Localization;
using UnityEngine.Localization.Components;
using UnityEngine.Localization.Settings;
using UnityEngine.UI;

namespace _Scripts.UI
{
    public class FightingScene : MonoBehaviour, IScene
    {
        [SerializeField] private Slider _sldFireExtinguisher;
        [SerializeField] private TMP_Text _txtGuide;
        [SerializeField] private LocalizedString _guideString = new("UI", "fighting.guide");

        [Header("Selected Extinguisher")]
        [SerializeField] private TMP_Text _txtExtinguisherName;
        [SerializeField] private TMP_Text _txtExtinguisherDescription;
        [SerializeField] private LocalizedString _selectExtinguisherPrompt = new("UI", "hint.select_extinguisher");
        [SerializeField] private LocalizedString _co2Name = new("UI", "select.co2_name");
        [SerializeField] private LocalizedString _co2Description = new("UI", "select.co2_description");
        [SerializeField] private LocalizedString _powderName = new("UI", "select.powder_name");
        [SerializeField] private LocalizedString _powderDescription = new("UI", "select.powder_description");
        
        private void OnEnable()
        {
            if (_txtGuide == null) _txtGuide = transform.Find("UI Guide Title/txtGuide")?.GetComponent<TMP_Text>();
            if (_txtGuide != null)
            {
                LocalizeStringEvent localizer = _txtGuide.GetComponent<LocalizeStringEvent>();
                if (localizer != null) localizer.enabled = false;
            }
            if (FireExtinguisherController.Instance == null || ApplicationManager.Instance == null) return;
            if (_sldFireExtinguisher != null)
            {
                _sldFireExtinguisher.minValue = 0f;
                _sldFireExtinguisher.maxValue = 100f;
                _sldFireExtinguisher.value = FireExtinguisherController.Instance.FireExtinguisher.RemainingRatio * 100f;
            }
            FireExtinguisherController.Instance.FireExtinguisher.OnRemainingAmountChanged += OnValueChanged;
            ApplicationManager.Instance.OnRemainingTimeChanged += OnRemainingTimeChanged;
            ApplicationManager.Instance.OnExtinguisherSelected += OnExtinguisherSelected;
            LocalizationSettings.SelectedLocaleChanged += OnSelectedLocaleChanged;
            OnRemainingTimeChanged(ApplicationManager.Instance.RemainingTime);
            RefreshExtinguisherInfo(ApplicationManager.Instance.SelectedExtinguisherType);
        }

        public void Show()
        {
        }

        public void Hide()
        {
        }

        private void OnDisable()
        {
            if (FireExtinguisherController.Instance != null) FireExtinguisherController.Instance.FireExtinguisher.OnRemainingAmountChanged -= OnValueChanged;
            if (ApplicationManager.Instance != null)
            {
                ApplicationManager.Instance.OnRemainingTimeChanged -= OnRemainingTimeChanged;
                ApplicationManager.Instance.OnExtinguisherSelected -= OnExtinguisherSelected;
            }
            LocalizationSettings.SelectedLocaleChanged -= OnSelectedLocaleChanged;
        }

        private void OnValueChanged(float amount, float ratio)
        {
            if (_sldFireExtinguisher != null) _sldFireExtinguisher.value = ratio * 100f;
        }

        private void OnRemainingTimeChanged(float remainingTime)
        {
            if (_txtGuide == null) return;
            int totalSeconds = Mathf.CeilToInt(remainingTime);
            int minutes = totalSeconds / 60;
            int seconds = totalSeconds % 60;
            _txtGuide.SetText(_guideString.GetLocalizedString(minutes.ToString("00"), seconds.ToString("00")));
        }

        private void OnExtinguisherSelected(FireExtinguisherType type) => RefreshExtinguisherInfo(type);

        private void OnSelectedLocaleChanged(UnityEngine.Localization.Locale locale)
        {
            ApplicationManager applicationManager = ApplicationManager.Instance;
            RefreshExtinguisherInfo(applicationManager != null
                ? applicationManager.SelectedExtinguisherType
                : FireExtinguisherType.Unselect);
        }

        private void RefreshExtinguisherInfo(FireExtinguisherType type)
        {
            if (_txtExtinguisherName == null || _txtExtinguisherDescription == null) return;

            switch (type)
            {
                case FireExtinguisherType.CO2:
                    _txtExtinguisherName.SetText(_co2Name.GetLocalizedString());
                    _txtExtinguisherDescription.SetText(_co2Description.GetLocalizedString());
                    break;
                case FireExtinguisherType.Powder:
                    _txtExtinguisherName.SetText(_powderName.GetLocalizedString());
                    _txtExtinguisherDescription.SetText(_powderDescription.GetLocalizedString());
                    break;
                default:
                    _txtExtinguisherName.SetText(_selectExtinguisherPrompt.GetLocalizedString());
                    _txtExtinguisherDescription.SetText(string.Empty);
                    break;
            }
        }
    }
}
