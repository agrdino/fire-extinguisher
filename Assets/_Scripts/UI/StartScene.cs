using System;
using _Scripts.FireExtinguishers;
using UnityEngine;
using UnityEngine.UI;

namespace _Scripts.UI
{
    public class StartScene : MonoBehaviour, IScene
    {
        [SerializeField] private Button _btnStart;

        private void Awake()
        {
            _btnStart.onClick.AddListener(OnClickStartButton);
        }

        public void Show()
        {
        }

        public void Hide()
        {
        }

        private void OnClickStartButton()
        {
            UIController.Instance.ShowScene(UIController.EScene.SelectScene);
        }
    }
}