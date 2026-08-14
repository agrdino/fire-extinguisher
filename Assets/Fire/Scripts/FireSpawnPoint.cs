using UnityEngine;

namespace _Scripts.Fires
{
    [DisallowMultipleComponent]
    public sealed class FireSpawnPoint : MonoBehaviour
    {
        [SerializeField] private FireType _fireType = FireType.Solid;

        public FireType FireType => _fireType;

#if UNITY_EDITOR
        private void OnDrawGizmos()
        {
            Gizmos.color = _fireType == FireType.Solid
                ? new Color(1f, 0.45f, 0.1f, 0.9f)
                : new Color(0.2f, 0.65f, 1f, 0.9f);
            Gizmos.DrawWireSphere(transform.position, 0.2f);
            Gizmos.DrawLine(transform.position, transform.position + transform.forward * 0.4f);
        }
#endif
    }
}
