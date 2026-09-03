using _Scripts.Controller;
using _Scripts.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

namespace _Scripts.UI
{
    public sealed class SelectEnvironmentScene : MonoBehaviour, IScene
    {
        [SerializeField] private Button _btnFactory;
        [SerializeField] private Button _btnPark;
        [SerializeField] private Button _btnConfirm;
        [SerializeField] private Button _btnBack;
        [SerializeField] private Color _selectedColor = new(0.16f, 0.55f, 0.82f, 0.9f);
        [SerializeField] private Color _unselectedColor = new(0f, 0f, 0f, 0.7f);

        private SceneId _selectedEnvironmentScene = SceneId.Factory;
        private IApplicationNavigator _navigator;

        public void Initialize(IApplicationNavigator navigator)
        {
            _navigator = navigator;
        }

        private void Awake()
        {
            ResolveButtons();
            if (_btnFactory != null) _btnFactory.onClick.AddListener(SelectFactory);
            if (_btnPark != null) _btnPark.onClick.AddListener(SelectPark);
            if (_btnConfirm != null) _btnConfirm.onClick.AddListener(ConfirmSelection);
            if (_btnBack != null) _btnBack.onClick.AddListener(GoBack);
        }

        private void OnDestroy()
        {
            if (_btnFactory != null) _btnFactory.onClick.RemoveListener(SelectFactory);
            if (_btnPark != null) _btnPark.onClick.RemoveListener(SelectPark);
            if (_btnConfirm != null) _btnConfirm.onClick.RemoveListener(ConfirmSelection);
            if (_btnBack != null) _btnBack.onClick.RemoveListener(GoBack);
        }

        public void Show()
        {
            _selectedEnvironmentScene = SceneId.Factory;
            UpdateSelectionVisuals();
        }

        public void Hide() { }

        private void SelectFactory() => SelectEnvironment(SceneId.Factory);
        private void SelectPark() => SelectEnvironment(SceneId.Park);

        private void ConfirmSelection()
        {
            if (_navigator == null)
            {
                Debug.LogError("SelectEnvironmentScene has no application navigator.", this);
                return;
            }

            _navigator.TryEnterEnvironment(_selectedEnvironmentScene);
        }

        private static void GoBack()
        {
            ApplicationManager.Instance.SetState(ApplicationState.Language);
        }

        private void SelectEnvironment(SceneId sceneId)
        {
            _selectedEnvironmentScene = sceneId;
            UpdateSelectionVisuals();
        }

        private void ResolveButtons()
        {
            if (_btnConfirm == null)
                _btnConfirm = FindButton("btnConfirm");
            if (_btnBack == null)
                _btnBack = FindButton("btnBack");

            Transform options = FindChild("Environments");
            if (options == null) return;

            Button[] optionButtons = options.GetComponentsInChildren<Button>(true);
            if (_btnFactory == null && optionButtons.Length > 0) _btnFactory = optionButtons[0];
            if (_btnPark == null && optionButtons.Length > 1) _btnPark = optionButtons[1];
        }

        private Transform FindChild(string childName)
        {
            Transform[] children = GetComponentsInChildren<Transform>(true);
            foreach (Transform child in children)
                if (child.name == childName)
                    return child;
            return null;
        }

        private Button FindButton(string buttonName)
        {
            Button[] buttons = GetComponentsInChildren<Button>(true);
            foreach (Button button in buttons)
                if (button.name == buttonName)
                    return button;
            return null;
        }

        private void UpdateSelectionVisuals()
        {
            if (_btnFactory != null && _btnFactory.image != null)
                _btnFactory.image.color = _selectedEnvironmentScene == SceneId.Factory
                    ? _selectedColor
                    : _unselectedColor;
            if (_btnPark != null && _btnPark.image != null)
                _btnPark.image.color = _selectedEnvironmentScene == SceneId.Park
                    ? _selectedColor
                    : _unselectedColor;
            if (_btnConfirm != null)
                _btnConfirm.interactable = _navigator != null && !_navigator.IsTransitioning;
        }
    }
}
