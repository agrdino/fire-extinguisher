using _Scripts.Controller;
using _Scripts.FireExtinguishers;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactors;
using UnityEngine.XR.Interaction.Toolkit.Interactors.Visuals;

namespace _Scripts.UI
{
    [DisallowMultipleComponent]
    public sealed class RayInteractorStateController : MonoBehaviour
    {
        [SerializeField] private GameObject _rayInteractor;
        [SerializeField] private LeftHandObjectInteractor _objectInteractor;

        [Header("Ray Length")]
        [SerializeField, Min(0.1f)] private float _noHitLineLength = 1f;
        [SerializeField, Min(0.1f)] private float _lineLengthChangeSpeed = 12f;
        [SerializeField, Min(0.1f)] private float _factoryInteractionLineLength = 2f;

        [Header("Ray Colors")]
        [SerializeField] private Color _idleRayColor = Color.white;
        [SerializeField] private Color _uiRayColor = Color.cyan;
        [SerializeField] private Color _interactableRayColor = Color.green;

        private ApplicationManager _applicationManager;
        private XRRayInteractor _xrRayInteractor;
        private XRInteractorLineVisual _lineVisual;
        private float _maxLineLength;
        private float _currentLineLength;

        private RayGradientSet _whiteGradients;
        private RayGradientSet _cyanGradients;
        private RayGradientSet _greenGradients;
        private RayColor? _currentRayColor;

        private enum RayColor
        {
            White,
            Cyan,
            Green
        }

        private sealed class RayGradientSet
        {
            public readonly Gradient Valid;
            public readonly Gradient Invalid;
            public readonly Gradient Blocked;

            public RayGradientSet(Gradient valid, Gradient invalid, Gradient blocked)
            {
                Valid = valid;
                Invalid = invalid;
                Blocked = blocked;
            }
        }

        private void OnEnable()
        {
            ResolveRayComponents();
            Application.onBeforeRender += UpdateRayLineLength;

            _applicationManager = ApplicationManager.Instance;
            if (_applicationManager == null)
                return;

            _applicationManager.OnStateChanged += HandleApplicationStateChanged;
            HandleApplicationStateChanged(_applicationManager.State);
        }

        private void OnDisable()
        {
            Application.onBeforeRender -= UpdateRayLineLength;

            if (_applicationManager != null)
                _applicationManager.OnStateChanged -= HandleApplicationStateChanged;
            if (_lineVisual != null)
                _lineVisual.enabled = false;

            _applicationManager = null;
        }

        private void HandleApplicationStateChanged(ApplicationState state)
        {
            if (_rayInteractor != null)
            {
                bool shouldEnable = ShouldEnableRayInteractor(state);
                if (_rayInteractor.activeSelf != shouldEnable)
                    _rayInteractor.SetActive(shouldEnable);
            }

            if (_lineVisual != null)
                _lineVisual.enabled = ShouldShowRayVisual(state, IsHoveringStation())
                    && (_rayInteractor == null || _rayInteractor.activeSelf);
        }

        private void ResolveRayComponents()
        {
            if (_objectInteractor == null)
                _objectInteractor = GetComponentInChildren<LeftHandObjectInteractor>(true);
            if (_rayInteractor == null)
                return;

            _xrRayInteractor = _rayInteractor.GetComponent<XRRayInteractor>();
            _lineVisual = _rayInteractor.GetComponent<XRInteractorLineVisual>();
            if (_xrRayInteractor == null || _lineVisual == null)
                return;

            _maxLineLength = Mathf.Max(
                0.1f,
                _lineVisual.lineLength,
                _xrRayInteractor.maxRaycastDistance);

            _whiteGradients = CreateTintedGradients(
                _lineVisual.validColorGradient,
                _lineVisual.invalidColorGradient,
                _lineVisual.blockedColorGradient,
                _idleRayColor);
            _cyanGradients = CreateTintedGradients(
                _lineVisual.validColorGradient,
                _lineVisual.invalidColorGradient,
                _lineVisual.blockedColorGradient,
                _uiRayColor);
            _greenGradients = CreateTintedGradients(
                _lineVisual.validColorGradient,
                _lineVisual.invalidColorGradient,
                _lineVisual.blockedColorGradient,
                _interactableRayColor);

            _lineVisual.autoAdjustLineLength = false;
            _lineVisual.setLineColorGradient = true;
            _currentLineLength = Mathf.Clamp(_noHitLineLength, 0.1f, _maxLineLength);
            _lineVisual.lineLength = _currentLineLength;
            SetLineColor(RayColor.White);
            _lineVisual.enabled = false;
        }

[BeforeRenderOrder(XRInteractionUpdateOrder.k_BeforeRenderLineVisual - 1)]
        private void UpdateRayLineLength()
        {
            if (_xrRayInteractor == null
                || _lineVisual == null
                || !_xrRayInteractor.isActiveAndEnabled
                || _applicationManager == null)
                return;

            ApplicationState state = _applicationManager.State;
            bool isHoveringStation = IsHoveringStation();
            bool shouldShowLine = ShouldShowRayVisual(state, isHoveringStation);
            if (_lineVisual.enabled != shouldShowLine)
                _lineVisual.enabled = shouldShowLine;
            if (!shouldShowLine)
                return;

            bool hasUiHit = _xrRayInteractor.TryGetCurrentUIRaycastResult(out var uiHit)
                            && uiHit.gameObject != null;
            bool hasWorldHit = _xrRayInteractor.TryGetCurrent3DRaycastHit(out var worldHit)
                               && worldHit.collider != null;
            HandRayInteractable hitInteractable = hasWorldHit
                ? worldHit.collider.GetComponentInParent<HandRayInteractable>()
                : null;

            bool isEmergencyResponse = state == ApplicationState.EmergencyResponse;
            float targetLineLength = Mathf.Clamp(_noHitLineLength, 0.1f, _maxLineLength);
            RayColor targetColor = RayColor.White;

            if (state == ApplicationState.Fighting && isHoveringStation)
            {
                targetColor = RayColor.Green;
                targetLineLength = Mathf.Min(
                    GetEmergencyInteractionDistance(),
                    _objectInteractor.HoveredDistance);
            }
            else if (hasUiHit)
            {
                targetColor = RayColor.Cyan;
                float maximumUiRayLength = isEmergencyResponse
                    ? GetEmergencyInteractionDistance()
                    : _maxLineLength;
                targetLineLength = Mathf.Clamp(uiHit.distance, 0.1f, maximumUiRayLength);
            }
            else if (_objectInteractor != null && _objectInteractor.HasHoveredInteractable)
            {
                targetColor = RayColor.Green;
                targetLineLength = Mathf.Min(
                    isEmergencyResponse ? GetEmergencyInteractionDistance() : _maxLineLength,
                    _objectInteractor.HoveredDistance);
            }
            else if (hitInteractable != null)
            {
                // The ray found a hand-interactable, but the current phase or range
                // does not allow activating it.
                targetColor = RayColor.Cyan;
                float maximumObjectRayLength = isEmergencyResponse
                    ? GetEmergencyInteractionDistance()
                    : _maxLineLength;
                targetLineLength = Mathf.Min(maximumObjectRayLength, worldHit.distance);
            }

            _currentLineLength = Mathf.MoveTowards(
                _currentLineLength,
                targetLineLength,
                _lineLengthChangeSpeed * Time.unscaledDeltaTime);
            _lineVisual.lineLength = _currentLineLength;
            SetLineColor(targetColor);
        }

        private bool IsHoveringStation()
        {
            return _objectInteractor != null
                && _objectInteractor.HoveredInteractable is FireExtinguisherDisplayItem;
        }

        private float GetEmergencyInteractionDistance()
        {
            float interactionDistance = _objectInteractor != null
                ? _objectInteractor.MaximumInteractionDistance
                : _factoryInteractionLineLength;
            return Mathf.Clamp(interactionDistance, 0.1f, _maxLineLength);
        }

        private void SetLineColor(RayColor color)
        {
            if (_lineVisual == null || (_currentRayColor.HasValue && _currentRayColor.Value == color))
                return;

            RayGradientSet gradients = color switch
            {
                RayColor.Cyan => _cyanGradients,
                RayColor.Green => _greenGradients,
                _ => _whiteGradients
            };
            if (gradients == null)
                return;

            _lineVisual.validColorGradient = gradients.Valid;
            _lineVisual.invalidColorGradient = gradients.Invalid;
            _lineVisual.blockedColorGradient = gradients.Blocked;
            _currentRayColor = color;
        }

        private static RayGradientSet CreateTintedGradients(
            Gradient valid,
            Gradient invalid,
            Gradient blocked,
            Color tint)
        {
            return new RayGradientSet(
                CreateTintedGradient(valid, tint),
                CreateTintedGradient(invalid, tint),
                CreateTintedGradient(blocked, tint));
        }

        private static Gradient CreateTintedGradient(Gradient source, Color tint)
        {
            GradientColorKey[] sourceColorKeys = source != null ? source.colorKeys : null;
            if (sourceColorKeys == null || sourceColorKeys.Length == 0)
            {
                sourceColorKeys = new[]
                {
                    new GradientColorKey(Color.white, 0f),
                    new GradientColorKey(Color.white, 1f)
                };
            }

            GradientAlphaKey[] alphaKeys = source != null ? source.alphaKeys : null;
            if (alphaKeys == null || alphaKeys.Length == 0)
            {
                alphaKeys = new[]
                {
                    new GradientAlphaKey(1f, 0f),
                    new GradientAlphaKey(0f, 1f)
                };
            }

            GradientColorKey[] tintedColorKeys = new GradientColorKey[sourceColorKeys.Length];
            for (int index = 0; index < sourceColorKeys.Length; index++)
                tintedColorKeys[index] = new GradientColorKey(tint, sourceColorKeys[index].time);

            Gradient gradient = new Gradient();
            gradient.SetKeys(tintedColorKeys, alphaKeys);
            if (source != null)
                gradient.mode = source.mode;
            return gradient;
        }

        private static bool ShouldShowRayVisual(ApplicationState state, bool isHoveringStation)
        {
            if (state == ApplicationState.Fighting)
                return isHoveringStation;

            return state == ApplicationState.Ready
                || state == ApplicationState.Language
                || state == ApplicationState.SelectEnvironment
                || state == ApplicationState.Explore
                || state == ApplicationState.EmergencyResponse
                || state == ApplicationState.Completed
                || state == ApplicationState.Failed
                || state == ApplicationState.Escaped;
        }

        private static bool ShouldEnableRayInteractor(ApplicationState state)
        {
            // Keep the interactor available where other gameplay input may still use it.
            // The visual itself is controlled separately by ShouldShowRayVisual.
            return state != ApplicationState.Escape;
        }
    }
}