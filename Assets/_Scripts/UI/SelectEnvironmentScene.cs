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

        private SceneId? _selectedEnvironmentScene;
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
            _selectedEnvironmentScene = null;
            UpdateSelectionVisuals();
        }

        public void Hide() { }

        private void SelectFactory() => SelectEnvironment(SceneId.Factory);
        private void SelectPark() => SelectEnvironment(SceneId.Park);

        private void ConfirmSelection()
        {
            if (!_selectedEnvironmentScene.HasValue) return;

            if (_navigator == null)
            {
                return;
            }

            _navigator.TryEnterEnvironment(_selectedEnvironmentScene.Value);
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
                _btnConfirm = UIComponentLookup.FindButton(this, "btnConfirm");
            if (_btnBack == null)
                _btnBack = UIComponentLookup.FindButton(this, "btnBack");

            Button[] optionButtons = UIComponentLookup.FindButtonsUnder(this, "Environments");
            if (_btnFactory == null && optionButtons.Length > 0) _btnFactory = optionButtons[0];
            if (_btnPark == null && optionButtons.Length > 1) _btnPark = optionButtons[1];
        }

        private void UpdateSelectionVisuals()
        {
            SetButtonVisual(_btnFactory, _selectedEnvironmentScene == SceneId.Factory);
            SetButtonVisual(_btnPark, _selectedEnvironmentScene == SceneId.Park);
            if (_btnConfirm != null)
                _btnConfirm.interactable = _selectedEnvironmentScene.HasValue
                    && _navigator != null
                    && !_navigator.IsTransitioning;
        }

        private static void SetButtonVisual(Button button, bool selected)
        {
            if (button == null) return;
            UIButtonTween tween = button.GetComponent<UIButtonTween>();
            if (tween != null) tween.SetSelected(selected);
        }
    }
}
