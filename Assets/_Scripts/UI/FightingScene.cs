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
            FireExtinguisherController.Instance.FireExtinguisher.OnRemainingAmountChanged += OnValueChanged;
        }

        public void Show()
        {
        }

        public void Hide()
        {
        }

        private void OnDestroy()
        {
            FireExtinguisherController.Instance.FireExtinguisher.OnRemainingAmountChanged -= OnValueChanged;
        }

        private void OnValueChanged(float amount, float ratio)
        {
            _sldFireExtinguisher.value = ratio;
        }
    }
}
