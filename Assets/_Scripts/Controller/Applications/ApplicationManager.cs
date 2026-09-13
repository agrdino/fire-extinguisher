using System;
using _Scripts.FireExtinguishers;
using _Scripts.Fires;
using UnityEngine;

namespace _Scripts.Controller
{
    [Flags]
    public enum TrainingFailureReason
    {
        None = 0,
        FireNotExtinguished = 1 << 0,
        FirefightingTimedOut = 1 << 1,
        ExtinguisherDepleted = 1 << 2,
        IncompatibleExtinguisherSelected = 1 << 3,
        EscapeTimedOut = 1 << 4
    }

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

        [Header("Timing")]
        [SerializeField, Min(0f)] private float _roundDuration = 60f;
        [SerializeField, Min(0f)] private float _escapeDuration = 30f;
        [SerializeField] private bool _isExploreTimeLimited = true;
        [SerializeField, Min(0f)] private float _exploreDuration = 30f;

        [Header("Runtime Controllers")]
        [SerializeField] private FireController _fireController;
        [SerializeField] private FireExtinguisherController _fireExtinguisherController;
        [SerializeField] private EmergencyExitPlacementController _exitPlacementController;

        [Header("Runtime References")]
        [SerializeField] private EmergencyExit _emergencyExit;
        [SerializeField] private Transform _playerRoot;
        [SerializeField] private Transform _playerView;
        [SerializeField] private GameObject _movementProviderObject;

        [Header("Runtime State")]
        [SerializeField] private ApplicationState _state = ApplicationState.Language;
        [SerializeField, Min(0f)] private float _remainingTime;
        [SerializeField] private FireExtinguisherType _selectedExtinguisherType = FireExtinguisherType.Unselect;
        [SerializeField] private TrainingFailureReason _failureReasons;

        private bool _isEscapeTimeLimited;
        private bool _isFireFlareUpPending;
        private IEnvironmentSceneContext _environmentContext;
        private EmergencyExitPathGuide _emergencyExitPathGuide;

        public ApplicationState State => _state;
        public float RoundDuration => _roundDuration;
        public float EscapeDuration => _escapeDuration;
        public float ExploreDuration => _exploreDuration;
        public float RemainingTime => _remainingTime;
        public bool IsExploring => _state == ApplicationState.Explore;
        public bool IsExploreTimeLimited => IsExploring && _isExploreTimeLimited;
        public bool IsFactoryResponding => _state == ApplicationState.FactoryResponse;
        public bool IsFighting => _state == ApplicationState.Fighting;
        public bool IsEscaping => _state == ApplicationState.Escape;
        public bool IsEscapeTimeLimited => IsEscaping && _isEscapeTimeLimited;
        public FireExtinguisherType SelectedExtinguisherType => _selectedExtinguisherType;
        public TrainingFailureReason FailureReasons => _failureReasons;
        public EmergencyExit EmergencyExit => _emergencyExit;
        public Transform PlayerView => GetPlayerView();
        public IEnvironmentSceneContext CurrentEnvironment => _environmentContext;

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
            Application.targetFrameRate = 60;

            _emergencyExitPathGuide = _emergencyExit != null
                ? _emergencyExit.GetComponentInChildren<EmergencyExitPathGuide>(true)
                : null;
            if (_emergencyExit != null && _emergencyExitPathGuide == null)
                Debug.LogError("Emergency Exit prefab is missing its preconfigured Escape Path Guide.", _emergencyExit);
            _emergencyExitPathGuide?.Initialize(_emergencyExit, _playerRoot);
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
            {
                _fireExtinguisherController.SetInputEnabled(false);
                _fireExtinguisherController.OnIncompatibleFireTargeted += HandleIncompatibleFireTargeted;
            }
            SetMovementEnabled(false);
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
            {
                _fireExtinguisherController.OnIncompatibleFireTargeted -= HandleIncompatibleFireTargeted;
                _fireExtinguisherController.SetInputEnabled(false);
            }
            _emergencyExitPathGuide?.SetVisible(false);
            SetMovementEnabled(false);
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
            if (!IsFighting && !IsEscaping) return;

            if (IsFighting || _isEscapeTimeLimited)
            {
                _remainingTime = Mathf.Max(0f, _remainingTime - Time.deltaTime);
                OnRemainingTimeChanged?.Invoke(_remainingTime);
            }

            if (IsFighting
                && !_isFireFlareUpPending
                && (_fireExtinguisherController.IsDepleted || _remainingTime <= 0f))
            {
                AddFailureReason(TrainingFailureReason.FireNotExtinguished);
                if (_remainingTime <= 0f) AddFailureReason(TrainingFailureReason.FirefightingTimedOut);
                if (_fireExtinguisherController.IsDepleted) AddFailureReason(TrainingFailureReason.ExtinguisherDepleted);
                BeginEscape(true);
                return;
            }

            if (IsEscaping && _isEscapeTimeLimited && _remainingTime <= 0f)
            {
                AddFailureReason(TrainingFailureReason.FireNotExtinguished | TrainingFailureReason.EscapeTimedOut);
                SetState(ApplicationState.Failed);
            }
        }

        private void OnDestroy()
        {
            if (Instance != this) return;
            Instance = null;
        }

        public void PrepareForSceneTransition()
        {
            _fireController.ClearFires();
            _fireExtinguisherController.SetInputEnabled(false);
            _emergencyExitPathGuide?.SetVisible(false);
            SetEmergencyExitActive(false);
            SetMovementEnabled(false);
        }

        public void BindEnvironment(
            IEnvironmentSceneContext environmentContext,
            ApplicationState entryState)
        {
            _environmentContext = environmentContext;
            _exitPlacementController.BindEnvironment(environmentContext);
            _fireController.BindEnvironment(environmentContext);
            ResetPlayerPose();
            SetState(entryState, true);
        }

        public void SetState(ApplicationState state)
        {
            SetState(state, false);
        }

        private void SetState(ApplicationState state, bool force)
        {
            if (_state == state && !force) return;

            ApplicationState previousState = _state;
            _state = state;
            SetMovementEnabled(CanMoveInState(_state));
            switch (_state)
            {
                case ApplicationState.Ready:
                case ApplicationState.Language:
                case ApplicationState.Guide:
                    ResetTrainingResult();
                    ResetExtinguisher();
                    SelectExtinguisher(FireExtinguisherType.Unselect);
                    _fireController.ClearFires();
                    _isEscapeTimeLimited = false;
                    SetEmergencyExitActive(false);
                    ResetPlayerPose();
                    break;

                case ApplicationState.SelectEnvironment:
                    ResetApplication();
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

                case ApplicationState.FactoryResponse:
                    ResetTrainingResult();
                    ResetExtinguisher();
                    SelectExtinguisher(FireExtinguisherType.Unselect);
                    _fireController.SpawnFires(_playerRoot, true);
                    _exitPlacementController.TryPosition(GetPlayerView());
                    break;

                case ApplicationState.SelectExtinguisher:
                    ResetTrainingResult();
                    ResetExtinguisher();
                    SelectExtinguisher(FireExtinguisherType.Unselect);
                    if (previousState != ApplicationState.FactoryResponse || _fireController.SelectedSpawnPoint == null)
                        _fireController.SpawnFires(_playerRoot);
                    _exitPlacementController.TryPosition(GetPlayerView());
                    break;

                case ApplicationState.Fighting:
                    _isEscapeTimeLimited = false;
                    _isFireFlareUpPending = false;
                    ResetExtinguisher();
                    if (!_fireExtinguisherController.FireExtinguisher.CanExtinguish(_fireController.CurrentFireType))
                        AddFailureReason(TrainingFailureReason.IncompatibleExtinguisherSelected);
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

                case ApplicationState.Completed:
                    _fireExtinguisherController.SetInputEnabled(false);
                    _emergencyExit?.Disarm();
                    break;

                case ApplicationState.Failed:
                    _fireExtinguisherController.SetInputEnabled(false);
                    SetEmergencyExitActive(false);
                    break;
            }

            _emergencyExitPathGuide?.SetVisible(_state == ApplicationState.Escape);
            OnStateChanged?.Invoke(_state);
        }

        private void ResetApplication()
        {
            ResetTrainingResult();
            _fireController.ClearFires();
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
            SetState(_environmentContext?.EnvironmentType == EnvironmentType.Factory
                ? ApplicationState.FactoryResponse
                : ApplicationState.SelectExtinguisher);
        }

        public void CompleteFactoryResponse()
        {
            if (!IsFactoryResponding) return;
            SetState(ApplicationState.SelectExtinguisher);
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
            if (_playerRoot == null || _environmentContext?.PlayerSpawnPoint == null) return;

            CharacterController characterController = _playerRoot.GetComponent<CharacterController>();
            bool wasEnabled = characterController != null && characterController.enabled;
            if (characterController != null) characterController.enabled = false;

            Transform spawnPoint = _environmentContext.PlayerSpawnPoint;
            _playerRoot.SetPositionAndRotation(spawnPoint.position, spawnPoint.rotation);

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
            if (IsFighting) BeginEscape(false);
        }

        private void HandleFireFlareUpStarted()
        {
            if (!IsFighting || _isFireFlareUpPending) return;
            _isFireFlareUpPending = true;
        }

        private void HandleFireFlareUpCompleted()
        {
            if (!IsFighting || !_isFireFlareUpPending) return;

            _isFireFlareUpPending = false;
            AddFailureReason(TrainingFailureReason.FireNotExtinguished);
            BeginEscape(true);
        }

        private void HandleEmergencyExitReached()
        {
            if (!IsFighting && !IsEscaping) return;

            if (_fireController.AreAllFiresExtinguished())
            {
                SetState(ApplicationState.Completed);
                return;
            }

            AddFailureReason(TrainingFailureReason.FireNotExtinguished);
            SetState(ApplicationState.Failed);
        }

        private void HandleIncompatibleFireTargeted(FireExtinguisherType extinguisherType, FireType fireType)
        {
            if (IsFighting) AddFailureReason(TrainingFailureReason.IncompatibleExtinguisherSelected);
        }

        private void AddFailureReason(TrainingFailureReason reason) => _failureReasons |= reason;

        private void ResetTrainingResult() => _failureReasons = TrainingFailureReason.None;

        private void BeginEscape(bool isTimeLimited)
        {
            _isEscapeTimeLimited = isTimeLimited;
            SetState(ApplicationState.Escape);
        }

        private static bool CanMoveInState(ApplicationState state)
        {
            return state == ApplicationState.Explore
                || state == ApplicationState.FactoryResponse
                || state == ApplicationState.SelectExtinguisher
                || state == ApplicationState.Fighting
                || state == ApplicationState.Escape;
        }
    }
}
