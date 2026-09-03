using _Scripts.SceneManagement;
using _Scripts.UI;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace _Scripts.Controller
{
    [DefaultExecutionOrder(50)]
    [DisallowMultipleComponent]
    public sealed class ApplicationFlowController : MonoBehaviour, IApplicationNavigator
    {
        [SerializeField] private SceneFlowController _sceneFlowController;
        [SerializeField] private ApplicationManager _applicationManager;
        [SerializeField] private UIController _uiController;
        [SerializeField] private EmergencyExitPlacementController _exitPlacementController;

        private SceneId? _pendingSceneId;
        private ApplicationState? _pendingEntryState;
        private EnvironmentSceneContext _currentEnvironment;
        private bool _isInitialized;

        public bool IsTransitioning => _sceneFlowController != null
            && _sceneFlowController.IsTransitioning;

        private void Awake()
        {
            if (!ValidateRuntimeReferences()) return;

            _uiController.InitializeNavigation(this);
            _sceneFlowController.SceneLoadStarted += HandleSceneLoadStarted;
            _sceneFlowController.SceneLoadFailed += HandleSceneLoadFailed;
            SceneManager.sceneLoaded += HandleUnitySceneLoaded;
            _isInitialized = true;
        }

        private void Start()
        {
            if (_isInitialized)
                BindScene(SceneManager.GetActiveScene());
        }

        private void OnDestroy()
        {
            if (!_isInitialized) return;

            _sceneFlowController.SceneLoadStarted -= HandleSceneLoadStarted;
            _sceneFlowController.SceneLoadFailed -= HandleSceneLoadFailed;
            SceneManager.sceneLoaded -= HandleUnitySceneLoaded;
        }

        public bool TryEnterEnvironment(SceneId environmentScene)
        {
            if (environmentScene == SceneId.Start)
            {
                Debug.LogError("Start is not a gameplay environment scene.", this);
                return false;
            }

            return TryNavigate(environmentScene, ApplicationState.Ready);
        }

        public bool TryReturnToEnvironmentSelection()
        {
            if (_currentEnvironment != null && _currentEnvironment.SceneId == SceneId.Start)
            {
                _applicationManager.SetState(ApplicationState.SelectEnvironment);
                return true;
            }

            return TryNavigate(SceneId.Start, ApplicationState.SelectEnvironment);
        }

        private bool TryNavigate(SceneId sceneId, ApplicationState entryState)
        {
            if (!_isInitialized || IsTransitioning) return false;

            if (_currentEnvironment != null && _currentEnvironment.SceneId == sceneId)
            {
                _applicationManager.SetState(entryState);
                return true;
            }

            _pendingSceneId = sceneId;
            _pendingEntryState = entryState;
            if (_sceneFlowController.TryLoad(sceneId)) return true;

            ClearPendingNavigation();
            return false;
        }

        private void HandleSceneLoadStarted(SceneId _)
        {
            _uiController.PrepareForSceneTransition();
            _applicationManager.PrepareForSceneTransition();
        }

        private void HandleSceneLoadFailed(SceneId _, string __)
        {
            ClearPendingNavigation();
        }

        private void HandleUnitySceneLoaded(Scene scene, LoadSceneMode _)
        {
            BindScene(scene);
        }

        private void BindScene(Scene scene)
        {
            if (!TryGetEnvironmentContext(scene, out EnvironmentSceneContext environment)) return;
            if (_currentEnvironment == environment && !_pendingSceneId.HasValue) return;

            ApplicationState entryState = environment.DefaultEntryState;
            if (_pendingSceneId.HasValue)
            {
                if (_pendingSceneId.Value == environment.SceneId && _pendingEntryState.HasValue)
                    entryState = _pendingEntryState.Value;
                else
                    Debug.LogError(
                        $"Loaded scene context {environment.SceneId} does not match requested scene {_pendingSceneId.Value}.",
                        environment);
            }

            ClearPendingNavigation();
            _currentEnvironment = environment;
            _uiController.BindEnvironment(environment, _exitPlacementController);
            _applicationManager.BindEnvironment(environment, entryState);
        }

        private bool TryGetEnvironmentContext(
            Scene scene,
            out EnvironmentSceneContext environment)
        {
            environment = null;
            foreach (GameObject root in scene.GetRootGameObjects())
            {
                EnvironmentSceneContext[] contexts =
                    root.GetComponentsInChildren<EnvironmentSceneContext>(true);
                foreach (EnvironmentSceneContext candidate in contexts)
                {
                    if (environment != null)
                    {
                        Debug.LogError(
                            $"Scene {scene.path} contains more than one EnvironmentSceneContext.",
                            candidate);
                        return false;
                    }

                    environment = candidate;
                }
            }

            if (environment == null)
            {
                Debug.LogError($"Scene {scene.path} has no EnvironmentSceneContext.", this);
                return false;
            }

            if (environment.ValidateConfiguration(out string error)) return true;

            Debug.LogError(error, environment);
            environment = null;
            return false;
        }

        private void ClearPendingNavigation()
        {
            _pendingSceneId = null;
            _pendingEntryState = null;
        }

        private bool ValidateRuntimeReferences()
        {
            if (_sceneFlowController == null)
                Debug.LogError("ApplicationFlowController requires a SceneFlowController.", this);
            if (_applicationManager == null)
                Debug.LogError("ApplicationFlowController requires an ApplicationManager.", this);
            if (_uiController == null)
                Debug.LogError("ApplicationFlowController requires a UIController.", this);
            if (_exitPlacementController == null)
                Debug.LogError("ApplicationFlowController requires an EmergencyExitPlacementController.", this);

            return _sceneFlowController != null
                && _applicationManager != null
                && _uiController != null
                && _exitPlacementController != null;
        }
    }
}
