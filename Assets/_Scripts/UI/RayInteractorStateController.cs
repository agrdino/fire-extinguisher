using _Scripts.Controller;
using UnityEngine;

namespace _Scripts.UI
{
    [DisallowMultipleComponent]
    public sealed class RayInteractorStateController : MonoBehaviour
    {
        [SerializeField] private GameObject _rayInteractor;

        private ApplicationManager _applicationManager;

        private void OnEnable()
        {
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
            if (_applicationManager != null)
                _applicationManager.OnStateChanged -= HandleApplicationStateChanged;
            _applicationManager = null;
        }

        private void HandleApplicationStateChanged(ApplicationState state)
        {
            if (_rayInteractor == null) return;

            bool shouldEnable = state != ApplicationState.Playing && state != ApplicationState.Escape;
            if (_rayInteractor.activeSelf != shouldEnable)
                _rayInteractor.SetActive(shouldEnable);
        }
    }
}
