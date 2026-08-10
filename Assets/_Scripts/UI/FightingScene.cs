using _Scripts.Controller;
using _Scripts.FireExtinguishers;
using UnityEngine;
using UnityEngine.UI;

namespace _Scripts.UI
{
    public class FightingScene : MonoBehaviour, IScene
    {
        [SerializeField] private Slider _sldFireExtinguisher;
        
        private void Start()
        {
            _sldFireExtinguisher.value = 1;
            FireExtinguisherController.Instance.FireExtinguisher.OnCapacityChanged += OnValueChanged;
            
            MessageDispatcher.Register(MessageDispatcher.EMessageID.FireOff, Complete);
            MessageDispatcher.Register(MessageDispatcher.EMessageID.OutOfEnergy, Complete);
        }

        public void Show()
        {
        }

        public void Hide()
        {
        }

        private void OnValueChanged(float value)
        {
            _sldFireExtinguisher.value = value;
        }

        private void Complete()
        {
            UIController.Instance.ShowScene(UIController.EScene.EscapeScene);
        }
    }
}