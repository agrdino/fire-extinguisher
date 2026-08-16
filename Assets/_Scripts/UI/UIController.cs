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
        [SerializeField] private GuideScene _guideScene;
        [SerializeField] private ExploreScene _exploreScene;
        [SerializeField] private InterfaceReference<IScene, MonoBehaviour> _selectScene;
        [SerializeField] private InterfaceReference<IScene, MonoBehaviour> _fightingScene;
        [SerializeField] private InterfaceReference<IScene, MonoBehaviour> _escapeScene;
        [SerializeField] private InterfaceReference<IScene, MonoBehaviour> _completeScene;
        [SerializeField] private InterfaceReference<IScene, MonoBehaviour> _failedScene;
        [SerializeField] private InterfaceReference<IScene, MonoBehaviour> _loadingScene;
        [SerializeField] private IdleHintController _idleHintPrefab;
        
        private IScene _currentScene;
        private IdleHintController _idleHintInstance;
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

            if (_idleHintPrefab != null)
            {
                _idleHintInstance = Instantiate(_idleHintPrefab);
                _idleHintInstance.name = "Idle Hint System";
            }
            else
            {
                Debug.LogError("Assign the Idle Hint Popup prefab on UIController.", this);
            }
        }

        private void OnDestroy()
        {
            if (_applicationManager != null)
                _applicationManager.OnStateChanged -= OnApplicationStateChanged;
            if (_enviromentController != null)
                _enviromentController.OnEnviromentChanged -= OnEnviromentChanged;
            if (_idleHintInstance != null)
                Destroy(_idleHintInstance.gameObject);
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
            EnviromentType enviromentType = state == ApplicationState.Start
                ? EnviromentType.Start
                : _enviromentController.CurrentEnviroment;

            if (!_enviromentController.TryGetUIPoint(enviromentType, state, out Transform point))
            {
                Debug.LogError($"Missing UI point for {enviromentType}/{state}. Assign it on EnviromentController.", this);
                scene.gameObject.SetActive(false);
                return false;
            }

            scene.gameObject.transform.SetPositionAndRotation(point.position, point.rotation);
            return true;
        }
    }
}
