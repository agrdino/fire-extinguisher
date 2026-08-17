using System.Collections.Generic;
using _Scripts.UI;
using UnityEngine;

namespace _Scripts.Fires
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(SphereCollider))]
    public sealed class FireProximityWarning : MonoBehaviour
    {
        [SerializeField] private SphereCollider _trigger;
        [SerializeField] private string _warningMessage = "Warning! Keep a safe distance from the fire.";
        [SerializeField, Min(0f)] private float _messageDuration = 2.5f;

        private readonly HashSet<Collider> _playerCollidersInside = new();
        private Transform _playerRoot;

        private void Reset()
        {
            _trigger = GetComponent<SphereCollider>();
            _trigger.isTrigger = true;
            _trigger.center = new Vector3(0f, 0.8f, 0f);
            _trigger.radius = 1.5f;
        }

        private void Awake()
        {
            if (_trigger == null) _trigger = GetComponent<SphereCollider>();
            _trigger.isTrigger = true;
        }

        private void OnDisable()
        {
            _playerCollidersInside.Clear();
        }

        public void Arm(Transform playerRoot)
        {
            _playerRoot = playerRoot;
            _playerCollidersInside.Clear();
        }

        private void OnTriggerEnter(Collider other)
        {
            if (!BelongsToPlayer(other) || !_playerCollidersInside.Add(other)) return;
            if (_playerCollidersInside.Count > 1) return;

            IdleHintController hintController = IdleHintController.Instance;
            if (hintController != null)
            {
                Transform warningTarget = transform.parent != null ? transform.parent : transform;
                hintController.ShowTemporaryMessage(_warningMessage, warningTarget, _messageDuration);
                return;
            }

            Debug.LogWarning(_warningMessage, this);
        }

        private void OnTriggerExit(Collider other)
        {
            if (other != null) _playerCollidersInside.Remove(other);
        }

        private bool BelongsToPlayer(Collider other)
        {
            if (_playerRoot == null || other == null) return false;

            Transform otherTransform = other.transform;
            return otherTransform == _playerRoot || otherTransform.IsChildOf(_playerRoot);
        }
    }
}
