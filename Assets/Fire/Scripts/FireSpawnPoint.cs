using UnityEngine;

namespace _Scripts.Fires
{
    [DisallowMultipleComponent]
    public sealed class FireSpawnPoint : MonoBehaviour
    {
        [SerializeField] private FireType _fireType = FireType.Solid;
        [SerializeField] private Transform _selectExtinguisherUIPoint;

        public FireType FireType => _fireType;
        public Transform SelectExtinguisherUIPoint => _selectExtinguisherUIPoint;

#if UNITY_EDITOR
        private void OnDrawGizmos()
        {
            Gizmos.color = _fireType == FireType.Solid
                ? new Color(1f, 0.45f, 0.1f, 0.9f)
                : new Color(0.2f, 0.65f, 1f, 0.9f);
            Gizmos.DrawWireSphere(transform.position, 0.2f);
            Gizmos.DrawLine(transform.position, transform.position + transform.forward * 0.4f);

            if (_selectExtinguisherUIPoint == null) return;
            Gizmos.color = new Color(0.2f, 0.75f, 1f, 0.9f);
            Gizmos.DrawLine(transform.position, _selectExtinguisherUIPoint.position);
            Gizmos.DrawWireSphere(_selectExtinguisherUIPoint.position, 0.15f);
        }
#endif
    }
}
