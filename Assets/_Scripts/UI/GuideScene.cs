using _Scripts.Controller;
using UnityEngine;
using UnityEngine.UI;

namespace _Scripts.UI
{
    public sealed class GuideScene : MonoBehaviour, IScene
    {
        [SerializeField] private ScrollRect _scrollView;
        [SerializeField] private Button _btnContinue;

        private void Awake()
        {
            if (_btnContinue != null) _btnContinue.onClick.AddListener(OnClickContinueButton);
        }

        private void OnDestroy()
        {
            if (_btnContinue != null) _btnContinue.onClick.RemoveListener(OnClickContinueButton);
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
    }
}
