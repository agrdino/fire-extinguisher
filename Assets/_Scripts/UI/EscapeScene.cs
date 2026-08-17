using _Scripts.Controller;
using TMPro;
using UnityEngine;

namespace _Scripts.UI
{
    public class EscapeScene : MonoBehaviour, IScene
    {
        [SerializeField] private TMP_Text _txtGuide;

        private void OnEnable()
        {
            ApplicationManager.Instance.OnRemainingTimeChanged += OnRemainingTimeChanged;
            OnRemainingTimeChanged(ApplicationManager.Instance.RemainingTime);
        }

        private void OnDisable()
        {
            if (ApplicationManager.Instance == null) return;
            ApplicationManager.Instance.OnRemainingTimeChanged -= OnRemainingTimeChanged;
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
                _txtGuide.SetText("TIME: UNLIMITED\nFollow the exit marker and reach the emergency exit.");
                return;
            }

            int totalSeconds = Mathf.CeilToInt(remainingTime);
            int minutes = totalSeconds / 60;
            int seconds = totalSeconds % 60;
            _txtGuide.SetText($"TIME: {minutes:00}:{seconds:00}\nFollow the exit marker and reach the emergency exit before time runs out.");
        }

    }
}
