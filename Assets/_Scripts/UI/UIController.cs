using System;
using AYellowpaper;
using UnityEngine;
using UnityEngine.Serialization;
using _Scripts.Controller;
using _Scripts.Fires;
using _Scripts.Environments.Factory;
using _Scripts.Environments.EmergencyContact;
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
        [SerializeField] private FactoryResponseScene _factoryResponseScene;
        [SerializeField] private ContactEmergencyScene _contactEmergencyScene;
        [SerializeField] private InterfaceReference<IScene, MonoBehaviour> _selectExtinguisherScene;
        [SerializeField] private InterfaceReference<IScene, MonoBehaviour> _fightingScene;
        [SerializeField] private InterfaceReference<IScene, MonoBehaviour> _escapeScene;
        [SerializeField] private InterfaceReference<IScene, MonoBehaviour> _completedScene;
        [SerializeField] private InterfaceReference<IScene, MonoBehaviour> _escapedScene;
        [SerializeField] private InterfaceReference<IScene, MonoBehaviour> _failedScene;

        [Header("Failed UI Placement")]
        [SerializeField, Min(0f)] private float _failedSceneDistance = 2.5f;
        [SerializeField] private float _failedSceneHeight = 1.4f;

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
            if (_escapedScene.Value is EscapedScene escapedScene)
                escapedScene.Initialize(navigator);
            if (_failedScene.Value is FailedScene failedScene)
                failedScene.Initialize(navigator);
        }

        public void PrepareForSceneTransition()
        {
            if (_currentScene == null) return;

            _currentScene.Hide();
            HideSceneObject(_currentScene);
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
                HideSceneObject(_currentScene);
            }

            _currentScene = state switch
            {
                ApplicationState.Ready => _readyView,
                ApplicationState.Language => _selectLanguageScene,
                ApplicationState.SelectEnvironment => _selectEnvironmentScene,
                ApplicationState.Guide => _guideScene,
                ApplicationState.Explore => _exploreScene,
                ApplicationState.FactoryResponse => _factoryResponseScene,
                ApplicationState.SelectExtinguisher => _selectExtinguisherScene.Value,
                ApplicationState.Fighting => _fightingScene.Value,
                ApplicationState.ContactEmergencyTeam => _contactEmergencyScene,
                ApplicationState.Escape => _escapeScene.Value,
                ApplicationState.Completed => _completedScene.Value,
                ApplicationState.Escaped => _escapedScene.Value,
                ApplicationState.Failed => _failedScene.Value,
                _ => throw new ArgumentOutOfRangeException(nameof(state), state, null)
            };

            if (!PlaceScene(_currentScene, state)) return;

            _currentScene.gameObject.SetActive(true);
            if (_currentScene.gameObject.TryGetComponent(out UIPanelTween transition))
                transition.PlayIn();
            _currentScene.Show();
        }

        private static void HideSceneObject(IScene scene)
        {
            if (scene.gameObject.TryGetComponent(out UIPanelTween transition))
                transition.PlayOutAndDeactivate();
            else
                scene.gameObject.SetActive(false);
        }

        private bool PlaceScene(IScene scene, ApplicationState state)
        {
            if (_environmentContext == null)
            {
                scene.gameObject.SetActive(false);
                return false;
            }

            if (state == ApplicationState.Failed) return PlaceFailedScene(scene);

            FireSpawnPoint selectedFireSpawnPoint = FireController.Instance?.SelectedSpawnPoint;
            FactoryEmergencyResponseController factoryResponseController = FindFirstObjectByType<FactoryEmergencyResponseController>();
            EmergencyContactController emergencyContactController = FindFirstObjectByType<EmergencyContactController>();
            Transform fireUIAnchor = state switch
            {
                ApplicationState.FactoryResponse when factoryResponseController?.CurrentStep == FactoryEmergencyResponseStep.ActivateFireAlarm => selectedFireSpawnPoint?.FireAlarmUIPoint,
                ApplicationState.FactoryResponse => selectedFireSpawnPoint?.CircuitBreakerUIPoint,
                ApplicationState.SelectExtinguisher => selectedFireSpawnPoint?.SelectExtinguisherUIPoint,
                ApplicationState.Fighting => selectedFireSpawnPoint?.FightingUIPoint,
                ApplicationState.ContactEmergencyTeam => emergencyContactController?.UIAnchor,
                ApplicationState.Escape => selectedFireSpawnPoint?.EscapeUIPoint,
                _ => null
            };

            if (fireUIAnchor != null)
            {
                scene.gameObject.transform.SetPositionAndRotation(
                    fireUIAnchor.position,
                    fireUIAnchor.rotation);
                return true;
            }

            if ((state == ApplicationState.Completed || state == ApplicationState.Escaped)
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
                scene.gameObject.SetActive(false);
                return false;
            }

            scene.gameObject.transform.SetPositionAndRotation(point.position, point.rotation);
            return true;
        }

        public void RefreshCurrentScenePlacement()
        {
            if (_currentScene != null) PlaceScene(_currentScene, _applicationManager.State);
        }

        private bool PlaceFailedScene(IScene scene)
        {
            Transform playerView = _applicationManager.PlayerView;
            if (playerView == null)
            {
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
            _factoryResponseScene ??= GetComponentInChildren<FactoryResponseScene>(true);
            _contactEmergencyScene ??= GetComponentInChildren<ContactEmergencyScene>(true);
        }
    }
}
