using _Scripts.Controller;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace _Scripts.UI
{
    public sealed class ExploreScene : MonoBehaviour, IScene
    {
        [SerializeField] private Button _btnStart;
        [SerializeField] private Component _txtCountdown;

        private ApplicationManager _applicationManager;

        private void Awake()
        {
            if (_btnStart != null) _btnStart.onClick.AddListener(OnClickStartButton);
        }

        private void OnDestroy()
        {
            if (_btnStart != null) _btnStart.onClick.RemoveListener(OnClickStartButton);
            UnsubscribeFromTimer();
        }

        public void Show()
        {
            _applicationManager = ApplicationManager.Instance;
            if (_applicationManager == null) return;

            _applicationManager.OnRemainingTimeChanged += UpdateCountdown;
            UpdateCountdown(_applicationManager.RemainingTime);
        }

        public void Hide()
        {
            UnsubscribeFromTimer();
        }

        private void OnClickStartButton()
        {
            ApplicationManager.Instance.CompleteExplore();
        }

        private void UpdateCountdown(float remainingTime)
        {
            int seconds = Mathf.CeilToInt(remainingTime);
            if (_txtCountdown is TMP_Text tmpText)
                tmpText.SetText("Fire drill starts in {0}s", seconds);
            else if (_txtCountdown is Text legacyText)
                legacyText.text = $"Fire drill starts in {seconds}s";
        }

        private void UnsubscribeFromTimer()
        {
            if (_applicationManager != null)
                _applicationManager.OnRemainingTimeChanged -= UpdateCountdown;
            _applicationManager = null;
        }
    }
}
