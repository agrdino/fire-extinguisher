using _Scripts.Controller;
using _Scripts.FireExtinguishers;
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

        [Header("View")]
        [SerializeField] private CanvasGroup _canvasGroup;
        [SerializeField] private TMP_Text _messageText;

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
        private global::_Scripts.FireExtinguishers.FireExtinguisher _fireExtinguisher;
        private Camera _baseCamera;
        private Camera _overlayCamera;
        private bool _ownsOverlayCamera;
        private int _originalBaseCameraMask;
        private float _idleTime;
        private float _targetAlpha;
        private HintStep _currentStep;

        private void Awake()
        {
            if (_instance != null && _instance != this)
            {
                Destroy(gameObject);
                return;
            }

            _instance = this;
            _applicationManager = ApplicationManager.Instance;
            _fireExtinguisher = FireExtinguisherController.Instance?.FireExtinguisher;

            if (_applicationManager == null)
            {
                Debug.LogWarning("Idle hints require an ApplicationManager in the active scene.", this);
                enabled = false;
                return;
            }

            if (_canvasGroup == null || _messageText == null)
            {
                Debug.LogError("Idle Hint Popup prefab is missing its CanvasGroup or message text reference.", this);
                enabled = false;
                return;
            }

            LazyFollow lazyFollow = GetComponent<LazyFollow>();
            lazyFollow.target = Camera.main != null ? Camera.main.transform : null;

            int layer = LayerMask.NameToLayer(AlwaysOnTopLayerName);
            if (layer != AlwaysOnTopLayer || gameObject.layer != layer)
            {
                Debug.LogError($"Idle Hint Popup must use layer {AlwaysOnTopLayer} ({AlwaysOnTopLayerName}).", this);
                layer = gameObject.layer;
            }

            _canvasGroup.alpha = 0f;
            _canvasGroup.interactable = false;
            _canvasGroup.blocksRaycasts = false;

            EnsureOverlayCamera(layer);
            BindEvents();
            RefreshRequirement(true);
        }

        private void Update()
        {
            if (_canvasGroup == null) return;

            HintStep requiredStep = ResolveRequiredStep();
            if (requiredStep != _currentStep)
                SetStep(requiredStep);

            if (_currentStep != HintStep.None && _targetAlpha <= 0f)
            {
                _idleTime += Time.unscaledDeltaTime;
                if (_idleTime >= _idleDelay) ShowCurrentHint();
            }

            float fadeSpeed = _fadeDuration > 0f ? Time.unscaledDeltaTime / _fadeDuration : 1f;
            _canvasGroup.alpha = Mathf.MoveTowards(_canvasGroup.alpha, _targetAlpha, fadeSpeed);
        }

        private void OnDestroy()
        {
            UnbindEvents();
            TearDownOverlayCamera();
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
            HideHint();
        }

        private void ShowCurrentHint()
        {
            _messageText.SetText(GetHintText(_currentStep));
            _targetAlpha = 1f;
        }

        private void HideHint()
        {
            _targetAlpha = 0f;
            if (_canvasGroup == null) return;
            _canvasGroup.interactable = false;
            _canvasGroup.blocksRaycasts = false;
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

        private void EnsureOverlayCamera(int layer)
        {
            _baseCamera = Camera.main;
            if (_baseCamera == null)
            {
                Debug.LogWarning("Idle hints could not find a Main Camera; the popup may be occluded.", this);
                return;
            }

            Transform existing = _baseCamera.transform.Find(OverlayCameraName);
            if (existing != null && existing.TryGetComponent(out _overlayCamera))
            {
                AddCameraToStack(_baseCamera, _overlayCamera);
                return;
            }

            GameObject cameraObject = new GameObject(OverlayCameraName);
            cameraObject.transform.SetParent(_baseCamera.transform, false);
            _overlayCamera = cameraObject.AddComponent<Camera>();
            _overlayCamera.clearFlags = CameraClearFlags.Nothing;
            _overlayCamera.cullingMask = 1 << layer;
            _overlayCamera.depth = _baseCamera.depth + 1f;
            _overlayCamera.useOcclusionCulling = false;

            UniversalAdditionalCameraData overlayData = _overlayCamera.GetUniversalAdditionalCameraData();
            overlayData.renderType = CameraRenderType.Overlay;
            overlayData.renderShadows = false;

            _originalBaseCameraMask = _baseCamera.cullingMask;
            _baseCamera.cullingMask &= ~(1 << layer);
            AddCameraToStack(_baseCamera, _overlayCamera);
            _ownsOverlayCamera = true;
        }

        private static void AddCameraToStack(Camera baseCamera, Camera overlayCamera)
        {
            UniversalAdditionalCameraData baseData = baseCamera.GetUniversalAdditionalCameraData();
            if (!baseData.cameraStack.Contains(overlayCamera))
                baseData.cameraStack.Add(overlayCamera);
        }

        private void TearDownOverlayCamera()
        {
            if (_baseCamera != null && _overlayCamera != null)
            {
                UniversalAdditionalCameraData baseData = _baseCamera.GetUniversalAdditionalCameraData();
                baseData.cameraStack.Remove(_overlayCamera);
            }

            if (!_ownsOverlayCamera || _overlayCamera == null) return;
            if (_baseCamera != null) _baseCamera.cullingMask = _originalBaseCameraMask;
            Destroy(_overlayCamera.gameObject);
        }
    }
}
