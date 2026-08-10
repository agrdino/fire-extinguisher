using System;
using UnityEngine;

namespace _Scripts.UI
{
    public class UIController : MonoBehaviour
    {
        private static UIController _instance;
        public static UIController Instance => _instance;
        
        [SerializeField] private StartScene _startScene;
        [SerializeField] private SelectScene _selectScene;
        [SerializeField] private FightingScene _fightingScene;
        [SerializeField] private EscapeScene _escapeScene;
        [SerializeField] private CompleteScene _completeScene;
        [SerializeField] private LoadingScene _loadingScene;
        
        private void Awake()
        {
            _instance = this;
        }
    }
}