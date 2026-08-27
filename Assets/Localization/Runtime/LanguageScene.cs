using _Scripts.Controller;
using UnityEngine;
using UnityEngine.Localization;
using UnityEngine.Localization.Settings;
using UnityEngine.UI;

namespace _Scripts.UI
{
    [DisallowMultipleComponent]
    public sealed class LanguageScene : MonoBehaviour, IScene
    {
        private const string VietnameseCode = "vi";
        private const string EnglishCode = "en";
        private const string JapaneseCode = "ja";

        [SerializeField] private Button _btnVietnamese;
        [SerializeField] private Button _btnEnglish;
        [SerializeField] private Button _btnJapanese;
        [SerializeField] private Button _btnContinue;
        [SerializeField] private Button _btnBack;
        [SerializeField] private Color _selectedColor = new(0.16f, 0.55f, 0.82f, 0.9f);
        [SerializeField] private Color _unselectedColor = new(0f, 0f, 0f, 0.7f);

        private void Awake()
        {
            if (_btnVietnamese != null) _btnVietnamese.onClick.AddListener(SelectVietnamese);
            if (_btnEnglish != null) _btnEnglish.onClick.AddListener(SelectEnglish);
            if (_btnJapanese != null) _btnJapanese.onClick.AddListener(SelectJapanese);
            if (_btnContinue != null) _btnContinue.onClick.AddListener(Continue);
            if (_btnBack != null) _btnBack.onClick.AddListener(Back);
        }

        private void OnEnable()
        {
            LocalizationSettings.SelectedLocaleChanged += OnSelectedLocaleChanged;
            UpdateSelectionVisuals();
        }

        private void OnDisable()
        {
            LocalizationSettings.SelectedLocaleChanged -= OnSelectedLocaleChanged;
        }

        private void OnDestroy()
        {
            if (_btnVietnamese != null) _btnVietnamese.onClick.RemoveListener(SelectVietnamese);
            if (_btnEnglish != null) _btnEnglish.onClick.RemoveListener(SelectEnglish);
            if (_btnJapanese != null) _btnJapanese.onClick.RemoveListener(SelectJapanese);
            if (_btnContinue != null) _btnContinue.onClick.RemoveListener(Continue);
            if (_btnBack != null) _btnBack.onClick.RemoveListener(Back);
        }

        public void Show() => UpdateSelectionVisuals();
        public void Hide() { }

        private void SelectVietnamese() => SelectLocale(VietnameseCode);
        private void SelectEnglish() => SelectLocale(EnglishCode);
        private void SelectJapanese() => SelectLocale(JapaneseCode);

        private static void Continue()
        {
            ApplicationManager.Instance.SetState(ApplicationState.Guide);
        }

        private static void Back()
        {
            ApplicationManager.Instance.SetState(ApplicationState.Start);
        }

        private void SelectLocale(string code)
        {
            Locale locale = LocalizationSettings.AvailableLocales.GetLocale(code);
            if (locale == null)
            {
                Debug.LogError($"Locale '{code}' is not available.", this);
                return;
            }

            LocalizationSettings.SelectedLocale = locale;
        }

        private void OnSelectedLocaleChanged(Locale locale)
        {
            UpdateSelectionVisuals();
        }

        private void UpdateSelectionVisuals()
        {
            string selectedCode = LocalizationSettings.SelectedLocale?.Identifier.Code;
            SetButtonColor(_btnVietnamese, selectedCode == VietnameseCode);
            SetButtonColor(_btnEnglish, selectedCode == EnglishCode);
            SetButtonColor(_btnJapanese, selectedCode == JapaneseCode);
        }

        private void SetButtonColor(Button button, bool isSelected)
        {
            if (button != null && button.image != null)
                button.image.color = isSelected ? _selectedColor : _unselectedColor;
        }
    }
}