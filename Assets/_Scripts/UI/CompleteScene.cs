using _Scripts.Controller;
using UnityEngine;
using UnityEngine.UI;

namespace _Scripts.UI
{
    public class CompleteScene : MonoBehaviour, IScene
    {
        [SerializeField] private Button _btnBack;

        private void Awake()
        {
            if (_btnBack != null) _btnBack.onClick.AddListener(OnClickBackButton);
        }

        private void OnDestroy()
        {
            if (_btnBack != null) _btnBack.onClick.RemoveListener(OnClickBackButton);
        }

        public void Show()
        {
        }

        public void Hide()
        {
        }

        private void OnClickBackButton()
        {
            ApplicationManager.Instance.SetState(ApplicationState.Selecting);
        }
    }
}
