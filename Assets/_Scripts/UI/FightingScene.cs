using _Scripts.FireExtinguishers;
using _Scripts.Controller;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace _Scripts.UI
{
    public class FightingScene : MonoBehaviour, IScene
    {
        [SerializeField] private Slider _sldFireExtinguisher;
        [SerializeField] private TMP_Text _txtGuide;
        
        private void OnEnable()
        {
            if (_txtGuide == null) _txtGuide = transform.Find("UI Guide Title/txtGuide")?.GetComponent<TMP_Text>();
            if (FireExtinguisherController.Instance == null || ApplicationManager.Instance == null) return;
            if (_sldFireExtinguisher != null) _sldFireExtinguisher.value = FireExtinguisherController.Instance.FireExtinguisher.RemainingRatio;
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
            if (_sldFireExtinguisher != null) _sldFireExtinguisher.value = ratio;
        }

        private void OnRemainingTimeChanged(float remainingTime)
        {
            if (_txtGuide == null) return;
            int totalSeconds = Mathf.CeilToInt(remainingTime);
            int minutes = totalSeconds / 60;
            int seconds = totalSeconds % 60;
            _txtGuide.SetText($"TIME: {minutes:00}:{seconds:00}\nRemove the safety pin, aim at the base of the fire, squeeze the lever, and sweep side to side.");
        }
    }
}
