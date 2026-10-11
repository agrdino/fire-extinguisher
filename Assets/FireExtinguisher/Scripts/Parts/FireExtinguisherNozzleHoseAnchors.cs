using UnityEngine;

namespace _Scripts.FireExtinguishers.Visualizes
{
    [DisallowMultipleComponent]
    public sealed class FireExtinguisherNozzleHoseAnchors : MonoBehaviour
    {
        [SerializeField] private Transform _unselectAnchor;
        [SerializeField] private Transform _co2Anchor;
        [SerializeField] private Transform _powderAnchor;

        public Transform GetAnchor(FireExtinguisherType extinguisherType)
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
