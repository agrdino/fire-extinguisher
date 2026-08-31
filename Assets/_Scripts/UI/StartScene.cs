using _Scripts.Controller;
using _Scripts.FireExtinguishers;
using UnityEngine;
using UnityEngine.UI;

namespace _Scripts.UI
{
    public sealed class StartScene : MonoBehaviour, IScene
    {
        [SerializeField] private Button _btnStart;

        private FireExtinguisherController _fireExtinguisherController;

        private void Awake()
        {
            if (_btnStart == null)
                _btnStart = FindButton("btnStart") ?? FindButton("btnConfirm");
            if (_btnStart != null) _btnStart.onClick.AddListener(OnClickStartButton);
        }

        private void OnDestroy()
        {
            if (_btnStart != null) _btnStart.onClick.RemoveListener(OnClickStartButton);
        }

        private void Update()
        {
            RefreshStartButton();
        }

        public void Show()
        {
            ResolveFireExtinguisherController();
            RefreshStartButton();
        }

        public void Hide() { }

        private void OnClickStartButton()
        {
            RefreshStartButton();
            if (_fireExtinguisherController == null
                || !_fireExtinguisherController.IsReadyToStart)
                return;

            ApplicationManager.Instance.SetState(ApplicationState.Guide);
        }

        private void RefreshStartButton()
        {
            ResolveFireExtinguisherController();
            if (_btnStart != null)
                _btnStart.interactable =
                    _fireExtinguisherController != null
                    && _fireExtinguisherController.IsReadyToStart;
        }

        private void ResolveFireExtinguisherController()
        {
            if (_fireExtinguisherController == null)
                _fireExtinguisherController = FireExtinguisherController.Instance;
        }

        private Button FindButton(string buttonName)
        {
            Button[] buttons = GetComponentsInChildren<Button>(true);
            foreach (Button button in buttons)
                if (button.name == buttonName)
                    return button;
            return null;
        }
    }
}
