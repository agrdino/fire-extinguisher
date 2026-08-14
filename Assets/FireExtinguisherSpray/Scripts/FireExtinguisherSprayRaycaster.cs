using System.Collections.Generic;
using _Scripts.Fires;
using UnityEngine;

namespace _Scripts.FireExtinguishers
{
    [DisallowMultipleComponent]
    public sealed class FireExtinguisherSprayRaycaster : MonoBehaviour
    {
        private const float MinimumIntensityMultiplierAtSphereEdge = 0.5f;

        [SerializeField] private FireExtinguisher _fireExtinguisher;
        [SerializeField] private Transform _rayOrigin;
        [SerializeField, Min(0f)] private float _range = 5f;
        [SerializeField, Min(0f)] private float _intensityReductionPerSecond = 20f;
        [SerializeField] private LayerMask _fireLayers = ~0;

        private readonly Dictionary<Fire, float> _hitFireIntensityMultipliers = new();
        private readonly HashSet<Fire> _incompatibleFiresLastFrame = new();
        private readonly HashSet<Fire> _incompatibleFiresThisFrame = new();
        private readonly RaycastHit[] _hits = new RaycastHit[8];

        private Transform RayOrigin => _rayOrigin != null ? _rayOrigin : transform;

        private void Reset()
        {
            _fireExtinguisher = GetComponentInParent<FireExtinguisher>();
            _rayOrigin = transform;
        }

        private void Update()
        {
            if (_fireExtinguisher == null || !_fireExtinguisher.CanSpray || _range <= 0f)
            {
                ClearIncompatibleFireTracking();
                return;
            }

            int count = Physics.SphereCastNonAlloc(
                RayOrigin.position,
                _fireExtinguisher.SprayRadius,
                RayOrigin.forward,
                _hits,
                _range,
                _fireLayers,
                QueryTriggerInteraction.Collide);

            _hitFireIntensityMultipliers.Clear();
            _incompatibleFiresThisFrame.Clear();
            for (int i = 0; i < count; i++)
            {
                RaycastHit hit = _hits[i];
                Fire fire = hit.collider.GetComponentInParent<Fire>();
                if (fire == null) continue;

                float intensityMultiplier = GetIntensityMultiplier(hit.collider);
                if (_hitFireIntensityMultipliers.TryGetValue(fire, out float highestMultiplier) && highestMultiplier >= intensityMultiplier)
                    continue;

                _hitFireIntensityMultipliers[fire] = intensityMultiplier;
            }

            foreach (KeyValuePair<Fire, float> hitFire in _hitFireIntensityMultipliers)
            {
                Fire fire = hitFire.Key;

                if (!_fireExtinguisher.CanExtinguish(fire.FireType))
                {
                    _incompatibleFiresThisFrame.Add(fire);
                    if (!_incompatibleFiresLastFrame.Contains(fire))
                        _fireExtinguisher.NotifyIncompatibleFireTargeted(fire.FireType);
                    continue;
                }

                if (_intensityReductionPerSecond <= 0f) continue;
                float intensityReduction = _intensityReductionPerSecond * hitFire.Value * Time.deltaTime;
                fire.ReduceIntensity(intensityReduction);
            }

            _incompatibleFiresLastFrame.Clear();
            foreach (Fire fire in _incompatibleFiresThisFrame)
                _incompatibleFiresLastFrame.Add(fire);
        }

        private float GetIntensityMultiplier(Collider fireCollider)
        {
            float radius = _fireExtinguisher.SprayRadius;
            if (radius <= 0f) return 1f;

            Transform rayOrigin = RayOrigin;
            Vector3 toFire = fireCollider.bounds.center - rayOrigin.position;
            float distanceAlongSpray = Mathf.Clamp(Vector3.Dot(toFire, rayOrigin.forward), 0f, _range);
            Vector3 sprayPosition = rayOrigin.position + rayOrigin.forward * distanceAlongSpray;
            Vector3 closestFirePosition = fireCollider.ClosestPoint(sprayPosition);
            float distanceRatio = Mathf.Clamp01(Vector3.Distance(sprayPosition, closestFirePosition) / radius);

            return Mathf.Lerp(1f, MinimumIntensityMultiplierAtSphereEdge, distanceRatio);
        }

        private void ClearIncompatibleFireTracking()
        {
            _incompatibleFiresLastFrame.Clear();
            _incompatibleFiresThisFrame.Clear();
        }

        private void OnDrawGizmosSelected()
        {
            Transform rayOrigin = RayOrigin;
            Vector3 start = rayOrigin.position;
            Vector3 end = start + rayOrigin.forward * _range;
            float radius = _fireExtinguisher != null ? _fireExtinguisher.SprayRadius : 0f;

            Gizmos.color = Color.cyan;
            Gizmos.DrawLine(start, end);
            Gizmos.DrawWireSphere(start, radius);
            Gizmos.DrawWireSphere(end, radius);

            Vector3 rightOffset = rayOrigin.right * radius;
            Vector3 upOffset = rayOrigin.up * radius;
            Gizmos.DrawLine(start + rightOffset, end + rightOffset);
            Gizmos.DrawLine(start - rightOffset, end - rightOffset);
            Gizmos.DrawLine(start + upOffset, end + upOffset);
            Gizmos.DrawLine(start - upOffset, end - upOffset);
        }

    }
}
