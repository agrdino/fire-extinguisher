using System.Collections.Generic;
using _Scripts.Fires;
using UnityEngine;
using UnityEngine.Serialization;

namespace _Scripts.FireExtinguishers
{
    [DisallowMultipleComponent]
    public sealed class FireExtinguisherSprayRaycaster : MonoBehaviour
    {
        private const float MinimumIntensityMultiplierAtSphereEdge = 0.5f;
        private const int InitialHitCapacity = 16;
        private const int MaximumHitCapacity = 256;

        [SerializeField] private FireExtinguisher _fireExtinguisher;
        [SerializeField] private Transform _rayOrigin;
        [SerializeField, Min(0f)] private float _range = 5f;
        [SerializeField, Min(0f)] private float _intensityReductionPerSecond = 20f;
        [FormerlySerializedAs("_fireLayers")]
        [SerializeField] private LayerMask _collisionLayers = ~0;

        private readonly Dictionary<Fire, float> _hitFireIntensityMultipliers = new();
        private readonly HashSet<SprayPassThrough> _appliedPassThroughs = new();
        private readonly HashSet<Fire> _incompatibleFiresLastFrame = new();
        private readonly HashSet<Fire> _incompatibleFiresThisFrame = new();
        private RaycastHit[] _hits = new RaycastHit[InitialHitCapacity];

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

            int count = CollectHits();
            SortHitsByDistance(count);

            _hitFireIntensityMultipliers.Clear();
            _appliedPassThroughs.Clear();
            _incompatibleFiresThisFrame.Clear();
            float obstructionMultiplier = 1f;
            for (int i = 0; i < count; i++)
            {
                RaycastHit hit = _hits[i];
                if (hit.collider == null || IsExtinguisherCollider(hit.collider)) continue;

                Fire fire = hit.collider.GetComponentInParent<Fire>();
                SprayPassThrough passThrough = hit.collider.GetComponent<SprayPassThrough>();
                if (fire == null)
                {
                    if (passThrough == null) break;

                    ApplyPassThroughMultiplier(passThrough, ref obstructionMultiplier);
                    continue;
                }

                float intensityMultiplier = GetIntensityMultiplier(hit.collider) * obstructionMultiplier;
                if (_hitFireIntensityMultipliers.TryGetValue(fire, out float highestMultiplier) && highestMultiplier >= intensityMultiplier)
                {
                    if (passThrough == null) break;

                    ApplyPassThroughMultiplier(passThrough, ref obstructionMultiplier);
                    continue;
                }

                _hitFireIntensityMultipliers[fire] = intensityMultiplier;

                // The target fire receives the spray before its own collider can obstruct it.
                // A pass-through on the fire collider only affects targets farther down the spray path.
                if (passThrough == null) break;
                ApplyPassThroughMultiplier(passThrough, ref obstructionMultiplier);
            }

            foreach (KeyValuePair<Fire, float> hitFire in _hitFireIntensityMultipliers)
            {
                Fire fire = hitFire.Key;

                if (!_fireExtinguisher.CanExtinguish(fire.FireType))
                {
                    _incompatibleFiresThisFrame.Add(fire);
                    if (_intensityReductionPerSecond > 0f)
                    {
                        float intensityIncrease = _intensityReductionPerSecond * hitFire.Value * Time.deltaTime;
                        fire.IncreaseFromIncompatibleExtinguisher(intensityIncrease);
                    }
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

        private int CollectHits()
        {
            Transform rayOrigin = RayOrigin;
            while (true)
            {
                int count = Physics.SphereCastNonAlloc(
                    rayOrigin.position,
                    _fireExtinguisher.SprayRadius,
                    rayOrigin.forward,
                    _hits,
                    _range,
                    _collisionLayers,
                    QueryTriggerInteraction.Collide);

                if (count < _hits.Length || _hits.Length >= MaximumHitCapacity) return count;

                int newCapacity = Mathf.Min(_hits.Length * 2, MaximumHitCapacity);
                System.Array.Resize(ref _hits, newCapacity);
            }
        }

        private void SortHitsByDistance(int count)
        {
            // NonAlloc physics queries do not guarantee hit order. An insertion sort is cheap for
            // the small, reusable buffer and avoids allocating a comparer every spray frame.
            for (int i = 1; i < count; i++)
            {
                RaycastHit hit = _hits[i];
                int insertionIndex = i - 1;
                while (insertionIndex >= 0 && _hits[insertionIndex].distance > hit.distance)
                {
                    _hits[insertionIndex + 1] = _hits[insertionIndex];
                    insertionIndex--;
                }

                _hits[insertionIndex + 1] = hit;
            }
        }

        private bool IsExtinguisherCollider(Collider collider)
        {
            return _fireExtinguisher != null
                   && collider.transform.IsChildOf(_fireExtinguisher.transform);
        }

        private void ApplyPassThroughMultiplier(SprayPassThrough passThrough, ref float multiplier)
        {
            // One component can govern multiple colliders on the same GameObject. Apply it once.
            if (_appliedPassThroughs.Add(passThrough)) multiplier *= passThrough.EffectivenessMultiplier;
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
