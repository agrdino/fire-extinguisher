using System.Collections.Generic;
using _Scripts.Controller;
using _Scripts.FireExtinguishers;
using _Scripts.FireExtinguishers.Visualizes;
using TMPro;
using UnityEngine;
using UnityEngine.Localization;
using UnityEngine.Localization.Settings;
using UnityEngine.Rendering.Universal;
using UnityEngine.XR.Interaction.Toolkit.UI;

namespace _Scripts.UI
{
    /// <summary>
    /// Shows a contextual hint when the user has not completed the next required action.
    /// Layout and follow settings live in the Idle Hint Popup prefab.
    /// </summary>
    [DefaultExecutionOrder(100)]
    [DisallowMultipleComponent]
    [RequireComponent(typeof(LazyFollow))]
    public sealed class IdleHintController : MonoBehaviour
    {
        private enum HintStep
        {
            None,
            ResetSafetyPin,
            SelectExtinguisher,
            ConfirmExtinguisher,
            RemoveSafetyPin,
            AimAndSqueeze,
            GoToEmergencyExit
        }

        private readonly struct PersistentMessage
        {
            public PersistentMessage(LocalizedString message, Transform target, ulong sequence)
            {
                Message = message;
                Target = target;
                Sequence = sequence;
            }

            public LocalizedString Message { get; }
            public Transform Target { get; }
            public ulong Sequence { get; }
        }

        private const int AlwaysOnTopLayer = 8;
        private const string AlwaysOnTopLayerName = "AlwaysOnTopUI";
        private const string OverlayCameraName = "Always On Top UI Camera";
        private static IdleHintController _instance;
        public static IdleHintController Instance => _instance;

        [Header("View")]
        [SerializeField] private CanvasGroup _canvasGroup;
        [SerializeField] private TMP_Text _messageText;

        [Header("Callout")]
        [SerializeField] private Material _curveMaterial;
        [SerializeField] private Color _curveColor = new(0.1254902f, 0.5882353f, 0.9529412f, 1f);
        [SerializeField, Min(0.0001f)] private float _curveWidth = 0.003f;
        [SerializeField, Min(0f)] private float _targetGap = 0.08f;
        [SerializeField, Min(0f)] private float _popupGap = 0.015f;
        [SerializeField, Min(0f)] private float _curveStrength = 0.18f;
        [SerializeField, Min(0.001f)] private float _targetAnchorRadius = 0.48f;
        [SerializeField] private float _targetAnchorVerticalOffset = 0.4f;

        [Header("Timing")]
        [SerializeField, Min(0f)] private float _idleDelay = 8f;
        [SerializeField, Min(0f)] private float _fadeDuration = 0.2f;

        [Header("Copy")]
        [SerializeField] private LocalizedString _resetSafetyPinHintLocalized = new("UI", "start.reset_safety_pin");
        [SerializeField] private LocalizedString _selectExtinguisherHintLocalized = new("UI", "hint.select_extinguisher");
        [SerializeField] private LocalizedString _confirmExtinguisherHintLocalized = new("UI", "hint.confirm_extinguisher");
        [SerializeField] private LocalizedString _removeSafetyPinHintLocalized = new("UI", "hint.remove_safety_pin");
        [SerializeField] private LocalizedString _aimAndSqueezeHintLocalized = new("UI", "hint.aim_and_squeeze");
        [SerializeField] private LocalizedString _goToEmergencyExitHintLocalized = new("UI", "hint.go_to_exit");

        private ApplicationManager _applicationManager;
        private FireExtinguisherController _fireExtinguisherController;
        private FireExtinguisher _fireExtinguisher;
        private Camera _baseCamera;
        private Camera _overlayCamera;
        private float _idleTime;
        private float _targetAlpha;
        private HintStep _currentStep;
        private HintCalloutCurve _calloutCurve;
        private readonly Dictionary<Object, PersistentMessage> _persistentMessages = new();
        private Object _activePersistentMessageSource;
        private ulong _persistentMessageSequence;
        private bool _isShowingTemporaryMessage;
        private float _remainingTemporaryMessageTime;
        private LocalizedString _temporaryMessage;
        private Transform _temporaryMessageTarget;

        private void Awake()
        {
            if (_instance != null && _instance != this)
            {
                Destroy(gameObject);
                return;
            }

            _instance = this;
            _applicationManager = ApplicationManager.Instance;
            _fireExtinguisherController = FireExtinguisherController.Instance;
            _fireExtinguisher = _fireExtinguisherController?.FireExtinguisher;

            if (_applicationManager == null) return;
            if (_canvasGroup == null || _messageText == null) return;

            int layer = LayerMask.NameToLayer(AlwaysOnTopLayerName);
            if (layer != AlwaysOnTopLayer || gameObject.layer != layer)
            {
                Debug.LogError($"Idle Hint Popup must use layer {AlwaysOnTopLayer} ({AlwaysOnTopLayerName}).", this);
                layer = gameObject.layer;
            }

            _canvasGroup.alpha = 0f;
            _canvasGroup.interactable = false;
            _canvasGroup.blocksRaycasts = false;

            EnsureOverlayCamera();
            EnsureCalloutCurve(layer);
            BindEvents();
            RefreshRequirement(true);
        }

        private void Update()
        {
            if (_canvasGroup == null) return;

            if (_persistentMessages.Count > 0)
            {
                // Persistent warnings are dismissed explicitly by their owner.
            }
            else if (_isShowingTemporaryMessage)
            {
                _remainingTemporaryMessageTime -= Time.unscaledDeltaTime;
                if (_remainingTemporaryMessageTime <= 0f)
                {
                    _isShowingTemporaryMessage = false;
                    _temporaryMessage = null;
                    _temporaryMessageTarget = null;
                    RefreshRequirement(true);
                }
            }
            else
            {
                HintStep requiredStep = ResolveRequiredStep();
                if (requiredStep != _currentStep)
                    SetStep(requiredStep);

                if (_currentStep != HintStep.None && _targetAlpha <= 0f)
                {
                    if (_currentStep == HintStep.ResetSafetyPin)
                    {
                        ShowCurrentHint();
                    }
                    else
                    {
                        _idleTime += Time.unscaledDeltaTime;
                        if (_idleTime >= _idleDelay) ShowCurrentHint();
                    }
                }
            }

            float fadeSpeed = _fadeDuration > 0f ? Time.unscaledDeltaTime / _fadeDuration : 1f;
            _canvasGroup.alpha = Mathf.MoveTowards(_canvasGroup.alpha, _targetAlpha, fadeSpeed);
        }

        private void OnDestroy()
        {
            UnbindEvents();
            if (_instance == this) _instance = null;
        }

        /// <summary>
        /// Resets the idle countdown. Call this from future interactions that do not already
        /// change the application, extinguisher selection, safety pin, or lever state.
        /// </summary>
        public void NotifyActivity()
        {
            RefreshRequirement(true);
        }

        public void ShowTemporaryMessage(LocalizedString message, Transform target, float duration)
        {
            if (message == null || message.IsEmpty || _messageText == null) return;

            _isShowingTemporaryMessage = true;
            _temporaryMessage = message;
            _temporaryMessageTarget = target;
            _remainingTemporaryMessageTime = Mathf.Max(duration, _fadeDuration);
            _idleTime = 0f;
            if (_persistentMessages.Count == 0)
                ApplyMessage(message, target);
        }

        public void ShowPersistentMessage(Object source, LocalizedString message, Transform target)
        {
            if (source == null || message == null || message.IsEmpty || _messageText == null) return;

            PersistentMessage persistentMessage = new(message, target, ++_persistentMessageSequence);
            _persistentMessages[source] = persistentMessage;
            _activePersistentMessageSource = source;
            _idleTime = 0f;
            ApplyMessage(persistentMessage.Message, persistentMessage.Target, true);
        }

        public void HidePersistentMessage(Object source)
        {
            if (source == null || !_persistentMessages.Remove(source)) return;
            if (_activePersistentMessageSource != source) return;

            if (TryGetLatestPersistentMessage(out Object latestSource, out PersistentMessage latestMessage))
            {
                _activePersistentMessageSource = latestSource;
                ApplyMessage(latestMessage.Message, latestMessage.Target, true);
                return;
            }

            _activePersistentMessageSource = null;
            if (_isShowingTemporaryMessage)
                ApplyMessage(_temporaryMessage, _temporaryMessageTarget);
            else
                RefreshRequirement(true);
        }

        private void BindEvents()
        {
            _applicationManager.OnStateChanged += OnApplicationStateChanged;
            _applicationManager.OnExtinguisherSelected += OnExtinguisherSelected;
            LocalizationSettings.SelectedLocaleChanged += OnSelectedLocaleChanged;
            if (_fireExtinguisher != null)
                _fireExtinguisher.OnStateChanged += OnFireExtinguisherStateChanged;
        }

        private void UnbindEvents()
        {
            if (_applicationManager != null)
            {
                _applicationManager.OnStateChanged -= OnApplicationStateChanged;
                _applicationManager.OnExtinguisherSelected -= OnExtinguisherSelected;
            }

            if (_fireExtinguisher != null)
                _fireExtinguisher.OnStateChanged -= OnFireExtinguisherStateChanged;
            LocalizationSettings.SelectedLocaleChanged -= OnSelectedLocaleChanged;
        }

        private void OnSelectedLocaleChanged(Locale locale)
        {
            if (_messageText == null || _targetAlpha <= 0f) return;
            if (_activePersistentMessageSource != null
                && _persistentMessages.TryGetValue(_activePersistentMessageSource, out PersistentMessage persistentMessage))
                _messageText.SetText(persistentMessage.Message.GetLocalizedString());
            else if (_isShowingTemporaryMessage && _temporaryMessage != null)
                _messageText.SetText(_temporaryMessage.GetLocalizedString());
            else if (_currentStep != HintStep.None)
                _messageText.SetText(GetHintText(_currentStep));
        }

        private void OnApplicationStateChanged(ApplicationState state)
        {
            RefreshRequirement(true);
        }

        private void OnExtinguisherSelected(FireExtinguisherType extinguisherType)
        {
            RefreshRequirement(true);
        }

        private void OnFireExtinguisherStateChanged(FireExtinguisherState state)
        {
            RefreshRequirement(true);
        }

        private void RefreshRequirement(bool resetTimer)
        {
            HintStep requiredStep = ResolveRequiredStep();
            if (_persistentMessages.Count > 0 || _isShowingTemporaryMessage)
            {
                _currentStep = requiredStep;
                if (resetTimer) _idleTime = 0f;
                return;
            }

            if (requiredStep == HintStep.ResetSafetyPin)
            {
                if (requiredStep != _currentStep)
                    SetStep(requiredStep);
                else
                    ShowCurrentHint();
                return;
            }

            if (requiredStep != _currentStep)
            {
                SetStep(requiredStep);
                return;
            }

            if (!resetTimer) return;
            _idleTime = 0f;
            HideHint();
        }

        private HintStep ResolveRequiredStep()
        {
            if (_applicationManager == null) return HintStep.None;

            if (_applicationManager.State == ApplicationState.Ready)
            {
                return _fireExtinguisherController != null
                    && _fireExtinguisherController.IsReadyToStart
                        ? HintStep.None
                        : HintStep.ResetSafetyPin;
            }

            if (_applicationManager.State == ApplicationState.SelectExtinguisher)
            {
                return _applicationManager.SelectedExtinguisherType == FireExtinguisherType.Unselect
                    ? HintStep.SelectExtinguisher
                    : HintStep.ConfirmExtinguisher;
            }

            if (_applicationManager.State == ApplicationState.Escape)
                return HintStep.GoToEmergencyExit;

            if (_applicationManager.State != ApplicationState.Fighting || _fireExtinguisher == null)
                return HintStep.None;

            FireExtinguisherState state = _fireExtinguisher.CurrentState;
            if (state.SafetyPin == SafetyPinState.Inserted) return HintStep.RemoveSafetyPin;
            if (state.Lever == LeverState.Released) return HintStep.AimAndSqueeze;
            return HintStep.None;
        }

        private void SetStep(HintStep step)
        {
            _currentStep = step;
            _idleTime = 0f;
            if (_calloutCurve != null) _calloutCurve.SetTarget(ResolveHintTarget(step));
            if (step == HintStep.ResetSafetyPin)
                ShowCurrentHint();
            else
                HideHint();
        }

        private void ShowCurrentHint()
        {
            _messageText.SetText(GetHintText(_currentStep));
            if (_calloutCurve != null)
            {
                _calloutCurve.SetTarget(ResolveHintTarget(_currentStep));
                _calloutCurve.SetVisible(true);
            }
            _targetAlpha = 1f;
        }

        private void HideHint()
        {
            _targetAlpha = 0f;
            if (_calloutCurve != null) _calloutCurve.SetVisible(false);
            if (_canvasGroup == null) return;
            _canvasGroup.interactable = false;
            _canvasGroup.blocksRaycasts = false;
        }

        private void ApplyMessage(LocalizedString message, Transform target, bool useCircularAnchor = false)
        {
            if (message == null || message.IsEmpty || _messageText == null) return;

            _messageText.SetText(message.GetLocalizedString());
            if (_calloutCurve != null)
            {
                _calloutCurve.SetTarget(target, useCircularAnchor);
                _calloutCurve.SetVisible(target != null);
            }
            _targetAlpha = 1f;
        }

        private bool TryGetLatestPersistentMessage(
            out Object latestSource,
            out PersistentMessage latestMessage)
        {
            latestSource = null;
            latestMessage = default;
            foreach (KeyValuePair<Object, PersistentMessage> pair in _persistentMessages)
            {
                if (latestSource != null && pair.Value.Sequence <= latestMessage.Sequence) continue;
                latestSource = pair.Key;
                latestMessage = pair.Value;
            }

            return latestSource != null;
        }

        private Transform ResolveHintTarget(HintStep step)
        {
            switch (step)
            {
                case HintStep.ResetSafetyPin:
                    return FindActivePartTarget<FireExtinguisherSafetyPin>(
                        component => component.HintTarget);

                case HintStep.SelectExtinguisher:
                case HintStep.ConfirmExtinguisher:
                {
                    SelectExtinguisherScene selectScene = UIController.Instance?.CurrentScene as SelectExtinguisherScene;
                    if (selectScene == null) return null;
                    return step == HintStep.SelectExtinguisher
                        ? selectScene.ExtinguisherOptionsHintTarget
                        : selectScene.ConfirmHintTarget;
                }

                case HintStep.RemoveSafetyPin:
                    return FindActivePartTarget<FireExtinguisherSafetyPin>(
                        component => component.HintTarget);

                case HintStep.AimAndSqueeze:
                    return FindActivePartTarget<FireExtinguisherLever>(
                        component => component.HintTarget);

                case HintStep.GoToEmergencyExit:
                    return _applicationManager?.EmergencyExit?.HintTarget;

                default:
                    return null;
            }
        }

        private Transform FindActivePartTarget<T>(System.Func<T, Transform> getTarget)
            where T : Component
        {
            if (_fireExtinguisher == null) return null;

            T[] parts = _fireExtinguisher.GetComponentsInChildren<T>(true);
            T fallback = null;
            foreach (T part in parts)
            {
                if (part == null) continue;
                fallback ??= part;
                if (part.gameObject.activeInHierarchy) return getTarget(part);
            }

            return fallback != null ? getTarget(fallback) : _fireExtinguisher.transform;
        }

        private string GetHintText(HintStep step)
        {
            return step switch
            {
                HintStep.ResetSafetyPin => _resetSafetyPinHintLocalized.GetLocalizedString(),
                HintStep.SelectExtinguisher => _selectExtinguisherHintLocalized.GetLocalizedString(),
                HintStep.ConfirmExtinguisher => _confirmExtinguisherHintLocalized.GetLocalizedString(),
                HintStep.RemoveSafetyPin => _removeSafetyPinHintLocalized.GetLocalizedString(),
                HintStep.AimAndSqueeze => _aimAndSqueezeHintLocalized.GetLocalizedString(),
                HintStep.GoToEmergencyExit => _goToEmergencyExitHintLocalized.GetLocalizedString(),
                _ => string.Empty
            };
        }

        private void EnsureOverlayCamera()
        {
            _baseCamera = Camera.main;
            if (_baseCamera == null)
            {
                Debug.LogWarning("Idle hints could not find a Main Camera; the popup may be occluded.", this);
                return;
            }

            Transform existing = _baseCamera.transform.Find(OverlayCameraName);
            if (existing == null || !existing.TryGetComponent(out _overlayCamera)) return;
            AddCameraToStack(_baseCamera, _overlayCamera);
        }

        private void EnsureCalloutCurve(int layer)
        {
            RectTransform popupRect = _canvasGroup != null ? _canvasGroup.transform as RectTransform : null;
            if (popupRect == null)
            {
                Debug.LogError("Idle Hint Popup requires its CanvasGroup on a RectTransform.", this);
                return;
            }

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

        private static void AddCameraToStack(Camera baseCamera, Camera overlayCamera)
        {
            UniversalAdditionalCameraData baseData = baseCamera.GetUniversalAdditionalCameraData();
            if (!baseData.cameraStack.Contains(overlayCamera))
                baseData.cameraStack.Add(overlayCamera);
        }

    }
}
