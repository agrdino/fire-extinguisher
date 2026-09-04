using _Scripts.FireExtinguishers;
using _Scripts.Controller;
using UnityEngine;
using UnityEngine.UI;

namespace _Scripts.UI
{
    public sealed class SelectExtinguisherScene : MonoBehaviour, IScene
    {
        [SerializeField] private Button _btnCO2;
        [SerializeField] private Button _btnPowder;
        [SerializeField] private Button _btnSelect;
        [SerializeField] private Button _btnBack;
        [SerializeField] private Color _selectedColor = new Color(0.16f, 0.55f, 0.82f, 0.9f);
        [SerializeField] private Color _unselectedColor = new Color(0f, 0f, 0f, 0.7f);

        private FireExtinguisherType _selectedType = FireExtinguisherType.Unselect;
        private FireExtinguisherModelSwitcher _modelSwitcher;

        public Transform ExtinguisherOptionsHintTarget => _btnCO2 != null
            ? _btnCO2.transform.parent
            : transform;
        public Transform ConfirmHintTarget => _btnSelect != null
            ? _btnSelect.transform
            : transform;

        private void Awake()
        {
            if (_btnCO2 != null) _btnCO2.onClick.AddListener(OnClickCO2Button);
            if (_btnPowder != null) _btnPowder.onClick.AddListener(OnClickPowderButton);
            if (_btnSelect != null) _btnSelect.onClick.AddListener(OnClickSelectButton);
            if (_btnBack != null) _btnBack.onClick.AddListener(OnClickBackButton);
        }

        private void OnDestroy()
        {
            UnbindModelSwitcher();
            if (_btnCO2 != null) _btnCO2.onClick.RemoveListener(OnClickCO2Button);
            if (_btnPowder != null) _btnPowder.onClick.RemoveListener(OnClickPowderButton);
            if (_btnSelect != null) _btnSelect.onClick.RemoveListener(OnClickSelectButton);
            if (_btnBack != null) _btnBack.onClick.RemoveListener(OnClickBackButton);
        }
        
        public void Show()
        {
            BindModelSwitcher();
            _selectedType = FireExtinguisherType.Unselect;
            UpdateSelectionVisuals();
        }

        public void Hide()
        {
            UnbindModelSwitcher();
        }
        
        private void OnClickSelectButton()
        {
            if (_selectedType == FireExtinguisherType.Unselect) return;
            ApplicationManager.Instance.SetState(ApplicationState.Fighting);
        }

        private void OnClickCO2Button()
        {
            SelectExtinguisher(FireExtinguisherType.CO2);
        }

        private void OnClickPowderButton()
        {
            SelectExtinguisher(FireExtinguisherType.Powder);
        }

        private void OnClickBackButton()
        {
            ApplicationManager.Instance.SetState(ApplicationState.Explore);
        }

        private void SelectExtinguisher(FireExtinguisherType extinguisherType)
        {
            if (_modelSwitcher != null && _modelSwitcher.IsTransitioning) return;

            _selectedType = extinguisherType;
            ApplicationManager.Instance.SelectExtinguisher(extinguisherType);
            UpdateSelectionVisuals();
        }

        private void UpdateSelectionVisuals()
        {
            bool canInteract = _modelSwitcher == null || !_modelSwitcher.IsTransitioning;
            if (_btnCO2 != null && _btnCO2.image != null) _btnCO2.image.color = _selectedType == FireExtinguisherType.CO2 ? _selectedColor : _unselectedColor;
            if (_btnPowder != null && _btnPowder.image != null) _btnPowder.image.color = _selectedType == FireExtinguisherType.Powder ? _selectedColor : _unselectedColor;
            if (_btnCO2 != null) _btnCO2.interactable = canInteract;
            if (_btnPowder != null) _btnPowder.interactable = canInteract;
            if (_btnSelect != null) _btnSelect.interactable = canInteract && _selectedType != FireExtinguisherType.Unselect;
        }

        private void BindModelSwitcher()
        {
            UnbindModelSwitcher();
            FireExtinguisher extinguisher = FireExtinguisherController.Instance?.FireExtinguisher;
            if (extinguisher == null) return;

            _modelSwitcher = extinguisher.GetComponent<FireExtinguisherModelSwitcher>();
            if (_modelSwitcher == null) return;

            _modelSwitcher.OnTransitionStarted += UpdateSelectionVisuals;
            _modelSwitcher.OnTransitionCompleted += UpdateSelectionVisuals;
        }

        private void UnbindModelSwitcher()
        {
            if (_modelSwitcher == null) return;

            _modelSwitcher.OnTransitionStarted -= UpdateSelectionVisuals;
            _modelSwitcher.OnTransitionCompleted -= UpdateSelectionVisuals;
            _modelSwitcher = null;
        }
    }
}
