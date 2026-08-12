using System;
using _Scripts.Fires;
using UnityEngine;
using UnityEngine.InputSystem;

namespace _Scripts.FireExtinguishers
{
    [DefaultExecutionOrder(-101)]
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
        [SerializeField] private bool _isInputEnabled;

        [SerializeField] private FireExtinguisherState _currentState;
        [SerializeField] private bool _isConnected;
        [SerializeField] private Key _lastReceivedKey = Key.None;
        [SerializeField] private bool _hasReceivedKey;

        private float _lastReceiveTime = float.NegativeInfinity;

        public FireExtinguisher FireExtinguisher => _fireExtinguisher;
        public bool IsDepleted => _fireExtinguisher.IsDepleted;

        public SO_FireExtinguisherInputSettings InputSettings => _inputSettings;
        public FireExtinguisherState CurrentState => _currentState;
        public bool IsConnected => _isConnected;
        public Key LastReceivedKey => _lastReceivedKey;
        public bool HasReceivedKey => _hasReceivedKey;
        public bool IsInputEnabled => _isInputEnabled;
        
        public event Action<float> OnCapacityChanged
        {
            add => _fireExtinguisher.OnCapacityChanged += value;
            remove => _fireExtinguisher.OnCapacityChanged -= value;
        }

        public event Action<FireExtinguisherType, FireType> OnIncompatibleFireTargeted
        {
            add => _fireExtinguisher.OnIncompatibleFireTargeted += value;
            remove => _fireExtinguisher.OnIncompatibleFireTargeted -= value;
        }

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;
            _currentState = _fireExtinguisher.CurrentState;
        }

        private void Update()
        {
            UpdateConnectionTimeout();
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        public bool TryReceiveKey(Key key)
        {
            if (!_inputSettings.TryGetState(key, out FireExtinguisherState state)) return false;
            if (!_isInputEnabled) return true;
            
            _lastReceivedKey = key;
            _hasReceivedKey = true;
            ReceiveState(state);
            return true;
        }

        public void ReceiveState(SafetyPinState safetyPin, LeverState lever) => ReceiveState(new FireExtinguisherState(safetyPin, lever));
        public void ReceiveState(FireExtinguisherState state)
        {
            if (!_isInputEnabled) return;

            _lastReceiveTime = Time.unscaledTime;
            _isConnected = true;
            _currentState = state;
            _fireExtinguisher.SetState(state);
            
            if (_logReceivedStates) LogReceivedState(state);
        }

        public void Refill()
        {
            _fireExtinguisher.Refill();
        }

        public void SetInputEnabled(bool isEnabled)
        {
            _isInputEnabled = isEnabled;
            if (isEnabled) return;

            _isConnected = false;
            _currentState = _currentState.WithLever(LeverState.Released);
            _fireExtinguisher.SetState(_currentState);
        }

        public void ResetInputState()
        {
            _lastReceiveTime = float.NegativeInfinity;
            _isConnected = false;
            _currentState = FireExtinguisherState.DefaultState;
            _lastReceivedKey = Key.None;
            _hasReceivedKey = false;

            _fireExtinguisher.SetState(_currentState);
        }

        private void UpdateConnectionTimeout()
        {
            if (!_isInputEnabled) return;
            if (!_isConnected) return;
            if (Time.unscaledTime - _lastReceiveTime < Mathf.Max(0.01f, _connectionTimeout)) return;
            
            _isConnected = false;
            _currentState = _currentState.WithLever(LeverState.Released);

            _fireExtinguisher.SetState(_currentState);
            if (_logReceivedStates) Debug.LogWarning("Fire extinguisher connection timed out. Lever was released; safety pin state was preserved.", this);
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
