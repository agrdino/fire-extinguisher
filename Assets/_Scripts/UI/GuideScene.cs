using _Scripts.Controller;
using UnityEngine;
using UnityEngine.UI;

namespace _Scripts.UI
{
    public sealed class GuideScene : MonoBehaviour, IScene
    {
        [SerializeField] private ScrollRect _scrollView;
        [SerializeField] private Button _btnContinue;
        [SerializeField] private Button _btnBack;

        private void Awake()
        {
            if (_btnContinue != null) _btnContinue.onClick.AddListener(OnClickContinueButton);
            if (_btnBack != null) _btnBack.onClick.AddListener(OnClickBackButton);
        }

        private void OnDestroy()
        {
            if (_btnContinue != null) _btnContinue.onClick.RemoveListener(OnClickContinueButton);
            if (_btnBack != null) _btnBack.onClick.RemoveListener(OnClickBackButton);
        }

        public void Show()
        {
            if (_scrollView != null) _scrollView.verticalNormalizedPosition = 1f;
        }

        public void Hide()
        {
        }

        private static void OnClickContinueButton()
        {
            ApplicationManager.Instance.SetState(ApplicationState.Explore);
        }

        private void OnClickBackButton()
        {
            ApplicationManager.Instance.SetState(ApplicationState.Start);
        }
    }
}
