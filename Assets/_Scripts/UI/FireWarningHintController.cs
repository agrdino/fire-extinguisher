using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.Localization;
using UnityEngine.Localization.Settings;
using UnityEngine.Rendering.Universal;

namespace _Scripts.UI
{
    /// <summary>
    /// Owns the dedicated fire-proximity warning popup so safety warnings never replace
    /// the current instructional hint.
    /// </summary>
    [DefaultExecutionOrder(100)]
    [DisallowMultipleComponent]
    public sealed class FireWarningHintController : MonoBehaviour
    {
        private readonly struct WarningMessage
        {
            public WarningMessage(LocalizedString message, ulong sequence)
            {
                Message = message;
                Sequence = sequence;
            }

            public LocalizedString Message { get; }
            public ulong Sequence { get; }
        }

        private const string OverlayCameraName = "Always On Top UI Camera";
        private static FireWarningHintController _instance;
        public static FireWarningHintController Instance => _instance;

        [Header("View")]
        [SerializeField] private CanvasGroup _canvasGroup;
        [SerializeField] private TMP_Text _messageText;
        [SerializeField] private UIPopupTween _popupAnimation;

        [Header("Fixed Follow")]
        [SerializeField] private Vector3 _viewOffset = new(0f, -0.2f, 1f);

        [Header("Timing")]
        [SerializeField, Min(0f)] private float _fadeDuration = 0.2f;

        private readonly Dictionary<Object, WarningMessage> _warnings = new();
        private Object _activeSource;
        private ulong _sequence;
        private float _targetAlpha;
        private Camera _baseCamera;
        private Camera _overlayCamera;

        private void Awake()
        {
            if (_instance != null && _instance != this)
            {
                Destroy(gameObject);
                return;
            }

            _instance = this;
            if (_canvasGroup == null || _messageText == null) return;

            _canvasGroup.alpha = 0f;
            _canvasGroup.interactable = false;
            _canvasGroup.blocksRaycasts = false;
            if (_popupAnimation == null)
                _popupAnimation = GetComponentInChildren<UIPopupTween>(true);

            EnsureOverlayCamera();
            UpdateFixedPose();
            LocalizationSettings.SelectedLocaleChanged += OnSelectedLocaleChanged;
        }

        private void Update()
        {
            if (_canvasGroup == null) return;

            float fadeSpeed = _fadeDuration > 0f ? Time.unscaledDeltaTime / _fadeDuration : 1f;
            _canvasGroup.alpha = Mathf.MoveTowards(_canvasGroup.alpha, _targetAlpha, fadeSpeed);
        }

        private void LateUpdate()
        {
            UpdateFixedPose();
        }

        private void OnDestroy()
        {
            LocalizationSettings.SelectedLocaleChanged -= OnSelectedLocaleChanged;
            if (_instance == this) _instance = null;
        }

        public void ShowWarning(Object source, LocalizedString message)
        {
            if (source == null || message == null || message.IsEmpty || _messageText == null) return;

            WarningMessage warning = new(message, ++_sequence);
            _warnings[source] = warning;
            _activeSource = source;
            ApplyWarning(warning);
        }

        public void HideWarning(Object source)
        {
            if (source == null || !_warnings.Remove(source)) return;
            if (_activeSource != source) return;

            if (TryGetLatestWarning(out Object latestSource, out WarningMessage latestWarning))
            {
                _activeSource = latestSource;
                ApplyWarning(latestWarning);
                return;
            }

            _activeSource = null;
            Hide();
        }

        private void ApplyWarning(WarningMessage warning)
        {
            _messageText.SetText(warning.Message.GetLocalizedString());
            if (_targetAlpha <= 0f && _popupAnimation != null) _popupAnimation.PlayIn();
            _targetAlpha = 1f;
        }

        private void Hide()
        {
            if (_targetAlpha > 0f && _popupAnimation != null) _popupAnimation.PlayOut();
            _targetAlpha = 0f;
        }

        private bool TryGetLatestWarning(out Object latestSource, out WarningMessage latestWarning)
        {
            latestSource = null;
            latestWarning = default;
            foreach (KeyValuePair<Object, WarningMessage> pair in _warnings)
            {
                if (latestSource != null && pair.Value.Sequence <= latestWarning.Sequence) continue;
                latestSource = pair.Key;
                latestWarning = pair.Value;
            }

            return latestSource != null;
        }

        private void OnSelectedLocaleChanged(Locale _)
        {
            if (_activeSource == null
                || !_warnings.TryGetValue(_activeSource, out WarningMessage warning))
                return;

            _messageText.SetText(warning.Message.GetLocalizedString());
        }

        private void EnsureOverlayCamera()
        {
            _baseCamera = Camera.main;
            if (_baseCamera == null)
            {
                Debug.LogWarning("Fire warning hint could not find a Main Camera; the popup may be occluded.", this);
                return;
            }

            Transform existing = _baseCamera.transform.Find(OverlayCameraName);
            if (existing == null || !existing.TryGetComponent(out _overlayCamera)) return;

            UniversalAdditionalCameraData baseData = _baseCamera.GetUniversalAdditionalCameraData();
            if (!baseData.cameraStack.Contains(_overlayCamera))
                baseData.cameraStack.Add(_overlayCamera);
        }

        private void UpdateFixedPose()
        {
            if (_baseCamera == null) return;

            Transform view = _baseCamera.transform;
            transform.SetPositionAndRotation(view.TransformPoint(_viewOffset), view.rotation);
        }
    }
}
