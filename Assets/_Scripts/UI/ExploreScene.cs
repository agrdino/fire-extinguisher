using _Scripts.Controller;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace _Scripts.UI
{
    public sealed class ExploreScene : MonoBehaviour, IScene
    {
        [SerializeField] private Button _btnStart;
        [SerializeField] private Button _btnBackGuide;
        [SerializeField] private TextMeshProUGUI _txtCountdown;

        private ApplicationManager _applicationManager;

        private void Awake()
        {
            _btnStart.onClick.AddListener(StartButton_OnClick);
            if (_btnBackGuide != null) _btnBackGuide.onClick.AddListener(BackGuideButton_OnClick);
        }

        private void OnDestroy()
        {
            _btnStart.onClick.RemoveListener(StartButton_OnClick);
            if (_btnBackGuide != null) _btnBackGuide.onClick.RemoveListener(BackGuideButton_OnClick);
            UnsubscribeFromTimer();
        }

        public void Show()
        {
            _applicationManager = ApplicationManager.Instance;

            _txtCountdown.gameObject.SetActive(_applicationManager.IsExploreTimeLimited);
            if (!_applicationManager.IsExploreTimeLimited) return;

            _applicationManager.OnRemainingTimeChanged += ApplicationManager_OnRemainingTimeChanged;
            ApplicationManager_OnRemainingTimeChanged(_applicationManager.RemainingTime);
        }

        public void Hide() => UnsubscribeFromTimer();

        private void ApplicationManager_OnRemainingTimeChanged(float remainingTime)
        {
            int seconds = Mathf.CeilToInt(remainingTime);
            _txtCountdown.SetText("Fire drill starts in {0}s", seconds);
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
