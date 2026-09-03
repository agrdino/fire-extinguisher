using _Scripts.Controller;
using UnityEngine;
using UnityEngine.UI;

namespace _Scripts.UI
{
    public sealed class CompletedScene : MonoBehaviour, IScene
    {
        [SerializeField] private Button _btnRestart;

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
        }

        public void Hide()
        {
        }

        private void OnClickRestartButton()
        {
            ApplicationManager.Instance.SetState(ApplicationState.Ready);
        }
    }
}
