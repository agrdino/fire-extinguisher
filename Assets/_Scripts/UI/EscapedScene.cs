using _Scripts.SceneManagement;
using TMPro;
using UnityEngine;
using UnityEngine.Localization;
using UnityEngine.Localization.Components;
using UnityEngine.Localization.Settings;
using UnityEngine.Serialization;
using UnityEngine.UI;

namespace _Scripts.UI
{
    public sealed class EscapedScene : MonoBehaviour, IScene
    {
        [FormerlySerializedAs("_btnRestart")]
        [SerializeField] private Button _btnRetry;
        [SerializeField] private TMP_Text _txtMessage;
        [SerializeField] private LocalizedString _escapedMessage = new("UI", "escaped.message");
        [SerializeField] private EndStateNavigationControls _navigationControls = new();

        private IApplicationNavigator _navigator;

        public void Initialize(IApplicationNavigator navigator)
        {
            _navigator = navigator;
            _navigationControls.Initialize(_navigator, _btnRetry);
        }

        private void Awake()
        {
            if (_btnRetry == null)
                _btnRetry = UIComponentLookup.FindButton(this, "btnRetry")
                    ?? UIComponentLookup.FindButton(this, "btnRestart");
            if (_txtMessage == null) _txtMessage = UIComponentLookup.FindText(this, "txtGuide");
            DisableStaticMessageLocalizer();
            ConfigureMessageText();
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
            _navigationControls.Unbind();
        }

        public void Show()
        {
            RefreshMessage();
            _navigationControls.Refresh(false);
        }

        public void Hide()
        {
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

        private void OnSelectedLocaleChanged(Locale locale)
        {
            RefreshMessage();
            _navigationControls.Refresh(false);
        }
    }
}
