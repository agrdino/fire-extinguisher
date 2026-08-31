using UnityEngine;

namespace _Scripts.Controller
{
    [DisallowMultipleComponent]
    public sealed class EmergencyExitSpawnPoint : MonoBehaviour
    {
        [SerializeField] private Transform _completeUIPoint;

        public Transform CompleteUIPoint => _completeUIPoint;

#if UNITY_EDITOR
        private void OnDrawGizmos()
        {
            Gizmos.color = new Color(0.15f, 1f, 0.35f, 0.9f);
            Gizmos.DrawWireCube(transform.position + Vector3.up * 0.8f, new Vector3(0.8f, 1.6f, 0.2f));
            Gizmos.DrawLine(transform.position, transform.position + transform.forward * 0.6f);

            if (_completeUIPoint == null) return;
            Gizmos.color = new Color(0.2f, 0.75f, 1f, 0.9f);
            Gizmos.DrawLine(transform.position + Vector3.up, _completeUIPoint.position);
            Gizmos.DrawWireSphere(_completeUIPoint.position, 0.15f);
        }
#endif
    }
}
