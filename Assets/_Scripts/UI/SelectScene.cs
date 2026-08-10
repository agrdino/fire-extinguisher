using System.Collections.Generic;
using _Scripts.Data;
using _Scripts.FireExtinguishers;
using UnityEngine;
using UnityEngine.UI;

namespace _Scripts.UI
{
    public class SelectScene : MonoBehaviour, IScene
    {
        [SerializeField] private Button _btnSelect;
        [SerializeField] private List<FireExtinguisher> _fireExtinguishers;

        private void Start()
        {
            FireExtinguisher.onSelect += OnSelect;
            _btnSelect.onClick.AddListener(OnClickSelectButton);
        }
        
        public void Show()
        {
            for (var i = 0; i < _fireExtinguishers.Count; i++)
            {
                _fireExtinguishers[i].ShowFireExtinguisher(FireExtinguisherConfigs.Instance.GetFireExtinguisherConfig(i + 1));
            }
        }

        public void Hide()
        {
        }
        
        private void OnSelect(int id)
        {
            // FireExtinguisherController.Instance.Show(id);
        }

        private void OnClickSelectButton()
        {
            UIController.Instance.ShowScene(UIController.EScene.FightingScene);
        }
    }
}