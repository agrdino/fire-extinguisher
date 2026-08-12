using _Scripts.FireExtinguishers;
using UnityEngine;
using UnityEngine.UI;

namespace _Scripts.UI
{
    public class FightingScene : MonoBehaviour, IScene
    {
        [SerializeField] private Slider _sldFireExtinguisher;
        
        private void OnEnable()
        {
            if (_sldFireExtinguisher != null)
                _sldFireExtinguisher.value = FireExtinguisherController.Instance.FireExtinguisher.RemainingRatio;
            FireExtinguisherController.Instance.FireExtinguisher.OnRemainingAmountChanged += OnValueChanged;
        }

        public void Show()
        {
        }

        public void Hide()
        {
        }

        private void OnDisable()
        {
            if (FireExtinguisherController.Instance == null) return;
            FireExtinguisherController.Instance.FireExtinguisher.OnRemainingAmountChanged -= OnValueChanged;
        }

        private void OnValueChanged(float amount, float ratio)
        {
            if (_sldFireExtinguisher != null) _sldFireExtinguisher.value = ratio;
        }
    }
}
