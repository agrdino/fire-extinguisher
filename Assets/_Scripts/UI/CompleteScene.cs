using System;
using UnityEngine;
using UnityEngine.UI;

namespace _Scripts.UI
{
    public class CompleteScene : MonoBehaviour,IScene
    {
        [SerializeField] private Button _btnBack;

        private void Awake()
        {
            _btnBack.onClick.AddListener(OnClickBackButton);
        }

        public void Show()
        {
            
        }

        public void Hide()
        {
            
        }

        private void OnClickBackButton()
        {
            UIController.Instance.ShowScene(UIController.EScene.StartScene);
        }
    }
}