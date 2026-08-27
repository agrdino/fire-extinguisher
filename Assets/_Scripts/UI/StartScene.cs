using _Scripts.Controller;
using UnityEngine;
using UnityEngine.UI;

namespace _Scripts.UI
{
    public class StartScene : MonoBehaviour, IScene
    {
        [SerializeField] private Button _btnFactory;
        [SerializeField] private Button _btnPark;
        [SerializeField] private Button _btnConfirm;
        [SerializeField] private Button _btnStart;
        [SerializeField] private Color _selectedColor = new Color(0.16f, 0.55f, 0.82f, 0.9f);
        [SerializeField] private Color _unselectedColor = new Color(0f, 0f, 0f, 0.7f);

        private EnviromentType _selectedEnviroment = EnviromentType.Factory;

        private void Awake()
        {
            if (_btnFactory != null) _btnFactory.onClick.AddListener(OnClickFactoryButton);
            if (_btnPark != null) _btnPark.onClick.AddListener(OnClickParkButton);
            if (_btnConfirm != null) _btnConfirm.onClick.AddListener(OnClickConfirmButton);
            if (_btnStart != null) _btnStart.onClick.AddListener(OnClickStartButton);
        }

        private void OnDestroy()
        {
            if (_btnFactory != null) _btnFactory.onClick.RemoveListener(OnClickFactoryButton);
            if (_btnPark != null) _btnPark.onClick.RemoveListener(OnClickParkButton);
            if (_btnConfirm != null) _btnConfirm.onClick.RemoveListener(OnClickConfirmButton);
            if (_btnStart != null) _btnStart.onClick.RemoveListener(OnClickStartButton);
        }

        public void Show()
        {
            _selectedEnviroment = EnviromentType.Factory;
            UpdateSelectionVisuals();
        }

        public void Hide()
        {
        }

        private void OnClickFactoryButton()
        {
            SelectEnviroment(EnviromentType.Factory);
        }

        private void OnClickParkButton()
        {
            SelectEnviroment(EnviromentType.Park);
        }

        private void OnClickConfirmButton()
        {
            EnviromentController.Instance.SetEnviroment(_selectedEnviroment);
            ApplicationManager.Instance.SetState(ApplicationState.Language);
        }

        private void OnClickStartButton()
        {
            EnviromentController.Instance.SetEnviroment(_selectedEnviroment);
            ApplicationManager.Instance.SetState(ApplicationState.Language);
        }

        private void SelectEnviroment(EnviromentType enviromentType)
        {
            _selectedEnviroment = enviromentType;
            UpdateSelectionVisuals();
        }

        private void UpdateSelectionVisuals()
        {
            if (_btnFactory != null && _btnFactory.image != null)
                _btnFactory.image.color = _selectedEnviroment == EnviromentType.Factory ? _selectedColor : _unselectedColor;
            if (_btnPark != null && _btnPark.image != null)
                _btnPark.image.color = _selectedEnviroment == EnviromentType.Park ? _selectedColor : _unselectedColor;
            if (_btnConfirm != null) _btnConfirm.interactable = true;
        }
    }
}
