using _Scripts.Controller;
using _Scripts.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

namespace _Scripts.UI
{
    public sealed class CompletedScene : MonoBehaviour, IScene
    {
        [SerializeField] private Button _btnRestart;

        private IApplicationNavigator _navigator;

        public void Initialize(IApplicationNavigator navigator)
        {
            _navigator = navigator;
        }

        private void Awake()
        {
            if (_btnRestart != null) _btnRestart.onClick.AddListener(OnClickRestartButton);
        }

        private void OnDestroy()
        {
            if (_btnRestart != null) _btnRestart.onClick.RemoveListener(OnClickRestartButton);
        }

        public void Show()
        {
            RefreshButton();
        }

        public void Hide()
        {
        }

        private void OnClickRestartButton()
        {
            if (_navigator == null)
            {
                Debug.LogError("CompletedScene has no application navigator.", this);
                return;
            }

            _navigator.TryRestartCurrentEnvironment();
            RefreshButton();
        }

        private void RefreshButton()
        {
            if (_btnRestart != null)
                _btnRestart.interactable = _navigator != null && !_navigator.IsTransitioning;
        }
    }
}
