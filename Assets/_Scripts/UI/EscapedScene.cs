using _Scripts.SceneManagement;
using TMPro;
using UnityEngine;
using UnityEngine.Localization;
using UnityEngine.Localization.Components;
using UnityEngine.Localization.Settings;
using UnityEngine.UI;

namespace _Scripts.UI
{
    public sealed class EscapedScene : MonoBehaviour, IScene
    {
        [SerializeField] private Button _btnRestart;
        [SerializeField] private TMP_Text _txtMessage;
        [SerializeField] private LocalizedString _escapedMessage = new("UI", "escaped.message");

        private IApplicationNavigator _navigator;

        public void Initialize(IApplicationNavigator navigator)
        {
            _navigator = navigator;
        }

        private void Awake()
        {
            if (_btnRestart == null) _btnRestart = UIComponentLookup.FindButton(this, "btnRestart");
            if (_txtMessage == null) _txtMessage = UIComponentLookup.FindText(this, "txtGuide");
            DisableStaticMessageLocalizer();
            ConfigureMessageText();
            if (_btnRestart != null) _btnRestart.onClick.AddListener(OnRestartClicked);
        }

        private void OnEnable()
        {
            LocalizationSettings.SelectedLocaleChanged += OnSelectedLocaleChanged;
        }

        private void OnDisable()
        {
            LocalizationSettings.SelectedLocaleChanged -= OnSelectedLocaleChanged;
        }

        private void OnDestroy()
        {
            if (_btnRestart != null) _btnRestart.onClick.RemoveListener(OnRestartClicked);
        }

        public void Show()
        {
            RefreshMessage();
            RefreshButton();
        }

        public void Hide()
        {
        }

        private void OnRestartClicked()
        {
            if (_navigator == null) return;

            _navigator.TryRestartCurrentEnvironment();
            RefreshButton();
        }

        private void RefreshButton()
        {
            if (_btnRestart != null)
                _btnRestart.interactable = _navigator != null && !_navigator.IsTransitioning;
        }

        private void RefreshMessage()
        {
            if (_txtMessage != null) _txtMessage.SetText(_escapedMessage.GetLocalizedString());
        }

        private void DisableStaticMessageLocalizer()
        {
            if (_txtMessage == null) return;
            LocalizeStringEvent localizer = _txtMessage.GetComponent<LocalizeStringEvent>();
            if (localizer != null) localizer.enabled = false;
        }

        private void ConfigureMessageText()
        {
            if (_txtMessage == null) return;
            _txtMessage.enableAutoSizing = true;
            _txtMessage.fontSizeMin = 18f;
            _txtMessage.fontSizeMax = 42f;
        }

        private void OnSelectedLocaleChanged(Locale locale) => RefreshMessage();
    }
}
