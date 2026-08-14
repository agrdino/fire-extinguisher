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
        [SerializeField] private InterfaceReference<IScene, MonoBehaviour> _selectScene;
        [SerializeField] private InterfaceReference<IScene, MonoBehaviour> _fightingScene;
        [SerializeField] private InterfaceReference<IScene, MonoBehaviour> _escapeScene;
        [SerializeField] private InterfaceReference<IScene, MonoBehaviour> _completeScene;
        [SerializeField] private InterfaceReference<IScene, MonoBehaviour> _failedScene;
        [SerializeField] private InterfaceReference<IScene, MonoBehaviour> _loadingScene;
        [SerializeField] private GameObject _rayInteractor;
        
        private IScene _currentScene;
        private ApplicationManager _applicationManager;
        
        private void Awake()
        {
            _instance = this;
            _applicationManager = ApplicationManager.Instance;
            _applicationManager.OnStateChanged += OnApplicationStateChanged;
        }

        private void OnDestroy()
        {
            _applicationManager.OnStateChanged -= OnApplicationStateChanged;
            if (_instance == this) _instance = null;
        }

        private void OnApplicationStateChanged(ApplicationState state)
        {
            if (_rayInteractor != null)
                _rayInteractor.SetActive(state != ApplicationState.Playing && state != ApplicationState.Escape);

            if (_currentScene != null)
            {
                _currentScene.Hide();
                _currentScene.gameObject.SetActive(false);
            }
            
            _currentScene = state switch
            {
                ApplicationState.Start => _startScene.Value,
                ApplicationState.Selecting => _selectScene.Value,
                ApplicationState.Playing => _fightingScene.Value,
                ApplicationState.Escape => _escapeScene.Value,
                ApplicationState.Won => _completeScene.Value,
                ApplicationState.Lost => _failedScene.Value,
                _ => throw new ArgumentOutOfRangeException(nameof(state), state, null)
            };
            
            _currentScene.gameObject.SetActive(true);
            _currentScene.Show();
        }
    }
}
