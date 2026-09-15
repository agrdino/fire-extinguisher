using UnityEngine;
using UnityEngine.SceneManagement;

namespace _Scripts.ParticleSystemLerps
{
    [DefaultExecutionOrder(-90)]
    [DisallowMultipleComponent]
    public sealed class VisibilityFogController : MonoBehaviour
    {
        [Header("Appearance")]
        [SerializeField] private Color _fogColor = new Color(0.5f, 0.5f, 0.5f, 1f);

        [Header("Default Visibility")]
        [SerializeField, Min(0f)] private float _defaultFogStart = 50f;
        [SerializeField, Min(0f)] private float _defaultFogEnd = 100f;

        [Header("Smoke Visibility")]
        [SerializeField, Min(0f)] private float _smokeFogStart = 20f;
        [SerializeField, Min(0f)] private float _smokeFogEnd = 30f;

        private float _transitionStartDistance;
        private float _transitionEndDistance;
        private float _targetStartDistance;
        private float _targetEndDistance;
        private float _transitionDuration;
        private float _transitionElapsed;
        private bool _isTransitioning;
        private bool _isSmokeActive;

        public bool IsSmokeActive => _isSmokeActive;

        private void OnEnable()
        {
            SceneManager.sceneLoaded += HandleSceneLoaded;
            ApplyTargetImmediately();
        }

        private void OnDisable()
        {
            SceneManager.sceneLoaded -= HandleSceneLoaded;
            _isTransitioning = false;
        }

        private void Update()
        {
            if (!_isTransitioning)
                return;

            _transitionElapsed += Time.deltaTime;
            float progress = _transitionDuration <= 0f
                ? 1f
                : Mathf.Clamp01(_transitionElapsed / _transitionDuration);

            RenderSettings.fogStartDistance = Mathf.Lerp(
                _transitionStartDistance,
                _targetStartDistance,
                progress);
            RenderSettings.fogEndDistance = Mathf.Lerp(
                _transitionEndDistance,
                _targetEndDistance,
                progress);

            if (progress >= 1f)
                _isTransitioning = false;
        }

        private void OnValidate()
        {
            _defaultFogEnd = Mathf.Max(_defaultFogStart, _defaultFogEnd);
            _smokeFogEnd = Mathf.Max(_smokeFogStart, _smokeFogEnd);
        }

        public void SetSmokeActive(bool isActive, float transitionDuration)
        {
            if (_isSmokeActive == isActive && !_isTransitioning)
                return;

            _isSmokeActive = isActive;
            BeginTransition(transitionDuration);
        }

        private void HandleSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            ApplyTargetImmediately();
        }

        private void BeginTransition(float transitionDuration)
        {
            ConfigureFog();
            ResolveTargetDistances();

            _transitionStartDistance = RenderSettings.fogStartDistance;
            _transitionEndDistance = RenderSettings.fogEndDistance;
            _transitionDuration = Mathf.Max(0f, transitionDuration);
            _transitionElapsed = 0f;
            _isTransitioning = _transitionDuration > 0f;

            if (!_isTransitioning)
                ApplyDistances(_targetStartDistance, _targetEndDistance);
        }

        private void ApplyTargetImmediately()
        {
            ConfigureFog();
            ResolveTargetDistances();
            ApplyDistances(_targetStartDistance, _targetEndDistance);
            _isTransitioning = false;
        }

        private void ResolveTargetDistances()
        {
            _targetStartDistance = _isSmokeActive ? _smokeFogStart : _defaultFogStart;
            _targetEndDistance = _isSmokeActive ? _smokeFogEnd : _defaultFogEnd;
        }

        private void ConfigureFog()
        {
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.Linear;
            RenderSettings.fogColor = _fogColor;
        }

        private static void ApplyDistances(float startDistance, float endDistance)
        {
            RenderSettings.fogStartDistance = startDistance;
            RenderSettings.fogEndDistance = endDistance;
        }
    }
}
