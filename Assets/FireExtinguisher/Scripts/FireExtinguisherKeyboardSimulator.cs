using UnityEngine;
using UnityEngine.InputSystem;

namespace _Scripts.FireExtinguishers
{
    [DisallowMultipleComponent]
    public sealed class FireExtinguisherKeyboardSimulator : MonoBehaviour
    {
        [SerializeField] private FireExtinguisherController _controller;

        [SerializeField] private bool _simulateKeyboardHeartbeat = true;
        [SerializeField, Min(0.02f)] private float _heartbeatInterval = 2f;
        [SerializeField] private Key _lastSimulatedKey = Key.None;

        private float _nextHeartbeatTime = float.PositiveInfinity;

        public Key LastSimulatedKey => _lastSimulatedKey;

        private void Start()
        {
            if (_controller == null) _controller = FireExtinguisherController.Instance;
            if (_controller == null) _controller = FindFirstObjectByType<FireExtinguisherController>();
            if (_controller == null)
            {
                enabled = false;
                return;
            }

            SO_FireExtinguisherInputSettings inputSettings = _controller.InputSettings;
            if (inputSettings == null)
            {
                enabled = false;
                return;
            }

            bool isSent = SendKey(inputSettings.DefaultKey);
            if (!isSent)
            {
                enabled = false;
                return;
            }
        }

        private void Update()
        {
            if (_controller == null) return;
            if (!_simulateKeyboardHeartbeat) return;
            if (Time.unscaledTime < _nextHeartbeatTime) return;

            Key heartbeatKey = _controller.HasReceivedKey ? _controller.LastReceivedKey : _lastSimulatedKey;
            SendKey(heartbeatKey);
        }

        private bool SendKey(Key key)
        {
            if (!_controller.TryReceiveKey(key)) return false;

            _lastSimulatedKey = key;
            _nextHeartbeatTime = Time.unscaledTime + Mathf.Max(0.01f, _heartbeatInterval);
            return true;
        }
    }
}
