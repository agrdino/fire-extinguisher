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
                Debug.LogError("RayInteractorStateController requires an ApplicationManager in the scene.", this);
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
            if (_xrRayInteractor == null || _lineVisual == null)
            {
                Debug.LogError("RayInteractorStateController requires an XRRayInteractor and XRInteractorLineVisual.", this);
                return;
            }

            _maxLineLength = _lineVisual.lineLength;
            _lineVisual.autoAdjustLineLength = false;
        }

        [BeforeRenderOrder(XRInteractionUpdateOrder.k_BeforeRenderLineVisual - 1)]
        private void UpdateRayLineLength()
        {
            if (_xrRayInteractor == null
                || _lineVisual == null
                || !_xrRayInteractor.isActiveAndEnabled)
                return;

            float lineLength = _maxLineLength;
            if (_xrRayInteractor.TryGetCurrentUIRaycastResult(out var uiHit)
                && uiHit.gameObject != null)
            {
                lineLength = Mathf.Clamp(uiHit.distance, 0f, _maxLineLength);
            }

            _lineVisual.lineLength = lineLength;
        }

        private static bool ShouldEnableRayInteractor(ApplicationState state)
        {
            return state != ApplicationState.Fighting
                && state != ApplicationState.Escape;
        }
    }
}
