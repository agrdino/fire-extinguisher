using System;
using System.Collections.Generic;
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
        [SerializeField, Min(0f)] private float _roundDuration = 90f;
        [SerializeField, Min(0f)] private float _escapeDuration = 40f;
        [SerializeField] private bool _isExploreTimeLimited = true;
        [SerializeField, Min(0f)] private float _exploreDuration = 30f;

        [Header("Runtime Controllers")]
        [SerializeField] private FireController _fireController;
        [SerializeField] private FireExtinguisherController _fireExtinguisherController;
        [SerializeField] private EmergencyExitPlacementController _exitPlacementController;

        [Header("Runtime References")]
        [SerializeField] private EmergencyExit _emergencyExit;
        [SerializeField] private EmergencyPathGuide _emergencyPathGuide;
        [SerializeField] private Transform _playerRoot;
        [SerializeField] private Transform _playerView;
        [SerializeField] private GameObject _movementProviderObject;

        [Header("Runtime State")]
        [SerializeField] private ApplicationState _state = ApplicationState.Language;
        [SerializeField, Min(0f)] private float _remainingTime;
        [SerializeField] private FireExtinguisherType _selectedExtinguisherType = FireExtinguisherType.Unselect;
        [SerializeField, Range(0f, 1f)] private float _co2RemainingRatio = 1f;
        [SerializeField, Range(0f, 1f)] private float _powderRemainingRatio = 1f;
        [SerializeField] private TrainingFailureReason _failureReasons;

        private bool _isEscapeTimeLimited;
        private bool _isRoundTimerRunning;
        private bool _hasPendingExtinguisherSelection;
        private FireExtinguisherType _pendingExtinguisherType = FireExtinguisherType.Unselect;
        private FireExtinguisherModelSwitcher _modelSwitcher;
        private FireExtinguisherStation _activeExtinguisherStation;
        private IEnvironmentSceneContext _environmentContext;

        public ApplicationState State => _state;
        public float RoundDuration => _roundDuration;
        public float EscapeDuration => _escapeDuration;
        public float ExploreDuration => _exploreDuration;
        public float RemainingTime => _remainingTime;
        public bool IsExploring => _state == ApplicationState.Explore;
        public bool IsExploreTimeLimited => IsExploring && _isExploreTimeLimited;
        public bool IsFactoryResponding => _state == ApplicationState.FactoryResponse;
        public bool IsFighting => _state == ApplicationState.Fighting;
        public bool IsEmergencyContacting => _state == ApplicationState.ContactEmergencyTeam;
        public bool IsEscaping => _state == ApplicationState.Escape;
        public bool IsEscapeTimeLimited => IsEscaping && _isEscapeTimeLimited;
        public FireExtinguisherType SelectedExtinguisherType => _selectedExtinguisherType;
        public TrainingFailureReason FailureReasons => _failureReasons;
        public EmergencyExit EmergencyExit => _emergencyExit;
        public Transform PlayerView => GetPlayerView();
        public IEnvironmentSceneContext CurrentEnvironment => _environmentContext;
        public FireExtinguisherStation ActiveExtinguisherStation => _activeExtinguisherStation;

        public event Action<ApplicationState> OnStateChanged;
        public event Action<float> OnRemainingTimeChanged;
        public event Action<FireExtinguisherType> OnExtinguisherSelected;
        public event Action<IEnvironmentSceneContext> OnEnvironmentBound;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;
            Application.targetFrameRate = 60;
            _modelSwitcher = _fireExtinguisherController?.FireExtinguisher != null
                ? _fireExtinguisherController.FireExtinguisher.GetComponent<FireExtinguisherModelSwitcher>()
                : null;

            _emergencyPathGuide?.Initialize(GetPlayerView(), _playerRoot);
        }

        private void OnEnable()
        {
            if (_fireController != null)
            {
                _fireController.OnAllFiresExtinguished += HandleAllFiresExtinguished;
                _fireController.OnDangerThresholdReached += HandleDangerThresholdReached;
            }

            if (_emergencyExit != null)
                _emergencyExit.OnPlayerReached += HandleEmergencyExitReached;
            if (_fireExtinguisherController != null)
            {
                _fireExtinguisherController.SetInputEnabled(false);
                _fireExtinguisherController.OnIncompatibleFireTargeted += HandleIncompatibleFireTargeted;
            }
            if (_modelSwitcher != null)
            {
                _modelSwitcher.OnOutgoingDissolveStarted += HandleOutgoingDissolveStarted;
                _modelSwitcher.OnIncomingDissolveStarted += HandleIncomingDissolveStarted;
                _modelSwitcher.OnVisualTypeChanged += HandleVisualTypeChanged;
                _modelSwitcher.OnTransitionCompleted += HandleExtinguisherTransitionCompleted;
            }
            SetMovementEnabled(false);
        }

        private void OnDisable()
        {
            if (_fireController != null)
            {
                _fireController.OnAllFiresExtinguished -= HandleAllFiresExtinguished;
                _fireController.OnDangerThresholdReached -= HandleDangerThresholdReached;
            }

            if (_emergencyExit != null)
                _emergencyExit.OnPlayerReached -= HandleEmergencyExitReached;
            if (_fireExtinguisherController != null)
            {
                _fireExtinguisherController.OnIncompatibleFireTargeted -= HandleIncompatibleFireTargeted;
                _fireExtinguisherController.SetInputEnabled(false);
            }
            if (_modelSwitcher != null)
            {
                _modelSwitcher.OnOutgoingDissolveStarted -= HandleOutgoingDissolveStarted;
                _modelSwitcher.OnIncomingDissolveStarted -= HandleIncomingDissolveStarted;
                _modelSwitcher.OnVisualTypeChanged -= HandleVisualTypeChanged;
                _modelSwitcher.OnTransitionCompleted -= HandleExtinguisherTransitionCompleted;
            }
            SetEmergencyPathTarget(null);
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
            if (!IsRoundInProgress() && !IsEscaping) return;

            if (_isRoundTimerRunning || _isEscapeTimeLimited)
            {
                _remainingTime = Mathf.Max(0f, _remainingTime - Time.deltaTime);
                OnRemainingTimeChanged?.Invoke(_remainingTime);
            }

            if (_isRoundTimerRunning && _remainingTime <= 0f)
            {
                AddFailureReason(TrainingFailureReason.FireNotExtinguished | TrainingFailureReason.FirefightingTimedOut);
                BeginEscape(true);
                return;
            }

            if (IsFighting
                && !_hasPendingExtinguisherSelection
                && _selectedExtinguisherType != FireExtinguisherType.Unselect
                && _fireExtinguisherController.IsDepleted
                && _fireExtinguisherController.FireExtinguisher.CanExtinguish(_fireController.CurrentFireType))
            {
                AddFailureReason(TrainingFailureReason.FireNotExtinguished | TrainingFailureReason.ExtinguisherDepleted);
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
            SetEmergencyPathTarget(null);
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
            OnEnvironmentBound?.Invoke(_environmentContext);
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
                    _isRoundTimerRunning = false;
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
                    _isRoundTimerRunning = false;
                    SetEmergencyExitActive(false);
                    ResetPlayerPose();
                    _remainingTime = _exploreDuration;
                    OnRemainingTimeChanged?.Invoke(_remainingTime);
                    break;

                case ApplicationState.FactoryResponse:
                    ResetTrainingResult();
                    ResetExtinguisher();
                    SelectExtinguisher(FireExtinguisherType.Unselect);
                    EnsureRoundTimerStarted();
                    _fireController.SpawnFires(_playerRoot, true);
                    ConfigureExtinguisherStations();
                    _exitPlacementController.TryPosition(GetPlayerView());
                    break;

                case ApplicationState.SelectExtinguisher:
                    ResetTrainingResult();
                    ResetExtinguisher();
                    SelectExtinguisher(FireExtinguisherType.Unselect);
                    EnsureRoundTimerStarted();
                    if (previousState != ApplicationState.FactoryResponse || _fireController.SelectedSpawnPoint == null)
                        _fireController.SpawnFires(_playerRoot);
                    _exitPlacementController.TryPosition(GetPlayerView());
                    break;

                case ApplicationState.Fighting:
                    _isEscapeTimeLimited = false;
                    ResetExtinguisher();
                    SelectExtinguisher(FireExtinguisherType.Unselect);
                    if (previousState != ApplicationState.FactoryResponse || _fireController.SelectedSpawnPoint == null)
                        _fireController.SpawnFires(_playerRoot);
                    ConfigureExtinguisherStations();
                    EnsureRoundTimerStarted();
                    _fireExtinguisherController.SetInputEnabled(false);
                    SetEmergencyExitActive(true);
                    break;

                case ApplicationState.Escape:
                    _isRoundTimerRunning = false;
                    _fireExtinguisherController.SetInputEnabled(false);
                    if (_isEscapeTimeLimited) _remainingTime = _escapeDuration;
                    OnRemainingTimeChanged?.Invoke(_remainingTime);
                    SetEmergencyExitActive(true);
                    break;

                case ApplicationState.ContactEmergencyTeam:
                    _isRoundTimerRunning = false;
                    _isEscapeTimeLimited = false;
                    _fireExtinguisherController.SetInputEnabled(false);
                    SetEmergencyExitActive(false);
                    break;

                case ApplicationState.Completed:
                case ApplicationState.Escaped:
                    _isRoundTimerRunning = false;
                    _fireExtinguisherController.SetInputEnabled(false);
                    _emergencyExit?.Disarm();
                    break;

                case ApplicationState.Failed:
                    _isRoundTimerRunning = false;
                    _fireExtinguisherController.SetInputEnabled(false);
                    SetEmergencyExitActive(false);
                    break;
            }

            UpdateEmergencyPathTarget();
            RefreshExtinguisherStationInteractions();
            OnStateChanged?.Invoke(_state);
        }

        private void ResetApplication()
        {
            ResetTrainingResult();
            _fireController.ClearFires();
            ResetExtinguisher();
            SelectExtinguisher(FireExtinguisherType.Unselect);
            _isEscapeTimeLimited = false;
            _isRoundTimerRunning = false;
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
            _co2RemainingRatio = 1f;
            _powderRemainingRatio = 1f;
            _hasPendingExtinguisherSelection = false;
            _pendingExtinguisherType = FireExtinguisherType.Unselect;
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

        public bool TrySelectExtinguisher(
            FireExtinguisherType extinguisherType,
            FireExtinguisherStation station)
        {
            if (!IsFighting
                || station == null
                || station != _activeExtinguisherStation
                || extinguisherType == FireExtinguisherType.Unselect
                || extinguisherType == _selectedExtinguisherType
                || _hasPendingExtinguisherSelection
                || _modelSwitcher == null
                || _modelSwitcher.IsTransitioning)
                return false;

            SaveSelectedExtinguisherAmount();
            _hasPendingExtinguisherSelection = true;
            _pendingExtinguisherType = extinguisherType;
            _fireExtinguisherController.SetInputEnabled(false);
            station.BeginTransition();

            if (_modelSwitcher.TryTransitionTo(extinguisherType))
            {
                // Confirm the accepted object interaction immediately; the logical
                // type still commits at the dissolve midpoint as before.
                station.PlaySelectionFeedback(extinguisherType);
                return true;
            }

            _hasPendingExtinguisherSelection = false;
            _pendingExtinguisherType = FireExtinguisherType.Unselect;
            station.CompleteTransition();
            return false;
        }

        public void SelectCO2Extinguisher() => SelectExtinguisher(FireExtinguisherType.CO2);
        public void SelectPowderExtinguisher() => SelectExtinguisher(FireExtinguisherType.Powder);

        public void CompleteExplore()
        {
            if (!IsExploring) return;
            SetState(_environmentContext?.EnvironmentType == EnvironmentType.Factory
                ? ApplicationState.FactoryResponse
                : ApplicationState.Fighting);
        }

        public void CompleteFactoryResponse()
        {
            if (!IsFactoryResponding) return;
            SetState(ApplicationState.Fighting);
        }

        public void CompleteEmergencyContact()
        {
            if (!IsEmergencyContacting) return;
            BeginEscape(false);
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

        private void UpdateEmergencyPathTarget()
        {
            Transform target = _state switch
            {
                ApplicationState.ContactEmergencyTeam => _environmentContext?.EmergencyContactController?.transform,
                ApplicationState.Escape => _emergencyExit != null ? _emergencyExit.transform : null,
                _ => null
            };
            SetEmergencyPathTarget(target);
        }

        private void SetEmergencyPathTarget(Transform target)
        {
            if (_emergencyPathGuide == null) return;
            _emergencyPathGuide.SetTarget(target);
            _emergencyPathGuide.SetVisible(target != null);
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
            if (!IsFighting) return;

            if (_environmentContext?.Supports(EnvironmentFeature.EmergencyContact) == true)
                SetState(ApplicationState.ContactEmergencyTeam);
            else
                BeginEscape(false);
        }

        private void HandleDangerThresholdReached()
        {
            if (!IsFighting) return;
            AddFailureReason(TrainingFailureReason.FireNotExtinguished | TrainingFailureReason.IncompatibleExtinguisherSelected);
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
            SetState(ApplicationState.Escaped);
        }

        private void HandleIncompatibleFireTargeted(FireExtinguisherType extinguisherType, FireType fireType)
        {
            if (IsFighting) AddFailureReason(TrainingFailureReason.IncompatibleExtinguisherSelected);
        }

        private void HandleOutgoingDissolveStarted(FireExtinguisherType outgoingType)
        {
            if (!_hasPendingExtinguisherSelection || _activeExtinguisherStation == null) return;
            _activeExtinguisherStation.PlayOutgoingToGround(outgoingType, _modelSwitcher.PhaseDuration);
        }

        private void HandleIncomingDissolveStarted(FireExtinguisherType incomingType)
        {
            if (!_hasPendingExtinguisherSelection || _activeExtinguisherStation == null) return;
            _activeExtinguisherStation.PlayIncomingFromGround(incomingType, _modelSwitcher.PhaseDuration);
        }

        private void HandleVisualTypeChanged(FireExtinguisherType visualType)
        {
            if (!_hasPendingExtinguisherSelection || visualType != _pendingExtinguisherType) return;

            _selectedExtinguisherType = visualType;
            _fireExtinguisherController.FireExtinguisher.SetRemainingRatio(GetStoredRemainingRatio(visualType));
            OnExtinguisherSelected?.Invoke(visualType);
        }

        private void HandleExtinguisherTransitionCompleted()
        {
            if (!_hasPendingExtinguisherSelection) return;

            _hasPendingExtinguisherSelection = false;
            _pendingExtinguisherType = FireExtinguisherType.Unselect;
            _activeExtinguisherStation?.CompleteTransition();
            if (IsFighting && _selectedExtinguisherType != FireExtinguisherType.Unselect)
                _fireExtinguisherController.SetInputEnabled(true);
        }

        private void SaveSelectedExtinguisherAmount()
        {
            float ratio = _fireExtinguisherController.FireExtinguisher.RemainingRatio;
            switch (_selectedExtinguisherType)
            {
                case FireExtinguisherType.CO2:
                    _co2RemainingRatio = ratio;
                    break;
                case FireExtinguisherType.Powder:
                    _powderRemainingRatio = ratio;
                    break;
            }
        }

        private float GetStoredRemainingRatio(FireExtinguisherType type)
        {
            return type switch
            {
                FireExtinguisherType.CO2 => _co2RemainingRatio,
                FireExtinguisherType.Powder => _powderRemainingRatio,
                _ => 1f
            };
        }

        private void ConfigureExtinguisherStations()
        {
            _activeExtinguisherStation = null;
            IReadOnlyList<FireExtinguisherStation> stations = _environmentContext?.FireExtinguisherStations;
            if (stations == null) return;

            FireSpawnPoint selectedSpawnPoint = _fireController.SelectedSpawnPoint;
            for (int index = 0; index < stations.Count; index++)
            {
                FireExtinguisherStation station = stations[index];
                if (station == null) continue;

                bool isActive = _activeExtinguisherStation == null && station.Matches(selectedSpawnPoint);
                if (isActive) _activeExtinguisherStation = station;
                station.Configure(isActive, _selectedExtinguisherType);
            }
        }

        private void RefreshExtinguisherStationInteractions()
        {
            IReadOnlyList<FireExtinguisherStation> stations = _environmentContext?.FireExtinguisherStations;
            if (stations == null) return;
            for (int index = 0; index < stations.Count; index++)
                stations[index]?.RefreshInteractionState();
        }

        private void AddFailureReason(TrainingFailureReason reason) => _failureReasons |= reason;

        private void ResetTrainingResult() => _failureReasons = TrainingFailureReason.None;

        private void BeginEscape(bool isTimeLimited)
        {
            _isEscapeTimeLimited = isTimeLimited;
            SetState(ApplicationState.Escape);
        }

        private void EnsureRoundTimerStarted()
        {
            if (_isRoundTimerRunning) return;
            _isRoundTimerRunning = true;
            _remainingTime = _roundDuration;
            OnRemainingTimeChanged?.Invoke(_remainingTime);
        }

        private bool IsRoundInProgress()
        {
            return IsFactoryResponding || _state == ApplicationState.SelectExtinguisher || IsFighting;
        }

        private static bool CanMoveInState(ApplicationState state)
        {
            return state == ApplicationState.Explore
                || state == ApplicationState.FactoryResponse
                || state == ApplicationState.SelectExtinguisher
                || state == ApplicationState.Fighting
                || state == ApplicationState.ContactEmergencyTeam
                || state == ApplicationState.Escape;
        }
    }
}
