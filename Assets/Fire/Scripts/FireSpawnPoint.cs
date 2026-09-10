using UnityEngine;

namespace _Scripts.Fires
{
    [DisallowMultipleComponent]
    public sealed class FireSpawnPoint : MonoBehaviour
    {
        [SerializeField] private FireType _fireType = FireType.Solid;
        [SerializeField] private Transform _selectExtinguisherUIPoint;
        [SerializeField] private Transform _fightingUIPoint;
        [SerializeField] private Transform _escapeUIPoint;

        public FireType FireType => _fireType;
        public Transform SelectExtinguisherUIPoint => _selectExtinguisherUIPoint;
        public Transform FightingUIPoint => _fightingUIPoint;
        public Transform EscapeUIPoint => _escapeUIPoint;

#if UNITY_EDITOR
        private void OnDrawGizmos()
        {
            Gizmos.color = _fireType == FireType.Solid
                ? new Color(1f, 0.45f, 0.1f, 0.9f)
                : new Color(0.2f, 0.65f, 1f, 0.9f);
            Gizmos.DrawWireSphere(transform.position, 0.2f);
            Gizmos.DrawLine(transform.position, transform.position + transform.forward * 0.4f);

            DrawUIPointGizmo(_selectExtinguisherUIPoint, new Color(0.2f, 0.75f, 1f, 0.9f));
            DrawUIPointGizmo(_fightingUIPoint, new Color(1f, 0.8f, 0.1f, 0.9f));
            DrawUIPointGizmo(_escapeUIPoint, new Color(0.35f, 1f, 0.35f, 0.9f));
        }

        private void DrawUIPointGizmo(Transform point, Color color)
        {
            if (point == null) return;

            Gizmos.color = color;
            Gizmos.DrawLine(transform.position, point.position);
            Gizmos.DrawWireSphere(point.position, 0.15f);
        }
#endif
    }
}
