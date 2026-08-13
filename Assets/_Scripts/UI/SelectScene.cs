using _Scripts.FireExtinguishers;
using _Scripts.Controller;
using UnityEngine;
using UnityEngine.UI;

namespace _Scripts.UI
{
    public class SelectScene : MonoBehaviour, IScene
    {
        [SerializeField] private Button _btnCO2;
        [SerializeField] private Button _btnPowder;
        [SerializeField] private Button _btnSelect;
        [SerializeField] private Button _btnBack;
        [SerializeField] private Color _selectedColor = new Color(0.16f, 0.55f, 0.82f, 0.9f);
        [SerializeField] private Color _unselectedColor = new Color(0f, 0f, 0f, 0.7f);

        private FireExtinguisherType _selectedType;

        private void Awake()
        {
            if (_btnCO2 != null) _btnCO2.onClick.AddListener(OnClickCO2Button);
            if (_btnPowder != null) _btnPowder.onClick.AddListener(OnClickPowderButton);
            if (_btnSelect != null) _btnSelect.onClick.AddListener(OnClickSelectButton);
            if (_btnBack != null) _btnBack.onClick.AddListener(OnClickBackButton);
        }

        private void OnDestroy()
        {
            if (_btnCO2 != null) _btnCO2.onClick.RemoveListener(OnClickCO2Button);
            if (_btnPowder != null) _btnPowder.onClick.RemoveListener(OnClickPowderButton);
            if (_btnSelect != null) _btnSelect.onClick.RemoveListener(OnClickSelectButton);
            if (_btnBack != null) _btnBack.onClick.RemoveListener(OnClickBackButton);
        }
        
        public void Show()
        {
            _selectedType = ApplicationManager.Instance.SelectedExtinguisherType;
            UpdateSelectionVisuals();
        }

        public void Hide()
        {
        }
        
        private void OnClickSelectButton()
        {
            ApplicationManager.Instance.SelectExtinguisher(_selectedType);
            ApplicationManager.Instance.SetState(ApplicationState.Playing);
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
            ApplicationManager.Instance.SetState(ApplicationState.Start);
        }

        private void SelectExtinguisher(FireExtinguisherType extinguisherType)
        {
            _selectedType = extinguisherType;
            UpdateSelectionVisuals();
        }

        private void UpdateSelectionVisuals()
        {
            if (_btnCO2 != null && _btnCO2.image != null) _btnCO2.image.color = _selectedType == FireExtinguisherType.CO2 ? _selectedColor : _unselectedColor;
            if (_btnPowder != null && _btnPowder.image != null) _btnPowder.image.color = _selectedType == FireExtinguisherType.Powder ? _selectedColor : _unselectedColor;
            if (_btnSelect != null) _btnSelect.interactable = true;
        }
    }
}
