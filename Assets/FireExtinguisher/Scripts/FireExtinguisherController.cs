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
        public enum InputSource
        {
            None,
            Keyboard,
            XRController,
            DirectState
        }

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
        [SerializeField] private InputSource _activeInputSource;

        private float _lastReceiveTime = float.NegativeInfinity;

        public FireExtinguisher FireExtinguisher => _fireExtinguisher;
        public bool IsDepleted => _fireExtinguisher.IsDepleted;

        public SO_FireExtinguisherInputSettings InputSettings => _inputSettings;
        public FireExtinguisherState CurrentState => _currentState;
        public bool IsConnected => _isConnected;
        public Key LastReceivedKey => _lastReceivedKey;
        public bool HasReceivedKey => _hasReceivedKey;
        public bool IsInputEnabled => _isInputEnabled;
        public InputSource ActiveInputSource => _activeInputSource;
        public bool IsReadyToStart =>
            _isConnected && _hasReceivedKey && _lastReceivedKey == Key.A;
        
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

        public bool TryReceiveKey(Key key) => TryReceiveKey(key, false);

        public bool TryReceiveKeyHeartbeat(Key key) => TryReceiveKey(key, true);

        private bool TryReceiveKey(Key key, bool isHeartbeat)
        {
            if (_inputSettings == null ||
                !_inputSettings.TryGetState(key, out FireExtinguisherState state))
                return false;

            RegisterInputActivity();
            _lastReceivedKey = key;
            _hasReceivedKey = true;

            if (!isHeartbeat || _activeInputSource == InputSource.None)
                _activeInputSource = InputSource.Keyboard;

            if (!_isInputEnabled) return true;
            if (isHeartbeat && _activeInputSource != InputSource.Keyboard) return true;

            ApplyState(state);
            return true;
        }

        public void ReceiveState(SafetyPinState safetyPin, LeverState lever) => ReceiveState(new FireExtinguisherState(safetyPin, lever));
        public void ReceiveState(FireExtinguisherState state)
        {
            if (!_isInputEnabled) return;

            RegisterInputActivity();
            _activeInputSource = InputSource.DirectState;
            ApplyState(state);
        }

        public void ReceiveXRControls(bool isTriggerPressed, bool toggleSafetyPin)
        {
            if (!_isInputEnabled) return;

            RegisterInputActivity();
            _activeInputSource = InputSource.XRController;

            FireExtinguisherState state = _currentState;
            if (toggleSafetyPin)
            {
                SafetyPinState nextSafetyPin = state.SafetyPin == SafetyPinState.Inserted
                    ? SafetyPinState.Removed
                    : SafetyPinState.Inserted;
                state = state.WithSafetyPin(nextSafetyPin);
            }

            LeverState lever = isTriggerPressed
                ? LeverState.Pressed
                : LeverState.Released;
            ApplyState(state.WithLever(lever));
        }

        public void RefreshXRInputConnection()
        {
            if (!_isInputEnabled || _activeInputSource != InputSource.XRController) return;
            RegisterInputActivity();
        }

        public void Refill()
        {
            _fireExtinguisher.Refill();
        }

        public void SetInputEnabled(bool isEnabled)
        {
            _isInputEnabled = isEnabled;
            if (isEnabled) return;

            _currentState = _currentState.WithLever(LeverState.Released);
            _fireExtinguisher.SetState(_currentState);
        }

        public void ResetInputState()
        {
            _isInputEnabled = false;
            _activeInputSource = InputSource.None;
            _currentState = FireExtinguisherState.DefaultState;

            _fireExtinguisher.SetState(_currentState);
        }

        private void UpdateConnectionTimeout()
        {
            if (!_isConnected) return;
            if (Time.unscaledTime - _lastReceiveTime < Mathf.Max(0.01f, _connectionTimeout)) return;
            
            _isConnected = false;
            if (!_isInputEnabled) return;

            _currentState = _currentState.WithLever(LeverState.Released);

            _fireExtinguisher.SetState(_currentState);
            if (_logReceivedStates) Debug.LogWarning("Fire extinguisher connection timed out. Lever was released; safety pin state was preserved.", this);
        }

        private void RegisterInputActivity()
        {
            _lastReceiveTime = Time.unscaledTime;
            _isConnected = true;
        }

        private void ApplyState(FireExtinguisherState state)
        {
            if (_currentState == state) return;

            _currentState = state;
            _fireExtinguisher.SetState(state);

            if (_logReceivedStates) LogReceivedState(state);
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
