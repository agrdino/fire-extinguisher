using System;
using UnityEngine;

namespace _Scripts.Controller
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(BoxCollider))]
    public sealed class EmergencyExit : MonoBehaviour
    {
        [SerializeField] private BoxCollider _trigger;

        private Transform _playerRoot;
        private bool _isArmed;

        public event Action OnPlayerReached;

        public bool IsArmed => _isArmed;
        public Transform HintTarget
        {
            get
            {
                Transform view = transform.Find("View");
                return view != null ? view : transform;
            }
        }

        private void Reset()
        {
            _trigger = GetComponent<BoxCollider>();
            _trigger.isTrigger = true;
        }

        private void Awake()
        {
            if (_trigger == null) _trigger = GetComponent<BoxCollider>();
            _trigger.isTrigger = true;
        }

        public void Arm(Transform playerRoot)
        {
            _playerRoot = playerRoot;
            _isArmed = _playerRoot != null;
        }

        public void Disarm()
        {
            _isArmed = false;
            _playerRoot = null;
        }

        private void OnTriggerEnter(Collider other)
        {
            if (!_isArmed || other == null) return;
            Transform otherTransform = other.transform;
            if (otherTransform != _playerRoot && !otherTransform.IsChildOf(_playerRoot)) return;

            _isArmed = false;
            OnPlayerReached?.Invoke();
        }
    }
}
