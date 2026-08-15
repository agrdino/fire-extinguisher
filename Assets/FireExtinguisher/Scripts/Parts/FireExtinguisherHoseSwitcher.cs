using UnityEngine;

namespace _Scripts.FireExtinguishers.Visualizes
{
    [DisallowMultipleComponent]
    public sealed class FireExtinguisherHoseSwitcher : MonoBehaviour
    {
        [SerializeField] private FireExtinguisher _fireExtinguisher;
        [SerializeField] private FireExtinguisherHose _hose;

        [Header("Start Anchors")]
        [SerializeField] private Transform _unselectAnchor;
        [SerializeField] private Transform _co2Anchor;
        [SerializeField] private Transform _powderAnchor;

        [Header("Materials")]
        [SerializeField] private Material _selectedMaterial;
        [SerializeField] private Material _unselectMaterial;

        private void OnEnable()
        {
            if (_fireExtinguisher == null || _hose == null) return;

            _fireExtinguisher.OnTypeChanged += UpdateHose;
            UpdateHose(_fireExtinguisher.ExtinguisherType);
        }

        private void OnDisable()
        {
            if (_fireExtinguisher != null)
                _fireExtinguisher.OnTypeChanged -= UpdateHose;
        }

        private void UpdateHose(FireExtinguisherType extinguisherType)
        {
            _hose.SetStartAnchor(GetAnchor(extinguisherType));
            _hose.SetMaterial(extinguisherType == FireExtinguisherType.Unselect
                ? _unselectMaterial
                : _selectedMaterial);
        }

        private Transform GetAnchor(FireExtinguisherType extinguisherType)
        {
            return extinguisherType switch
            {
                FireExtinguisherType.CO2 => _co2Anchor,
                FireExtinguisherType.Powder => _powderAnchor,
                _ => _unselectAnchor
            };
        }
    }
}
