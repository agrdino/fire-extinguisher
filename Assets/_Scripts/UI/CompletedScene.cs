using _Scripts.Controller;
using _Scripts.Fires;
using _Scripts.SceneManagement;
using TMPro;
using UnityEngine;
using UnityEngine.Localization;
using UnityEngine.Localization.Components;
using UnityEngine.Localization.Settings;
using UnityEngine.UI;

namespace _Scripts.UI
{
    public sealed class CompletedScene : MonoBehaviour, IScene
    {
        [SerializeField] private Button _btnRestart;
        [SerializeField] private TMP_Text _txtMessage;
        [SerializeField] private LocalizedString _electricalCompleteMessage = new("UI", "complete.message.electrical");
        [SerializeField] private LocalizedString _liquidCompleteMessage = new("UI", "complete.message.liquid");

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
            if (_btnRestart != null) _btnRestart.onClick.AddListener(OnClickRestartButton);
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
            if (_btnRestart != null) _btnRestart.onClick.RemoveListener(OnClickRestartButton);
        }

        public void Show()
        {
            RefreshMessage();
            RefreshButton();
        }

        public void Hide()
        {
        }

        private void OnClickRestartButton()
        {
            if (_navigator == null)
            {
                Debug.LogError("CompletedScene has no application navigator.", this);
                return;
            }

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
            if (_txtMessage == null) return;

            FireController fireController = FireController.Instance;
            FireType fireType = fireController != null ? fireController.CurrentFireType : FireType.Electrical;
            LocalizedString message = fireType == FireType.Liquid ? _liquidCompleteMessage : _electricalCompleteMessage;
            _txtMessage.SetText(message.GetLocalizedString());
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
