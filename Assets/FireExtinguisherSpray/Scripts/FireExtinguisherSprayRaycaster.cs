using System.Collections.Generic;
using _Scripts.Fires;
using UnityEngine;

namespace _Scripts.FireExtinguishers
{
    [DisallowMultipleComponent]
    public sealed class FireExtinguisherSprayRaycaster : MonoBehaviour
    {
        [SerializeField] private FireExtinguisher _fireExtinguisher;
        [SerializeField] private Transform _rayOrigin;
        [SerializeField, Min(0f)] private float _range = 5f;
        [SerializeField, Min(0f)] private float _intensityReductionPerSecond = 20f;
        [SerializeField] private LayerMask _fireLayers = ~0;

        private readonly HashSet<Fire> _hitFires = new();
        private readonly RaycastHit[] _hits = new RaycastHit[8];

        private Transform RayOrigin => _rayOrigin != null ? _rayOrigin : transform;

        private void Reset()
        {
            _fireExtinguisher = GetComponentInParent<FireExtinguisher>();
            _rayOrigin = transform;
        }

        private void Update()
        {
            if (_fireExtinguisher == null) return;
            if (!_fireExtinguisher.CanSpray) return;
            if (_range <= 0f || _intensityReductionPerSecond <= 0f) return;

            int count = Physics.RaycastNonAlloc(RayOrigin.position, RayOrigin.forward, _hits, _range, _fireLayers, QueryTriggerInteraction.Collide);
            if (count == 0) return;

            _hitFires.Clear();
            float intensityReduction = _intensityReductionPerSecond * Time.deltaTime;
            for (int i = 0; i < count; i++)
            {
                RaycastHit hit = _hits[i];
                Fire fire = hit.collider.GetComponentInParent<Fire>();
                if (fire == null || !_hitFires.Add(fire)) continue;

                fire.ReduceIntensity(intensityReduction);
            }
        }

        private void OnDrawGizmosSelected()
        {
            Transform rayOrigin = RayOrigin;
            Gizmos.color = Color.cyan;
            Gizmos.DrawLine(rayOrigin.position, rayOrigin.position + rayOrigin.forward * _range);
        }

    }
}
