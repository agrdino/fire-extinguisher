using _Scripts.Controller;
using _Scripts.FireExtinguishers;
using _Scripts.FireExtinguishers.Visualizes;
using TMPro;
using UnityEngine;
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
            SelectExtinguisher,
            ConfirmExtinguisher,
            RemoveSafetyPin,
            AimAndSqueeze,
            GoToEmergencyExit
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
        [SerializeField, Min(0f)] private float _viewportMargin = 0.08f;
        [SerializeField, Min(0f)] private float _curveStrength = 0.18f;

        [Header("Timing")]
        [SerializeField, Min(0f)] private float _idleDelay = 8f;
        [SerializeField, Min(0f)] private float _fadeDuration = 0.2f;

        [Header("Copy")]
        [SerializeField] private string _selectExtinguisherHint = "Select 1 of 2 extinguishers.";
        [SerializeField] private string _confirmExtinguisherHint = "Press CONFIRM to continue.";
        [SerializeField] private string _removeSafetyPinHint = "Remove the safety pin.";
        [SerializeField] private string _aimAndSqueezeHint = "Aim at the base of the fire and squeeze the lever.";
        [SerializeField] private string _goToEmergencyExitHint = "Follow the EXIT signs and go to the emergency exit.";

        private ApplicationManager _applicationManager;
        private FireExtinguisher _fireExtinguisher;
        private Camera _baseCamera;
        private Camera _overlayCamera;
        private float _idleTime;
        private float _targetAlpha;
        private HintStep _currentStep;
        private HintCalloutCurve _calloutCurve;
        private bool _isShowingTemporaryMessage;
        private float _remainingTemporaryMessageTime;

        private void Awake()
        {
            if (_instance != null && _instance != this)
            {
                Destroy(gameObject);
                return;
            }

            _instance = this;
            _applicationManager = ApplicationManager.Instance;
            _fireExtinguisher = FireExtinguisherController.Instance.FireExtinguisher;

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

            if (_isShowingTemporaryMessage)
            {
                _remainingTemporaryMessageTime -= Time.unscaledDeltaTime;
                if (_remainingTemporaryMessageTime <= 0f)
                {
                    _isShowingTemporaryMessage = false;
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
                    _idleTime += Time.unscaledDeltaTime;
                    if (_idleTime >= _idleDelay) ShowCurrentHint();
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

        public void ShowTemporaryMessage(string message, Transform target, float duration)
        {
            if (string.IsNullOrWhiteSpace(message) || _messageText == null) return;

            _isShowingTemporaryMessage = true;
            _remainingTemporaryMessageTime = Mathf.Max(duration, _fadeDuration);
            _idleTime = 0f;
            _messageText.SetText(message);
            if (_calloutCurve != null)
            {
                _calloutCurve.SetTarget(target);
                _calloutCurve.SetVisible(target != null);
            }
            _targetAlpha = 1f;
        }

        private void BindEvents()
        {
            _applicationManager.OnStateChanged += OnApplicationStateChanged;
            _applicationManager.OnExtinguisherSelected += OnExtinguisherSelected;
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
            if (_isShowingTemporaryMessage)
            {
                _currentStep = requiredStep;
                if (resetTimer) _idleTime = 0f;
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

            if (_applicationManager.State == ApplicationState.Selecting)
            {
                return _applicationManager.SelectedExtinguisherType == FireExtinguisherType.Unselect
                    ? HintStep.SelectExtinguisher
                    : HintStep.ConfirmExtinguisher;
            }

            if (_applicationManager.State == ApplicationState.Escape)
                return HintStep.GoToEmergencyExit;

            if (_applicationManager.State != ApplicationState.Playing || _fireExtinguisher == null)
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

        private Transform ResolveHintTarget(HintStep step)
        {
            switch (step)
            {
                case HintStep.SelectExtinguisher:
                case HintStep.ConfirmExtinguisher:
                {
                    SelectScene selectScene = UIController.Instance?.CurrentScene as SelectScene;
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
                HintStep.SelectExtinguisher => _selectExtinguisherHint,
                HintStep.ConfirmExtinguisher => _confirmExtinguisherHint,
                HintStep.RemoveSafetyPin => _removeSafetyPinHint,
                HintStep.AimAndSqueeze => _aimAndSqueezeHint,
                HintStep.GoToEmergencyExit => _goToEmergencyExitHint,
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
                _viewportMargin,
                _curveStrength);
        }

        private static void AddCameraToStack(Camera baseCamera, Camera overlayCamera)
        {
            UniversalAdditionalCameraData baseData = baseCamera.GetUniversalAdditionalCameraData();
            if (!baseData.cameraStack.Contains(overlayCamera))
                baseData.cameraStack.Add(overlayCamera);
        }

    }
}
