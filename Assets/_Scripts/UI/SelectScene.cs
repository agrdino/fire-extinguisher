using _Scripts.Controller;
using UnityEngine;
using UnityEngine.UI;

namespace _Scripts.UI
{
    public class SelectScene : MonoBehaviour, IScene
    {
        [SerializeField] private Button _btnSelect;

        private void Awake()
        {
            _btnSelect.onClick.AddListener(OnClickSelectButton);
        }
        
        public void Show()
        {
        }

        public void Hide()
        {
        }
        
        private void OnClickSelectButton()
        {
            ApplicationManager.Instance.SetState(ApplicationState.Playing);
        }
    }
}
