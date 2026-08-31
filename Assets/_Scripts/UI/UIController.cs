using System;
using System.Collections;
using AYellowpaper;
using UnityEngine;
using _Scripts.Controller;

namespace _Scripts.UI
{
    public class UIController : MonoBehaviour
    {
        private static UIController _instance;
        public static UIController Instance => _instance;
        
        [SerializeField] private SelectLanguageScene _selectLanguageScene;
        [SerializeField] private SelectEnvironmentScene _selectEnvironmentScene;
        [SerializeField] private StartScene _startScene;
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

        [Header("Start Flow UI Placement")]
        [SerializeField, Min(0f)] private float _startFlowSceneDistance = 2.5f;
        [SerializeField] private float _startFlowVerticalOffset;
        [SerializeField, Min(1)] private int _startFlowPlacementFrames = 30;
        
        private IScene _currentScene;
        private ApplicationManager _applicationManager;
        private EnvironmentController _environmentController;
        private ApplicationState _currentState;
        private bool _hasCurrentState;
        private Coroutine _startFlowPlacementCoroutine;

        public IScene CurrentScene => _currentScene;
        
        private void Awake()
        {
            _instance = this;
            _applicationManager = ApplicationManager.Instance;
            _environmentController = EnvironmentController.Instance;
            ResolveSceneReferences();
            _applicationManager.OnStateChanged += OnApplicationStateChanged;
            _environmentController.OnEnvironmentChanged += OnEnvironmentChanged;

        }

        private void OnDestroy()
        {
            if (_applicationManager != null)
                _applicationManager.OnStateChanged -= OnApplicationStateChanged;
            if (_environmentController != null)
                _environmentController.OnEnvironmentChanged -= OnEnvironmentChanged;
            if (_instance == this) _instance = null;
        }

        private void OnApplicationStateChanged(ApplicationState state)
        {
            if (_startFlowPlacementCoroutine != null)
            {
                StopCoroutine(_startFlowPlacementCoroutine);
                _startFlowPlacementCoroutine = null;
            }

            if (_currentScene != null)
            {
                _currentScene.Hide();
                _currentScene.gameObject.SetActive(false);
            }
            
            _currentScene = state switch
            {
                ApplicationState.Start => _startScene,
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
            
            _currentState = state;
            _hasCurrentState = true;

            if (!PlaceScene(_currentScene, state)) return;

            _currentScene.gameObject.SetActive(true);
            _currentScene.Show();

            if (IsStartFlowState(state))
                _startFlowPlacementCoroutine = StartCoroutine(RefreshStartFlowPlacement(_currentScene, state));
        }

        private void OnEnvironmentChanged(EnvironmentType EnvironmentType)
        {
            if (!_hasCurrentState || _currentScene == null) return;
            if (_currentState == ApplicationState.Language
                || _currentState == ApplicationState.SelectEnvironment
                || EnvironmentType == EnvironmentType.Start)
                return;

            PlaceScene(_currentScene, _currentState);
        }

        private bool PlaceScene(IScene scene, ApplicationState state)
        {
            if (state == ApplicationState.Failed) return PlaceFailedScene(scene);

            if (IsStartFlowState(state)) return PlaceStartFlowScene(scene);

            EnvironmentType environmentType = _environmentController.CurrentEnvironment;

            if (!_environmentController.TryGetUIPoint(environmentType, state, out Transform point))
            {
                Debug.LogError($"Missing UI point for {environmentType}/{state}. Assign it on EnvironmentController.", this);
                scene.gameObject.SetActive(false);
                return false;
            }

            scene.gameObject.transform.SetPositionAndRotation(point.position, point.rotation);
            return true;
        }

        private static bool IsStartFlowState(ApplicationState state)
        {
            return state == ApplicationState.Language
                || state == ApplicationState.SelectEnvironment
                || state == ApplicationState.Start;
        }

        private bool PlaceStartFlowScene(IScene scene)
        {
            Transform playerView = _applicationManager.PlayerView;
            if (playerView == null)
            {
                Debug.LogError("Cannot place start flow UI because the player view is missing.", this);
                return false;
            }

            Vector3 position = playerView.position
                + playerView.forward * _startFlowSceneDistance
                + playerView.up * _startFlowVerticalOffset;
            scene.gameObject.transform.SetPositionAndRotation(position, playerView.rotation);
            return true;
        }

        private IEnumerator RefreshStartFlowPlacement(IScene scene, ApplicationState state)
        {
            // OpenXR can apply the tracked head pose a few frames after the initial
            // Language state. Re-sample briefly, then leave the UI fixed in world space.
            for (int frame = 0; frame < _startFlowPlacementFrames; frame++)
            {
                yield return null;
                if (!_hasCurrentState || _currentState != state || _currentScene != scene)
                    yield break;

                PlaceStartFlowScene(scene);
            }

            _startFlowPlacementCoroutine = null;
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
            _startScene ??= GetComponentInChildren<StartScene>(true);
            _guideScene ??= GetComponentInChildren<GuideScene>(true);
            _exploreScene ??= GetComponentInChildren<ExploreScene>(true);
        }

    }
}
