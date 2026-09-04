using System;
using AYellowpaper;
using UnityEngine;
using UnityEngine.Serialization;
using _Scripts.Controller;
using _Scripts.SceneManagement;

namespace _Scripts.UI
{
    public class UIController : MonoBehaviour
    {
        private static UIController _instance;
        public static UIController Instance => _instance;
        
        [SerializeField] private SelectLanguageScene _selectLanguageScene;
        [SerializeField] private SelectEnvironmentScene _selectEnvironmentScene;
        [FormerlySerializedAs("_startScene")]
        [SerializeField] private ReadyView _readyView;
        [SerializeField] private GuideScene _guideScene;
        [SerializeField] private ExploreScene _exploreScene;
        [SerializeField] private InterfaceReference<IScene, MonoBehaviour> _selectExtinguisherScene;
        [SerializeField] private InterfaceReference<IScene, MonoBehaviour> _fightingScene;
        [SerializeField] private InterfaceReference<IScene, MonoBehaviour> _escapeScene;
        [SerializeField] private InterfaceReference<IScene, MonoBehaviour> _completedScene;
        [SerializeField] private InterfaceReference<IScene, MonoBehaviour> _failedScene;

        [Header("Failed UI Placement")]
        [SerializeField, Min(0f)] private float _failedSceneDistance = 2.5f;
        [SerializeField] private float _failedSceneHeight = 1.55f;

        private IScene _currentScene;
        private ApplicationManager _applicationManager;
        private IEnvironmentSceneContext _environmentContext;
        private EmergencyExitPlacementController _exitPlacementController;

        public IScene CurrentScene => _currentScene;
        
        private void Awake()
        {
            _instance = this;
            _applicationManager = ApplicationManager.Instance;
            ResolveSceneReferences();

            if (_applicationManager == null)
            {
                Debug.LogError("UIController requires an ApplicationManager.", this);
                return;
            }

            _applicationManager.OnStateChanged += OnApplicationStateChanged;
        }

        private void OnDestroy()
        {
            if (_applicationManager != null)
                _applicationManager.OnStateChanged -= OnApplicationStateChanged;
            if (_instance == this) _instance = null;
        }

        public void InitializeNavigation(IApplicationNavigator navigator)
        {
            _selectEnvironmentScene.Initialize(navigator);
            _readyView.Initialize(navigator);
            if (_completedScene.Value is CompletedScene completedScene)
                completedScene.Initialize(navigator);
            if (_failedScene.Value is FailedScene failedScene)
                failedScene.Initialize(navigator);
        }

        public void PrepareForSceneTransition()
        {
            if (_currentScene == null) return;

            _currentScene.Hide();
            _currentScene.gameObject.SetActive(false);
        }

public void BindEnvironment(
            IEnvironmentSceneContext environmentContext,
            EmergencyExitPlacementController exitPlacementController)
        {
            _environmentContext = environmentContext;
            _exitPlacementController = exitPlacementController;
        }

        private void OnApplicationStateChanged(ApplicationState state)
        {
            if (_currentScene != null)
            {
                _currentScene.Hide();
                _currentScene.gameObject.SetActive(false);
            }
            
            _currentScene = state switch
            {
                ApplicationState.Ready => _readyView,
                ApplicationState.Language => _selectLanguageScene,
                ApplicationState.SelectEnvironment => _selectEnvironmentScene,
                ApplicationState.Guide => _guideScene,
                ApplicationState.Explore => _exploreScene,
                ApplicationState.SelectExtinguisher => _selectExtinguisherScene.Value,
                ApplicationState.Fighting => _fightingScene.Value,
                ApplicationState.Escape => _escapeScene.Value,
                ApplicationState.Completed => _completedScene.Value,
                ApplicationState.Failed => _failedScene.Value,
                _ => throw new ArgumentOutOfRangeException(nameof(state), state, null)
            };

            if (!PlaceScene(_currentScene, state)) return;

            _currentScene.gameObject.SetActive(true);
            _currentScene.Show();
        }

        private bool PlaceScene(IScene scene, ApplicationState state)
        {
            if (_environmentContext == null)
            {
                Debug.LogError("Cannot place UI before an EnvironmentSceneContext is bound.", this);
                scene.gameObject.SetActive(false);
                return false;
            }

            if (state == ApplicationState.Failed) return PlaceFailedScene(scene);

            if (state == ApplicationState.Completed
                && _exitPlacementController?.SelectedSpawnPoint?.CompleteUIPoint != null)
            {
                Transform completeAnchor = _exitPlacementController.SelectedSpawnPoint.CompleteUIPoint;
                scene.gameObject.transform.SetPositionAndRotation(
                    completeAnchor.position,
                    completeAnchor.rotation);
                return true;
            }

            ApplicationState placementState = state == ApplicationState.Ready
                ? ApplicationState.Guide
                : state;

            if (!_environmentContext.TryGetUIAnchor(placementState, out Transform point))
            {
                Debug.LogError(
                    $"Missing UI anchor for {_environmentContext.SceneId}/{placementState}.",
                    this);
                scene.gameObject.SetActive(false);
                return false;
            }

            scene.gameObject.transform.SetPositionAndRotation(point.position, point.rotation);
            return true;
        }

        private bool PlaceFailedScene(IScene scene)
        {
            Transform playerView = _applicationManager.PlayerView;
            if (playerView == null)
            {
                Debug.LogError("Cannot place Failed UI because the player view is missing.", this);
                return false;
            }

            Quaternion yawRotation = Quaternion.Euler(0f, playerView.eulerAngles.y, 0f);
            Vector3 position = playerView.position + yawRotation * Vector3.forward * _failedSceneDistance;
            position.y = _failedSceneHeight;
            scene.gameObject.transform.SetPositionAndRotation(position, yawRotation);
            return true;
        }

        private void ResolveSceneReferences()
        {
            _selectLanguageScene ??= GetComponentInChildren<SelectLanguageScene>(true);
            _selectEnvironmentScene ??= GetComponentInChildren<SelectEnvironmentScene>(true);
            _readyView ??= GetComponentInChildren<ReadyView>(true);
            _guideScene ??= GetComponentInChildren<GuideScene>(true);
            _exploreScene ??= GetComponentInChildren<ExploreScene>(true);
        }
    }
}
