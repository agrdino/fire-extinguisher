using _Scripts.Controller;
using UnityEngine;
using UnityEngine.UI;

namespace _Scripts.UI
{
    public sealed class FailedScene : MonoBehaviour, IScene
    {
        [SerializeField] private Button _btnRetry;

        private void Awake()
        {
            if (_btnRetry != null) _btnRetry.onClick.AddListener(OnRetryClicked);
        }

        private void OnDestroy()
        {
            if (_btnRetry != null) _btnRetry.onClick.RemoveListener(OnRetryClicked);
        }

        public void Show()
        {
        }

        public void Hide()
        {
        }

        private void OnRetryClicked()
        {
            ApplicationManager.Instance.SetState(ApplicationState.Start);
        }
    }
}
