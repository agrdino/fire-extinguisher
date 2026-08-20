using UnityEngine;

namespace _Scripts.Fires.Visualizes
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(ParticleSystemRenderer))]
    public sealed class YAxisCameraBillboard : MonoBehaviour
    {
        [Tooltip("Optional target. When empty, the Main Camera is used.")]
        [SerializeField] private Transform _target;

        [Tooltip("Use this when the front of the particle texture is not +Z.")]
        [SerializeField] private float _yawOffset;

        private Transform _mainCameraTransform;

        private void Awake()
        {
            ConfigureRenderer();
        }

        private void OnValidate()
        {
            ConfigureRenderer();
        }

        private void LateUpdate()
        {
            Transform target = GetTarget();
            if (target == null) return;

            Vector3 direction = target.position - transform.position;
            direction.y = 0f;

            if (direction.sqrMagnitude < 0.000001f) return;

            Quaternion faceTarget = Quaternion.LookRotation(direction, Vector3.up);
            transform.rotation = faceTarget * Quaternion.Euler(0f, _yawOffset, 0f);
        }

        private Transform GetTarget()
        {
            if (_target != null) return _target;
            if (_mainCameraTransform != null) return _mainCameraTransform;

            Camera mainCamera = Camera.main;
            if (mainCamera != null) _mainCameraTransform = mainCamera.transform;

            return _mainCameraTransform;
        }

        private void ConfigureRenderer()
        {
            if (!TryGetComponent(out ParticleSystemRenderer particleRenderer)) return;

            particleRenderer.renderMode = ParticleSystemRenderMode.Billboard;
            particleRenderer.alignment = ParticleSystemRenderSpace.Local;
        }
    }
}
