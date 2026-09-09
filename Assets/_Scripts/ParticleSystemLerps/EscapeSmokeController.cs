using _Scripts.Controller;
using _Scripts.Fires;
using UnityEngine;

namespace _Scripts.ParticleSystemLerps
{
    [DefaultExecutionOrder(10)]
    [DisallowMultipleComponent]
    public sealed class EscapeSmokeController : MonoBehaviour
    {
        [SerializeField] private ApplicationManager _applicationManager;
        [SerializeField] private FireController _fireController;
        [SerializeField] private ParticleSystemBlendTransition _transition;
        [SerializeField] private Transform _effectRoot;
        [SerializeField, Min(0f)] private float _heightAbovePlayerView = 0.5f;

        private void Reset()
        {
            _applicationManager = GetComponentInParent<ApplicationManager>();
            _fireController = GetComponentInParent<FireController>();
            _transition = GetComponentInChildren<ParticleSystemBlendTransition>(true);
            _effectRoot = _transition != null ? _transition.transform : null;
        }

        private void OnEnable()
        {
            ResolveReferences();

            if (_applicationManager != null)
            {
                _applicationManager.OnStateChanged += HandleStateChanged;
                HandleStateChanged(_applicationManager.State);
            }
            else
            {
                _transition?.ClearImmediately();
            }
        }

        private void OnDisable()
        {
            if (_applicationManager != null)
                _applicationManager.OnStateChanged -= HandleStateChanged;

            _transition?.ClearImmediately();
        }

        private void LateUpdate()
        {
            if (_transition != null && _transition.IsVisible)
                PlaceAbovePlayerView();
        }

        private void ResolveReferences()
        {
            if (_applicationManager == null)
                _applicationManager = ApplicationManager.Instance;
            if (_fireController == null)
                _fireController = FireController.Instance;
        }

        private void HandleStateChanged(ApplicationState state)
        {
            if (_transition == null)
                return;

            if (state == ApplicationState.Completed)
            {
                _transition.FadeOutAndClear();
                return;
            }

            if (state == ApplicationState.Failed)
                return;

            if (ShouldShowSmoke(state))
            {
                PlaceAbovePlayerView();
                _transition.FadeInFromClear();
                return;
            }

            _transition.ClearImmediately();
        }

        private bool ShouldShowSmoke(ApplicationState state)
        {
            return state == ApplicationState.Escape
                && _applicationManager.CurrentEnvironment?.EscapeSmokeEnabled == true
                && _fireController != null
                && _fireController.HasBurningFires;
        }

        private void PlaceAbovePlayerView()
        {
            if (_effectRoot == null || _applicationManager.PlayerView == null)
                return;

            Vector3 position = _applicationManager.PlayerView.position;
            position.y += _heightAbovePlayerView;
            _effectRoot.SetPositionAndRotation(position, Quaternion.identity);
        }
    }
}
