using UnityEngine;
namespace _Scripts.FireExtinguishers
{
    [DisallowMultipleComponent]
    public sealed class FireExtinguisherModelSwitcher : MonoBehaviour
    {
        [SerializeField] private FireExtinguisher _fireExtinguisher;
        [SerializeField] private Transform _unselectModel;
        [SerializeField] private Transform _co2Model;
        [SerializeField] private Transform _powderModel;

        private void OnEnable()
        {
            if (_fireExtinguisher == null) return;

            _fireExtinguisher.OnTypeChanged += UpdateModel;
            UpdateModel(_fireExtinguisher.ExtinguisherType);
        }

        private void OnDisable()
        {
            if (_fireExtinguisher != null)
                _fireExtinguisher.OnTypeChanged -= UpdateModel;
        }

        private void UpdateModel(FireExtinguisherType extinguisherType)
        {
            SetActive(_unselectModel, extinguisherType == FireExtinguisherType.Unselect);
            SetActive(_co2Model, extinguisherType == FireExtinguisherType.CO2);
            SetActive(_powderModel, extinguisherType == FireExtinguisherType.Powder);
        }

        private static void SetActive(Transform model, bool isActive)
        {
            if (model != null && model.gameObject.activeSelf != isActive)
                model.gameObject.SetActive(isActive);
        }
    }
}
