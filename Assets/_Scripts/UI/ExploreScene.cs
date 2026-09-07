using _Scripts.Controller;
using TMPro;
using UnityEngine;
using UnityEngine.Localization;
using UnityEngine.UI;

namespace _Scripts.UI
{
    public sealed class ExploreScene : MonoBehaviour, IScene
    {
        [SerializeField] private Button _btnStart;
        [SerializeField] private Button _btnBackGuide;
        [SerializeField] private TextMeshProUGUI _txtCountdown;
        [SerializeField] private Slider _timerBar;
        [SerializeField] private LocalizedString _countdownString = new("UI", "explore.countdown");

        private ApplicationManager _applicationManager;

        private void Awake()
        {
            if (_btnStart == null) _btnStart = UIComponentLookup.FindButton(this, "btnStart");
            if (_btnBackGuide == null) _btnBackGuide = UIComponentLookup.FindButton(this, "btnBack");
            if (_btnStart != null) _btnStart.onClick.AddListener(StartButton_OnClick);
            if (_btnBackGuide != null) _btnBackGuide.onClick.AddListener(BackGuideButton_OnClick);
        }

        private void OnDestroy()
        {
            if (_btnStart != null) _btnStart.onClick.RemoveListener(StartButton_OnClick);
            if (_btnBackGuide != null) _btnBackGuide.onClick.RemoveListener(BackGuideButton_OnClick);
            UnsubscribeFromTimer();
        }

        public void Show()
        {
            _applicationManager = ApplicationManager.Instance;

            _txtCountdown.gameObject.SetActive(_applicationManager.IsExploreTimeLimited);
            if (_timerBar != null) _timerBar.gameObject.SetActive(_applicationManager.IsExploreTimeLimited);
            if (!_applicationManager.IsExploreTimeLimited) return;

            if (_timerBar != null)
            {
                _timerBar.minValue = 0f;
                _timerBar.maxValue = Mathf.Max(0.01f, _applicationManager.ExploreDuration);
            }

            _applicationManager.OnRemainingTimeChanged += ApplicationManager_OnRemainingTimeChanged;
            ApplicationManager_OnRemainingTimeChanged(_applicationManager.RemainingTime);
        }

        public void Hide() => UnsubscribeFromTimer();

        private void ApplicationManager_OnRemainingTimeChanged(float remainingTime)
        {
            int seconds = Mathf.CeilToInt(remainingTime);
            _txtCountdown.SetText(_countdownString.GetLocalizedString(seconds));
            if (_timerBar != null) _timerBar.value = remainingTime;
        }

        private void UnsubscribeFromTimer()
        {
            if (_applicationManager == null) return;
            _applicationManager.OnRemainingTimeChanged -= ApplicationManager_OnRemainingTimeChanged;
            _applicationManager = null;
        }

        private void StartButton_OnClick()
        {
            ApplicationManager.Instance.CompleteExplore();
        }

        private void BackGuideButton_OnClick()
        {
            ApplicationManager.Instance.SetState(ApplicationState.Guide);
        }

    }
}
