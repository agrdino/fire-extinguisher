using _Scripts.Controller;
using _Scripts.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

namespace _Scripts.UI
{
    public sealed class FailedScene : MonoBehaviour, IScene
    {
        [SerializeField] private Button _btnRetry;

        private IApplicationNavigator _navigator;

        public void Initialize(IApplicationNavigator navigator)
        {
            _navigator = navigator;
        }

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
            RefreshButton();
        }

        public void Hide()
        {
        }

        private void OnRetryClicked()
        {
            if (_navigator == null)
            {
                Debug.LogError("FailedScene has no application navigator.", this);
                return;
            }

            _navigator.TryRestartCurrentEnvironment();
            RefreshButton();
        }

        private void RefreshButton()
        {
            if (_btnRetry != null)
                _btnRetry.interactable = _navigator != null && !_navigator.IsTransitioning;
        }
    }
}
