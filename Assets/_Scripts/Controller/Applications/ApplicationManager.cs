using System;
using _Scripts.FireExtinguishers;
using _Scripts.Fires;
using UnityEngine;

namespace _Scripts.Controller
{
    [DefaultExecutionOrder(-100)]
    [DisallowMultipleComponent]
    public sealed class ApplicationManager : MonoBehaviour
    {
        private static ApplicationManager _instance;
        public static ApplicationManager Instance 
        { 
            get => _instance; 
            private set => _instance = value; 
        }

        [SerializeField, Min(0f)] private float _roundDuration = 60f;
        [SerializeField, Min(0f)] private float _escapeDuration = 30f;

        [Header("Gameplay References")]
        [SerializeField] private EmergencyExit _emergencyExit;
        [SerializeField] private Transform _playerRoot;
        [SerializeField] private GameObject _movementProviderObject;
        [SerializeField] private FireExtinguisherType _defaultExtinguisherType = FireExtinguisherType.CO2;

        [Header("Runtime")]
        [SerializeField] private ApplicationState _state = ApplicationState.Start;
        [SerializeField, Min(0f)] private float _remainingTime;
        [SerializeField] private FireExtinguisherType _selectedExtinguisherType;

        private bool _isEscapeTimeLimited;

        private FireController _fireController;
        private FireExtinguisherController _fireExtinguisherController;
        private Vector3 _initialPlayerPosition;
        private Quaternion _initialPlayerRotation;

        public ApplicationState State => _state;
        public float RoundDuration => _roundDuration;
        public float EscapeDuration => _escapeDuration;
        public float RemainingTime => _remainingTime;
        public bool IsPlaying => _state == ApplicationState.Playing;
        public bool IsEscaping => _state == ApplicationState.Escape;
        public bool IsEscapeTimeLimited => IsEscaping && _isEscapeTimeLimited;
        public FireExtinguisherType SelectedExtinguisherType => _selectedExtinguisherType;

        public event Action<ApplicationState> OnStateChanged;
        public event Action<float> OnRemainingTimeChanged;
        public event Action<FireExtinguisherType> OnExtinguisherSelected;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
            _fireController = FireController.Instance;
            _fireExtinguisherController = FireExtinguisherController.Instance;
            if (_playerRoot != null)
            {
                _initialPlayerPosition = _playerRoot.position;
                _initialPlayerRotation = _playerRoot.rotation;
            }
        }

        private void OnEnable()
        {
            if (_fireController != null)
                _fireController.OnAllFiresExtinguished += HandleAllFiresExtinguished;
            if (_emergencyExit != null)
                _emergencyExit.OnPlayerReached += HandleEmergencyExitReached;
            if (_fireExtinguisherController != null)
                _fireExtinguisherController.SetInputEnabled(false);
            SetMovementEnabled(_state == ApplicationState.Playing || _state == ApplicationState.Escape);
        }

        private void OnDisable()
        {
            if (_fireController != null)
                _fireController.OnAllFiresExtinguished -= HandleAllFiresExtinguished;
            if (_emergencyExit != null)
                _emergencyExit.OnPlayerReached -= HandleEmergencyExitReached;
            if (_fireExtinguisherController != null)
                _fireExtinguisherController.SetInputEnabled(false);
            SetMovementEnabled(false);
        }

        private void Start()
        {
            Application.targetFrameRate = 60;
            SetState(ApplicationState.Start, true);
        }

        private void LateUpdate()
        {
            if (!IsPlaying && !IsEscaping) return;

            if (IsPlaying || _isEscapeTimeLimited)
            {
                _remainingTime = Mathf.Max(0f, _remainingTime - Time.deltaTime);
                OnRemainingTimeChanged?.Invoke(_remainingTime);
            }

            if (IsPlaying && (_fireExtinguisherController.IsDepleted || _remainingTime <= 0f))
            {
                BeginEscape(true);
                return;
            }

            if (IsEscaping && _isEscapeTimeLimited && _remainingTime <= 0f) SetState(ApplicationState.Lost);
        }

        private void OnDestroy()
        {
            if (Instance != this) return;
            Instance = null;
        }

        public void SetState(ApplicationState state)
        {
            SetState(state, false);
        }

        private void SetState(ApplicationState state, bool force = false)
        {
            if (_state == state && !force) return;

            _state = state;
            SetMovementEnabled(_state == ApplicationState.Playing || _state == ApplicationState.Escape);
            switch (_state)
            {
                case ApplicationState.Start:
                    ResetApplication();
                    break;

                case ApplicationState.Selecting:
                    _fireController.SpawnFires();
                    break;

                case ApplicationState.Playing:
                    _isEscapeTimeLimited = false;
                    ResetExtinguisher();
                    _remainingTime = _roundDuration;
                    OnRemainingTimeChanged?.Invoke(_remainingTime);
                    _fireExtinguisherController.SetInputEnabled(true);
                    SetEmergencyExitActive(true);
                    break;

                case ApplicationState.Escape:
                    _fireExtinguisherController.SetInputEnabled(false);
                    if (_isEscapeTimeLimited) _remainingTime = _escapeDuration;
                    OnRemainingTimeChanged?.Invoke(_remainingTime);
                    SetEmergencyExitActive(true);
                    break;

                case ApplicationState.Won:
                case ApplicationState.Lost:
                    _fireExtinguisherController.SetInputEnabled(false);
                    SetEmergencyExitActive(false);
                    break;
            }

            OnStateChanged?.Invoke(_state);
        }

        private void ResetApplication()
        {
            _fireController.ClearFires();
            ResetExtinguisher();
            SelectExtinguisher(_defaultExtinguisherType);
            _isEscapeTimeLimited = false;
            SetEmergencyExitActive(false);
            ResetPlayerPose();
            _remainingTime = _roundDuration;
            OnRemainingTimeChanged?.Invoke(_remainingTime);
        }

        private void ResetExtinguisher()
        {
            _fireExtinguisherController.SetInputEnabled(false);
            _fireExtinguisherController.ResetInputState();
            _fireExtinguisherController.Refill();
        }

        private void SetMovementEnabled(bool isEnabled)
        {
            if (_movementProviderObject != null) _movementProviderObject.SetActive(isEnabled);
        }

        public void SelectExtinguisher(FireExtinguisherType extinguisherType)
        {
            _selectedExtinguisherType = extinguisherType;
            _fireExtinguisherController.FireExtinguisher.SetType(extinguisherType);
            OnExtinguisherSelected?.Invoke(extinguisherType);
        }

        public void SelectCO2Extinguisher() => SelectExtinguisher(FireExtinguisherType.CO2);
        public void SelectPowderExtinguisher() => SelectExtinguisher(FireExtinguisherType.Powder);

        private void SetEmergencyExitActive(bool isActive)
        {
            if (_emergencyExit == null) return;

            if (isActive)
            {
                _emergencyExit.gameObject.SetActive(true);
                _emergencyExit.Arm(_playerRoot);
                return;
            }

            _emergencyExit.Disarm();
            _emergencyExit.gameObject.SetActive(false);
        }

        private void ResetPlayerPose()
        {
            if (_playerRoot == null) return;

            CharacterController characterController = _playerRoot.GetComponent<CharacterController>();
            bool wasEnabled = characterController != null && characterController.enabled;
            if (characterController != null) characterController.enabled = false;
            _playerRoot.SetPositionAndRotation(_initialPlayerPosition, _initialPlayerRotation);
            if (characterController != null) characterController.enabled = wasEnabled;
        }

        private void HandleAllFiresExtinguished()
        {
            if (IsPlaying) BeginEscape(false);
        }

        private void HandleEmergencyExitReached()
        {
            if (IsPlaying)
            {
                SetState(ApplicationState.Start);
                return;
            }

            if (IsEscaping) SetState(ApplicationState.Won);
        }

        private void BeginEscape(bool isTimeLimited)
        {
            _isEscapeTimeLimited = isTimeLimited;
            SetState(ApplicationState.Escape);
        }
    }
}
