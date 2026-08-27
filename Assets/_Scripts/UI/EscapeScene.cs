using _Scripts.Controller;
using TMPro;
using UnityEngine;
using UnityEngine.Localization;
using UnityEngine.Localization.Components;
using UnityEngine.Localization.Settings;

namespace _Scripts.UI
{
    public class EscapeScene : MonoBehaviour, IScene
    {
        [SerializeField] private TMP_Text _txtGuide;
        [SerializeField] private LocalizedString _unlimitedString = new("UI", "escape.unlimited");
        [SerializeField] private LocalizedString _timedString = new("UI", "escape.timed");

        private void OnEnable()
        {
            if (_txtGuide != null)
            {
                LocalizeStringEvent localizer = _txtGuide.GetComponent<LocalizeStringEvent>();
                if (localizer != null) localizer.enabled = false;
            }
            ApplicationManager.Instance.OnRemainingTimeChanged += OnRemainingTimeChanged;
            LocalizationSettings.SelectedLocaleChanged += OnSelectedLocaleChanged;
            OnRemainingTimeChanged(ApplicationManager.Instance.RemainingTime);
        }

        private void OnDisable()
        {
            if (ApplicationManager.Instance != null)
                ApplicationManager.Instance.OnRemainingTimeChanged -= OnRemainingTimeChanged;
            LocalizationSettings.SelectedLocaleChanged -= OnSelectedLocaleChanged;
        }

        public void Show()
        {
        }

        public void Hide()
        {
        }

        public void Complete()
        {
        }

        private void OnRemainingTimeChanged(float remainingTime)
        {
            if (_txtGuide == null) return;
            if (!ApplicationManager.Instance.IsEscapeTimeLimited)
            {
                _txtGuide.SetText(_unlimitedString.GetLocalizedString());
                return;
            }

            int totalSeconds = Mathf.CeilToInt(remainingTime);
            int minutes = totalSeconds / 60;
            int seconds = totalSeconds % 60;
            _txtGuide.SetText(_timedString.GetLocalizedString(minutes.ToString("00"), seconds.ToString("00")));
        }

        private void OnSelectedLocaleChanged(Locale locale)
        {
            OnRemainingTimeChanged(ApplicationManager.Instance.RemainingTime);
        }

    }
}
