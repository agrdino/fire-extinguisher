using _Scripts.Controller;
using UnityEngine;
using UnityEngine.Localization;
using UnityEngine.Localization.Settings;
using UnityEngine.UI;

namespace _Scripts.UI
{
    [DisallowMultipleComponent]
    public sealed class SelectLanguageScene : MonoBehaviour, IScene
    {
        private const string VietnameseCode = "vi";
        private const string EnglishCode = "en";
        private const string JapaneseCode = "ja";

        [SerializeField] private Button _btnVietnamese;
        [SerializeField] private Button _btnEnglish;
        [SerializeField] private Button _btnJapanese;
        [SerializeField] private Button _btnContinue;

        private void Awake()
        {
            ResolveButtons();
            if (_btnVietnamese != null) _btnVietnamese.onClick.AddListener(SelectVietnamese);
            if (_btnEnglish != null) _btnEnglish.onClick.AddListener(SelectEnglish);
            if (_btnJapanese != null) _btnJapanese.onClick.AddListener(SelectJapanese);
            if (_btnContinue != null) _btnContinue.onClick.AddListener(Continue);
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
        }

        public void Show() => UpdateSelectionVisuals();
        public void Hide() { }

        private void SelectVietnamese() => SelectLocale(VietnameseCode);
        private void SelectEnglish() => SelectLocale(EnglishCode);
        private void SelectJapanese() => SelectLocale(JapaneseCode);

        private static void Continue()
        {
            ApplicationManager.Instance.SetState(ApplicationState.SelectEnvironment);
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

        private static void SetButtonColor(Button button, bool isSelected)
        {
            if (button == null) return;
            UIButtonTween tween = button.GetComponent<UIButtonTween>();
            if (tween != null) tween.SetSelected(isSelected);
        }

        private void ResolveButtons()
        {
            if (_btnContinue == null)
                _btnContinue = UIComponentLookup.FindButton(this, "btnContinue");

            Button[] languageButtons = UIComponentLookup.FindButtonsUnder(this, "Languages");
            if (_btnVietnamese == null && languageButtons.Length > 0) _btnVietnamese = languageButtons[0];
            if (_btnEnglish == null && languageButtons.Length > 1) _btnEnglish = languageButtons[1];
            if (_btnJapanese == null && languageButtons.Length > 2) _btnJapanese = languageButtons[2];
        }
    }
}
