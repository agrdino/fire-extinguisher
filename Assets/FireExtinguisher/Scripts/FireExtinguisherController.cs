using UnityEngine;
using UnityEngine.InputSystem;

namespace _Scripts.FireExtinguishers
{
    [DisallowMultipleComponent]
    public sealed class FireExtinguisherController : MonoBehaviour
    {
        private static FireExtinguisherController _instance;

        public static FireExtinguisherController Instance
        {
            get => _instance;
            private set => _instance = value;
        }

        [SerializeField] private FireExtinguisher _fireExtinguisher;
        [SerializeField] private SO_FireExtinguisherInputSettings _inputSettings;

        [SerializeField, Min(0.02f)] private float _connectionTimeout = 3f;

        [SerializeField] private bool _logReceivedStates = true;

        [SerializeField] private FireExtinguisherState _currentState;
        [SerializeField] private bool _isConnected;
        [SerializeField] private Key _lastReceivedKey = Key.None;
        [SerializeField] private bool _hasReceivedKey;

        private float _lastReceiveTime = float.NegativeInfinity;

        public FireExtinguisher FireExtinguisher => _fireExtinguisher;
        public SO_FireExtinguisherInputSettings InputSettings => _inputSettings;
        public FireExtinguisherState CurrentState => _currentState;
        public bool IsConnected => _isConnected;
        public Key LastReceivedKey => _lastReceivedKey;
        public bool HasReceivedKey => _hasReceivedKey;

        private void Reset()
        {
            _fireExtinguisher = GetComponent<FireExtinguisher>();
            if (_fireExtinguisher == null) _fireExtinguisher = FindFirstObjectByType<FireExtinguisher>();
        }

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Debug.LogError("Only one FireExtinguisherController may be active in a scene.", this);
                enabled = false;
                return;
            }

            Instance = this;

            if (_fireExtinguisher == null) _fireExtinguisher = GetComponent<FireExtinguisher>();
            if (_fireExtinguisher == null) _fireExtinguisher = FindFirstObjectByType<FireExtinguisher>();

            _currentState = _fireExtinguisher != null ? _fireExtinguisher.CurrentState : FireExtinguisherState.DefaultState;
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        private void Update()
        {
            UpdateConnectionTimeout();
        }

        public bool TryReceiveKey(Key key)
        {
            if (_inputSettings == null || !_inputSettings.TryGetState(key, out FireExtinguisherState state)) return false;
            
            _lastReceivedKey = key;
            _hasReceivedKey = true;
            ReceiveState(state);
            return true;
        }

        public void ReceiveState(FireExtinguisherState state)
        {
            _lastReceiveTime = Time.unscaledTime;
            _isConnected = true;
            _currentState = state;

            if (_fireExtinguisher != null)  _fireExtinguisher.SetState(state);
            if (_logReceivedStates) LogReceivedState(state);
        }

        public void ReceiveState(SafetyPinState safetyPin, LeverState lever)
        {
            ReceiveState(new FireExtinguisherState(safetyPin, lever));
        }

        private void UpdateConnectionTimeout()
        {
            if (!_isConnected) return;
            if (Time.unscaledTime - _lastReceiveTime < Mathf.Max(0.01f, _connectionTimeout)) return;
            
            _isConnected = false;
            _currentState = _currentState.WithLever(LeverState.Released);

            if (_fireExtinguisher != null) _fireExtinguisher.SetState(_currentState);

            Debug.LogWarning("Fire extinguisher connection timed out. Lever was released; safety pin state was preserved.", this);
        }

        private void LogReceivedState(FireExtinguisherState state)
        {
            if (state.SafetyPin == SafetyPinState.Inserted)
            {
                if (state.Lever == LeverState.Pressed) Debug.Log("Fire extinguisher received: Safety Pin Inserted, Lever Pressed.", this);
                else Debug.Log("Fire extinguisher received: Safety Pin Inserted, Lever Released.", this);
                return;
            }

            if (state.Lever == LeverState.Pressed) Debug.Log("Fire extinguisher received: Safety Pin Removed, Lever Pressed.", this);
            else Debug.Log("Fire extinguisher received: Safety Pin Removed, Lever Released.", this);
        }

    }
}
