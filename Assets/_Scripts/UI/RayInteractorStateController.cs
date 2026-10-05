using _Scripts.Controller;
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
        [SerializeField, Min(0.1f)] private float _factoryInteractionLineLength = 2f;

        private ApplicationManager _applicationManager;
        private XRRayInteractor _xrRayInteractor;
        private XRInteractorLineVisual _lineVisual;
        private float _maxLineLength;

        private void OnEnable()
        {
            ResolveRayComponents();
            Application.onBeforeRender += UpdateRayLineLength;

            _applicationManager = ApplicationManager.Instance;
            if (_applicationManager == null)
            {
                return;
            }

            _applicationManager.OnStateChanged += HandleApplicationStateChanged;
            HandleApplicationStateChanged(_applicationManager.State);
        }

        private void OnDisable()
        {
            Application.onBeforeRender -= UpdateRayLineLength;

            if (_applicationManager != null)
                _applicationManager.OnStateChanged -= HandleApplicationStateChanged;
            if (_lineVisual != null) _lineVisual.enabled = false;
            _applicationManager = null;
        }

        private void HandleApplicationStateChanged(ApplicationState state)
        {
            if (_rayInteractor == null) return;

            bool shouldEnable = ShouldEnableRayInteractor(state);
            if (_rayInteractor.activeSelf != shouldEnable)
                _rayInteractor.SetActive(shouldEnable);
        }

        private void ResolveRayComponents()
        {
            if (_rayInteractor == null) return;

            _xrRayInteractor = _rayInteractor.GetComponent<XRRayInteractor>();
            _lineVisual = _rayInteractor.GetComponent<XRInteractorLineVisual>();
            if (_objectInteractor == null)
                _objectInteractor = GetComponentInChildren<LeftHandObjectInteractor>(true);
            if (_xrRayInteractor == null || _lineVisual == null)
            {
                return;
            }

            _maxLineLength = _lineVisual.lineLength;
            _lineVisual.autoAdjustLineLength = false;
            _lineVisual.enabled = false;
        }

        [BeforeRenderOrder(XRInteractionUpdateOrder.k_BeforeRenderLineVisual - 1)]
        private void UpdateRayLineLength()
        {
            if (_xrRayInteractor == null
                || _lineVisual == null
                || !_xrRayInteractor.isActiveAndEnabled)
                return;

            bool hasUiHit = _xrRayInteractor.TryGetCurrentUIRaycastResult(out var uiHit)
                            && uiHit.gameObject != null;
            bool hasObjectHit = _objectInteractor != null
                                && _objectInteractor.HasHoveredInteractable;
            bool shouldShowLine = hasUiHit || hasObjectHit;
            if (_lineVisual.enabled != shouldShowLine)
                _lineVisual.enabled = shouldShowLine;
            if (!shouldShowLine) return;

            float lineLength = _maxLineLength;
            if (_applicationManager != null && (_applicationManager.IsFactoryResponding || _applicationManager.IsEmergencyContacting))
                lineLength = Mathf.Min(lineLength, _factoryInteractionLineLength);
            if (hasObjectHit)
                lineLength = Mathf.Min(lineLength, _objectInteractor.HoveredDistance);
            if (hasUiHit)
                lineLength = Mathf.Min(
                    lineLength,
                    Mathf.Clamp(uiHit.distance, 0f, _maxLineLength));

            _lineVisual.lineLength = lineLength;
        }

        private static bool ShouldEnableRayInteractor(ApplicationState state)
        {
            // The left-hand ray remains available while fighting so the player can
            // select or swap the physical extinguisher at any time.
            return state != ApplicationState.Escape;
        }
    }
}
