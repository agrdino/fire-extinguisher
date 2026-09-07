using _Scripts.FireExtinguishers;
using _Scripts.Controller;
using TMPro;
using UnityEngine;
using UnityEngine.Localization;
using UnityEngine.Localization.Components;
using UnityEngine.UI;

namespace _Scripts.UI
{
    public class FightingScene : MonoBehaviour, IScene
    {
        [SerializeField] private Slider _sldFireExtinguisher;
        [SerializeField] private Slider _timerBar;
        [SerializeField] private TMP_Text _txtGuide;
        [SerializeField] private LocalizedString _guideString = new("UI", "fighting.guide");
        
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
            if (_timerBar != null)
            {
                _timerBar.minValue = 0f;
                _timerBar.maxValue = Mathf.Max(0.01f, ApplicationManager.Instance.RoundDuration);
            }
            FireExtinguisherController.Instance.FireExtinguisher.OnRemainingAmountChanged += OnValueChanged;
            ApplicationManager.Instance.OnRemainingTimeChanged += OnRemainingTimeChanged;
            OnRemainingTimeChanged(ApplicationManager.Instance.RemainingTime);
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
            if (ApplicationManager.Instance != null) ApplicationManager.Instance.OnRemainingTimeChanged -= OnRemainingTimeChanged;
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
            if (_timerBar != null) _timerBar.value = remainingTime;
        }
    }
}
