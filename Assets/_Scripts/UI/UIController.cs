using System;
using AYellowpaper;
using UnityEngine;
using _Scripts.Controller;

namespace _Scripts.UI
{
    public class UIController : MonoBehaviour
    {
        private static UIController _instance;
        public static UIController Instance => _instance;
        
        [SerializeField] private InterfaceReference<IScene, MonoBehaviour> _startScene;
        [SerializeField] private LanguageScene _languageScene;
        [SerializeField] private GuideScene _guideScene;
        [SerializeField] private ExploreScene _exploreScene;
        [SerializeField] private InterfaceReference<IScene, MonoBehaviour> _selectScene;
        [SerializeField] private InterfaceReference<IScene, MonoBehaviour> _fightingScene;
        [SerializeField] private InterfaceReference<IScene, MonoBehaviour> _escapeScene;
        [SerializeField] private InterfaceReference<IScene, MonoBehaviour> _completeScene;
        [SerializeField] private InterfaceReference<IScene, MonoBehaviour> _failedScene;
        [SerializeField] private InterfaceReference<IScene, MonoBehaviour> _loadingScene;

        [Header("Failed UI Placement")]
        [SerializeField, Min(0f)] private float _failedSceneDistance = 2.5f;
        [SerializeField] private float _failedSceneHeight = 1.55f;
        
        private IScene _currentScene;
        private ApplicationManager _applicationManager;
        private EnviromentController _enviromentController;
        private ApplicationState _currentState;
        private bool _hasCurrentState;

        public IScene CurrentScene => _currentScene;
        
        private void Awake()
        {
            _instance = this;
            _applicationManager = ApplicationManager.Instance;
            _enviromentController = EnviromentController.Instance;
            _applicationManager.OnStateChanged += OnApplicationStateChanged;
            _enviromentController.OnEnviromentChanged += OnEnviromentChanged;

        }

        private void OnDestroy()
        {
            if (_applicationManager != null)
                _applicationManager.OnStateChanged -= OnApplicationStateChanged;
            if (_enviromentController != null)
                _enviromentController.OnEnviromentChanged -= OnEnviromentChanged;
            if (_instance == this) _instance = null;
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
                ApplicationState.Start => _startScene.Value,
                ApplicationState.Language => _languageScene,
                ApplicationState.Guide => _guideScene,
                ApplicationState.Explore => _exploreScene,
                ApplicationState.Selecting => _selectScene.Value,
                ApplicationState.Playing => _fightingScene.Value,
                ApplicationState.Escape => _escapeScene.Value,
                ApplicationState.Won => _completeScene.Value,
                ApplicationState.Lost => _failedScene.Value,
                _ => throw new ArgumentOutOfRangeException(nameof(state), state, null)
            };
            
            _currentState = state;
            _hasCurrentState = true;

            if (!PlaceScene(_currentScene, state)) return;

            _currentScene.gameObject.SetActive(true);
            _currentScene.Show();
        }

        private void OnEnviromentChanged(EnviromentType enviromentType)
        {
            if (!_hasCurrentState || _currentScene == null) return;
            if (_currentState == ApplicationState.Start || enviromentType == EnviromentType.Start) return;

            PlaceScene(_currentScene, _currentState);
        }

        private bool PlaceScene(IScene scene, ApplicationState state)
        {
            if (state == ApplicationState.Lost) return PlaceFailedScene(scene);

            bool isStartFlow = state == ApplicationState.Language || state == ApplicationState.Start;
            EnviromentType enviromentType = isStartFlow
                ? EnviromentType.Start
                : _enviromentController.CurrentEnviroment;

            ApplicationState placementState = state == ApplicationState.Language
                ? ApplicationState.Start
                : state;

            if (!_enviromentController.TryGetUIPoint(enviromentType, placementState, out Transform point))
            {
                Debug.LogError($"Missing UI point for {enviromentType}/{placementState}. Assign it on EnviromentController.", this);
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

    }
}
