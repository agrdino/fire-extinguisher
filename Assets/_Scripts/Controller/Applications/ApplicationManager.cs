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
        [SerializeField] private bool _isExploreTimeLimited = true;
        [SerializeField, Min(0f)] private float _exploreDuration = 30f;

        [Header("Gameplay References")]
        [SerializeField] private EmergencyExit _emergencyExit;
        [SerializeField] private Transform _playerRoot;
        [SerializeField] private Transform _playerView;
        [SerializeField] private GameObject _movementProviderObject;

        [Header("Runtime")]
        [SerializeField] private ApplicationState _state = ApplicationState.Language;
        [SerializeField, Min(0f)] private float _remainingTime;
        [SerializeField] private FireExtinguisherType _selectedExtinguisherType = FireExtinguisherType.Unselect;

        private bool _isEscapeTimeLimited;
        private bool _isFireFlareUpPending;

        private FireController _fireController;
        private FireExtinguisherController _fireExtinguisherController;
        private EnviromentController _enviromentController;
        private EmergencyExitPathGuide _emergencyExitPathGuide;
        private Vector3 _initialPlayerPosition;
        private Quaternion _initialPlayerRotation;

        public ApplicationState State => _state;
        public float RoundDuration => _roundDuration;
        public float EscapeDuration => _escapeDuration;
        public float ExploreDuration => _exploreDuration;
        public float RemainingTime => _remainingTime;
        public bool IsExploring => _state == ApplicationState.Explore;
        public bool IsExploreTimeLimited => IsExploring && _isExploreTimeLimited;
        public bool IsPlaying => _state == ApplicationState.Playing;
        public bool IsEscaping => _state == ApplicationState.Escape;
        public bool IsEscapeTimeLimited => IsEscaping && _isEscapeTimeLimited;
        public FireExtinguisherType SelectedExtinguisherType => _selectedExtinguisherType;
        public EmergencyExit EmergencyExit => _emergencyExit;
        public Transform PlayerView => GetPlayerView();

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
            _enviromentController = EnviromentController.Instance;
            _emergencyExitPathGuide = _emergencyExit != null
                ? _emergencyExit.GetComponentInChildren<EmergencyExitPathGuide>(true)
                : null;
            if (_emergencyExit != null && _emergencyExitPathGuide == null)
                Debug.LogError("Emergency Exit prefab is missing its preconfigured Escape Path Guide.", _emergencyExit);
            _emergencyExitPathGuide?.Initialize(_emergencyExit, _playerRoot);
            if (_playerRoot != null)
            {
                _initialPlayerPosition = _playerRoot.position;
                _initialPlayerRotation = _playerRoot.rotation;
            }
        }

        private void OnEnable()
        {
            if (_fireController != null)
            {
                _fireController.OnAllFiresExtinguished += HandleAllFiresExtinguished;
                _fireController.OnFireFlareUpStarted += HandleFireFlareUpStarted;
                _fireController.OnFireFlareUpCompleted += HandleFireFlareUpCompleted;
            }
            if (_emergencyExit != null)
                _emergencyExit.OnPlayerReached += HandleEmergencyExitReached;
            if (_fireExtinguisherController != null)
                _fireExtinguisherController.SetInputEnabled(false);
            SetMovementEnabled(CanMoveInState(_state));
        }

        private void OnDisable()
        {
            if (_fireController != null)
            {
                _fireController.OnAllFiresExtinguished -= HandleAllFiresExtinguished;
                _fireController.OnFireFlareUpStarted -= HandleFireFlareUpStarted;
                _fireController.OnFireFlareUpCompleted -= HandleFireFlareUpCompleted;
            }
            if (_emergencyExit != null)
                _emergencyExit.OnPlayerReached -= HandleEmergencyExitReached;
            if (_fireExtinguisherController != null)
                _fireExtinguisherController.SetInputEnabled(false);
            _emergencyExitPathGuide?.SetVisible(false);
            SetMovementEnabled(false);
        }

        private void Start()
        {
            Application.targetFrameRate = 60;
            SetState(ApplicationState.Language, true);
        }

        private void LateUpdate()
        {
            if (IsExploreTimeLimited)
            {
                _remainingTime = Mathf.Max(0f, _remainingTime - Time.deltaTime);
                OnRemainingTimeChanged?.Invoke(_remainingTime);
                if (_remainingTime <= 0f) CompleteExplore();
                return;
            }

            if (IsExploring) return;

            if (!IsPlaying && !IsEscaping) return;

            if (IsPlaying || _isEscapeTimeLimited)
            {
                _remainingTime = Mathf.Max(0f, _remainingTime - Time.deltaTime);
                OnRemainingTimeChanged?.Invoke(_remainingTime);
            }

            if (IsPlaying && !_isFireFlareUpPending && (_fireExtinguisherController.IsDepleted || _remainingTime <= 0f))
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
            SetMovementEnabled(CanMoveInState(_state));
            switch (_state)
            {
                case ApplicationState.Start:
                    ResetApplication();
                    break;

                case ApplicationState.Language:
                case ApplicationState.Guide:
                    ResetExtinguisher();
                    SelectExtinguisher(FireExtinguisherType.Unselect);
                    _fireController.ClearFires();
                    _isEscapeTimeLimited = false;
                    SetEmergencyExitActive(false);
                    ResetPlayerPose();
                    break;

                case ApplicationState.Explore:
                    ResetExtinguisher();
                    SelectExtinguisher(FireExtinguisherType.Unselect);
                    _fireController.ClearFires();
                    _isEscapeTimeLimited = false;
                    SetEmergencyExitActive(false);
                    ResetPlayerPose();
                    _remainingTime = _exploreDuration;
                    OnRemainingTimeChanged?.Invoke(_remainingTime);
                    break;

                case ApplicationState.Selecting:
                    ResetExtinguisher();
                    SelectExtinguisher(FireExtinguisherType.Unselect);
                    _fireController.SpawnFires(_playerRoot);
                    _enviromentController?.PositionEmergencyExit(GetPlayerView());
                    break;

                case ApplicationState.Playing:
                    _isEscapeTimeLimited = false;
                    _isFireFlareUpPending = false;
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
                    _fireExtinguisherController.SetInputEnabled(false);
                    _emergencyExit?.Disarm();
                    break;

                case ApplicationState.Lost:
                    _fireExtinguisherController.SetInputEnabled(false);
                    SetEmergencyExitActive(false);
                    break;
            }

            _emergencyExitPathGuide?.SetVisible(_state == ApplicationState.Escape);
            OnStateChanged?.Invoke(_state);
        }

        private void ResetApplication()
        {
            _fireController.ClearFires();
            _enviromentController?.ShowStartEnviroment();
            ResetExtinguisher();
            SelectExtinguisher(FireExtinguisherType.Unselect);
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

        public void CompleteExplore()
        {
            if (!IsExploring) return;
            SetState(ApplicationState.Selecting);
        }

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

        private Transform GetPlayerView()
        {
            if (_playerView != null) return _playerView;

            Camera mainCamera = Camera.main;
            return mainCamera != null ? mainCamera.transform : _playerRoot;
        }

        private void HandleAllFiresExtinguished()
        {
            if (IsPlaying) BeginEscape(false);
        }

        private void HandleFireFlareUpStarted()
        {
            if (!IsPlaying || _isFireFlareUpPending) return;

            _isFireFlareUpPending = true;
        }

        private void HandleFireFlareUpCompleted()
        {
            if (!IsPlaying || !_isFireFlareUpPending) return;

            _isFireFlareUpPending = false;
            BeginEscape(true);
        }

        private void HandleEmergencyExitReached()
        {
            if (IsPlaying)
            {
                SetState(ApplicationState.Guide);
                return;
            }

            if (IsEscaping) SetState(ApplicationState.Won);
        }

        private void BeginEscape(bool isTimeLimited)
        {
            _isEscapeTimeLimited = isTimeLimited;
            SetState(ApplicationState.Escape);
        }

        private static bool CanMoveInState(ApplicationState state)
        {
            return state == ApplicationState.Guide
                || state == ApplicationState.Explore
                || state == ApplicationState.Selecting
                || state == ApplicationState.Playing
                || state == ApplicationState.Escape;
        }
    }
}
