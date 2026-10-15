using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.Localization;
using UnityEngine.Localization.Settings;
using UnityEngine.Rendering.Universal;
using UnityEngine.XR.Interaction.Toolkit.UI;

namespace _Scripts.UI
{
    /// <summary>
    /// Owns the dedicated fire-proximity warning popup so safety warnings never replace
    /// the current instructional hint.
    /// </summary>
    [DefaultExecutionOrder(100)]
    [DisallowMultipleComponent]
    [RequireComponent(typeof(LazyFollow))]
    public sealed class FireWarningHintController : MonoBehaviour
    {
        private readonly struct WarningMessage
        {
            public WarningMessage(LocalizedString message, Transform target, ulong sequence)
            {
                Message = message;
                Target = target;
                Sequence = sequence;
            }

            public LocalizedString Message { get; }
            public Transform Target { get; }
            public ulong Sequence { get; }
        }

        private const string OverlayCameraName = "Always On Top UI Camera";
        private static FireWarningHintController _instance;
        public static FireWarningHintController Instance => _instance;

        [Header("View")]
        [SerializeField] private CanvasGroup _canvasGroup;
        [SerializeField] private TMP_Text _messageText;
        [SerializeField] private UIPopupTween _popupAnimation;

        [Header("Callout")]
        [SerializeField] private Material _curveMaterial;
        [SerializeField] private Color _curveColor = new(1f, 0.29f, 0.24f, 1f);
        [SerializeField, Min(0.0001f)] private float _curveWidth = 0.01f;
        [SerializeField, Min(0f)] private float _targetGap = 0.08f;
        [SerializeField, Min(0f)] private float _popupGap = 0.015f;
        [SerializeField, Min(0f)] private float _curveStrength = 0.18f;
        [SerializeField, Min(0.001f)] private float _targetAnchorRadius = 0.48f;
        [SerializeField] private float _targetAnchorVerticalOffset = 0.4f;

        [Header("Timing")]
        [SerializeField, Min(0f)] private float _fadeDuration = 0.2f;

        private readonly Dictionary<Object, WarningMessage> _warnings = new();
        private Object _activeSource;
        private ulong _sequence;
        private float _targetAlpha;
        private HintCalloutCurve _calloutCurve;
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
            EnsureCalloutCurve(gameObject.layer);
            LocalizationSettings.SelectedLocaleChanged += OnSelectedLocaleChanged;
        }

        private void Update()
        {
            if (_canvasGroup == null) return;

            float fadeSpeed = _fadeDuration > 0f ? Time.unscaledDeltaTime / _fadeDuration : 1f;
            _canvasGroup.alpha = Mathf.MoveTowards(_canvasGroup.alpha, _targetAlpha, fadeSpeed);
        }

        private void OnDestroy()
        {
            LocalizationSettings.SelectedLocaleChanged -= OnSelectedLocaleChanged;
            if (_instance == this) _instance = null;
        }

        public void ShowWarning(Object source, LocalizedString message, Transform target)
        {
            if (source == null || message == null || message.IsEmpty || _messageText == null) return;

            WarningMessage warning = new(message, target, ++_sequence);
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
            if (_calloutCurve != null)
            {
                _calloutCurve.SetTarget(warning.Target, true);
                _calloutCurve.SetVisible(warning.Target != null);
            }

            if (_targetAlpha <= 0f && _popupAnimation != null) _popupAnimation.PlayIn();
            _targetAlpha = 1f;
        }

        private void Hide()
        {
            if (_targetAlpha > 0f && _popupAnimation != null) _popupAnimation.PlayOut();
            _targetAlpha = 0f;
            if (_calloutCurve != null) _calloutCurve.SetVisible(false);
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

        private void EnsureCalloutCurve(int layer)
        {
            RectTransform popupRect = _canvasGroup != null ? _canvasGroup.transform as RectTransform : null;
            if (popupRect == null) return;

            _calloutCurve = GetComponent<HintCalloutCurve>();
            if (_calloutCurve == null) _calloutCurve = gameObject.AddComponent<HintCalloutCurve>();
            _calloutCurve.gameObject.layer = layer;
            _calloutCurve.Initialize(
                popupRect,
                _canvasGroup,
                _baseCamera,
                _curveMaterial,
                _curveColor,
                _curveWidth,
                _targetGap,
                _popupGap,
                _curveStrength,
                _targetAnchorRadius,
                _targetAnchorVerticalOffset);
        }
    }
}
