using System;
using AYellowpaper;
using UnityEngine;

namespace _Scripts.UI
{
    public class UIController : MonoBehaviour
    {
        public enum EScene
        {
            StartScene,
            SelectScene,
            FightingScene,
            EscapeScene,
            CompleteScene,
            LoadingScene,
        }
        
        private static UIController _instance;
        public static UIController Instance => _instance;
        
        [SerializeField] private InterfaceReference<IScene, MonoBehaviour> _startScene;
        [SerializeField] private InterfaceReference<IScene, MonoBehaviour> _selectScene;
        [SerializeField] private InterfaceReference<IScene, MonoBehaviour> _fightingScene;
        [SerializeField] private InterfaceReference<IScene, MonoBehaviour> _escapeScene;
        [SerializeField] private InterfaceReference<IScene, MonoBehaviour> _completeScene;
        [SerializeField] private InterfaceReference<IScene, MonoBehaviour> _loadingScene;
        
        private IScene _currentScene;
        
        private void Awake()
        {
            _instance = this;
        }

        public void ShowScene(EScene scene)
        {
            if (_currentScene != null)
            {
                _currentScene.Hide();
                _currentScene.gameObject.SetActive(false);
            }
            _currentScene?.Hide();
            
            _currentScene = scene switch
            {
                EScene.StartScene => _startScene.Value,
                EScene.SelectScene => _selectScene.Value,
                EScene.FightingScene => _fightingScene.Value,
                EScene.EscapeScene => _escapeScene.Value,
                EScene.CompleteScene => _completeScene.Value,
                EScene.LoadingScene => _loadingScene.Value,
                _ => throw new ArgumentOutOfRangeException(nameof(scene), scene, null)
            };
            
            _currentScene.Show();
            _currentScene.gameObject.SetActive(false);
        }
    }
}