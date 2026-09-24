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
        [SerializeField] private VisibilityFogController _fogVisibilityController;
        [SerializeField] private Transform _effectRoot;
        [SerializeField] private float _fixedWorldHeight = 2f;

        private void Reset()
        {
            _applicationManager = GetComponentInParent<ApplicationManager>();
            _fireController = GetComponentInParent<FireController>();
            _transition = GetComponentInChildren<ParticleSystemBlendTransition>(true);
            _fogVisibilityController = GetComponentInParent<VisibilityFogController>();
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
                _fogVisibilityController?.SetSmokeActive(false, 0f);
            }
        }

        private void OnDisable()
        {
            if (_applicationManager != null)
                _applicationManager.OnStateChanged -= HandleStateChanged;

            _transition?.ClearImmediately();
            _fogVisibilityController?.SetSmokeActive(false, 0f);
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
            if (_fogVisibilityController == null)
                _fogVisibilityController = GetComponentInParent<VisibilityFogController>();
        }

        private void HandleStateChanged(ApplicationState state)
        {
            if (_transition == null)
                return;

            if (state == ApplicationState.Completed || state == ApplicationState.Escaped)
            {
                _transition.FadeOutAndClear();
                _fogVisibilityController?.SetSmokeActive(false, _transition.FadeOutDuration);
                return;
            }

            if (state == ApplicationState.Failed)
                return;

            if (ShouldShowSmoke(state))
            {
                PlaceAbovePlayerView();
                _transition.FadeInFromClear();
                _fogVisibilityController?.SetSmokeActive(true, _transition.FadeInDuration);
                return;
            }

            _transition.ClearImmediately();
            _fogVisibilityController?.SetSmokeActive(false, 0f);
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
            position.y = _fixedWorldHeight;
            _effectRoot.SetPositionAndRotation(position, Quaternion.identity);
        }
    }
}
