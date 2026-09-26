using TMPro;
using UnityEngine;
using UnityEngine.Localization;
using UnityEngine.Localization.Components;
using UnityEngine.Localization.Settings;

namespace _Scripts.UI
{
    public sealed class ContactEmergencyScene : MonoBehaviour, IScene
    {
        [SerializeField] private TMP_Text _txtGuide;
        [SerializeField] private LocalizedString _instruction = new("UI", "contact.instruction");

        private void Awake()
        {
            DisableGuideLocalizer();
        }

        public void Show()
        {
            DisableGuideLocalizer();
            LocalizationSettings.SelectedLocaleChanged += HandleLocaleChanged;
            RefreshContent();
        }

        public void Hide()
        {
            LocalizationSettings.SelectedLocaleChanged -= HandleLocaleChanged;
        }

        private void OnDestroy()
        {
            Hide();
        }

        private void HandleLocaleChanged(Locale locale)
        {
            RefreshContent();
        }

        private void RefreshContent()
        {
            if (_txtGuide != null) _txtGuide.SetText(_instruction.GetLocalizedString());
        }

        private void DisableGuideLocalizer()
        {
            if (_txtGuide == null) return;
            LocalizeStringEvent localizer = _txtGuide.GetComponent<LocalizeStringEvent>();
            if (localizer != null) localizer.enabled = false;
        }
    }
}
