using System.Collections.Generic;
using _Scripts.UI;
using UnityEngine;
using UnityEngine.Localization;

namespace _Scripts.Fires
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(SphereCollider))]
    public sealed class FireProximityWarning : MonoBehaviour
    {
        [SerializeField] private SphereCollider _trigger;
        [SerializeField] private LocalizedString _localizedWarningMessage = new("UI", "warning.safe_distance");
        [SerializeField, Min(1f)] private float _flareUpRadiusMultiplier = 2f;

        private readonly HashSet<Collider> _playerCollidersInside = new();
        private Fire _fire;
        private Transform _playerRoot;
        private float _baseWarningRadius;

        private void Reset()
        {
            _trigger = GetComponent<SphereCollider>();
            _trigger.isTrigger = true;
            _trigger.center = new Vector3(0f, 0.8f, 0f);
            _trigger.radius = 3f;
        }

        private void Awake()
        {
            if (_trigger == null) _trigger = GetComponent<SphereCollider>();
            _trigger.isTrigger = true;
            _baseWarningRadius = _trigger.radius;
            _fire = GetComponentInParent<Fire>();
        }

        private void Update()
        {
            if (_trigger == null) return;

            bool usesExpandedRadius = _fire != null
                && (_fire.IsFlaringUp || _fire.IntensityRatio > 1f);
            float multiplier = usesExpandedRadius ? Mathf.Max(1f, _flareUpRadiusMultiplier) : 1f;
            float warningRadius = _baseWarningRadius * multiplier;
            if (!Mathf.Approximately(_trigger.radius, warningRadius))
                _trigger.radius = warningRadius;
        }

        private void OnDisable()
        {
            IdleHintController.Instance?.HidePersistentMessage(this);
            _playerCollidersInside.Clear();
        }

        public void Arm(Transform playerRoot)
        {
            IdleHintController.Instance?.HidePersistentMessage(this);
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
                // The circular anchor is only geometry for the curve endpoint. Center it on
                // the fire itself, not on the elevated proximity-warning trigger.
                Transform warningTarget = _fire != null ? _fire.transform : transform;
                hintController.ShowPersistentMessage(this, _localizedWarningMessage, warningTarget);
                return;
            }

            Debug.LogWarning(_localizedWarningMessage.GetLocalizedString(), this);
        }

        private void OnTriggerExit(Collider other)
        {
            if (other != null) _playerCollidersInside.Remove(other);
            if (_playerCollidersInside.Count == 0)
                IdleHintController.Instance?.HidePersistentMessage(this);
        }

        private bool BelongsToPlayer(Collider other)
        {
            if (_playerRoot == null || other == null) return false;

            Transform otherTransform = other.transform;
            return otherTransform == _playerRoot || otherTransform.IsChildOf(_playerRoot);
        }
    }
}
