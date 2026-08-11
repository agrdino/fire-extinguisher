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

        [Header("Runtime")]
        [SerializeField] private ApplicationState _state = ApplicationState.Start;
        [SerializeField, Min(0f)] private float _remainingTime;

        private FireController _fireController;
        private FireExtinguisherController _fireExtinguisherController;

        public ApplicationState State => _state;
        public float RoundDuration => _roundDuration;
        public float RemainingTime => _remainingTime;
        public bool IsPlaying => _state == ApplicationState.Playing;

        public event Action<ApplicationState> OnStateChanged;
        public event Action<float> OnRemainingTimeChanged;

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
        }

        private void OnEnable()
        {
            _fireController.OnAllFiresExtinguished += HandleAllFiresExtinguished;
            _fireExtinguisherController.SetInputEnabled(false);
        }

        private void OnDisable()
        {
            _fireController.OnAllFiresExtinguished -= HandleAllFiresExtinguished;
            _fireExtinguisherController.SetInputEnabled(false);
        }

        private void Start()
        {
            Application.targetFrameRate = 60;
            SetState(ApplicationState.Start, true);
        }

        private void LateUpdate()
        {
            if (!IsPlaying) return;

            _remainingTime = Mathf.Max(0f, _remainingTime - Time.deltaTime);
            OnRemainingTimeChanged?.Invoke(_remainingTime);

            if (_fireExtinguisherController.IsDepleted || _remainingTime <= 0f)
                SetState(ApplicationState.Lost);
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
            switch (_state)
            {
                case ApplicationState.Start:
                    ResetApplication();
                    break;

                case ApplicationState.Selecting:
                    _fireController.SpawnFires();
                    break;

                case ApplicationState.Playing:
                    ResetExtinguisher();
                    _remainingTime = _roundDuration;
                    OnRemainingTimeChanged?.Invoke(_remainingTime);
                    _fireExtinguisherController.SetInputEnabled(true);
                    break;

                case ApplicationState.Won:
                case ApplicationState.Lost:
                    _fireExtinguisherController.SetInputEnabled(false);
                    break;
            }

            OnStateChanged?.Invoke(_state);
        }

        private void ResetApplication()
        {
            _fireController.ClearFires();
            ResetExtinguisher();
            _remainingTime = _roundDuration;
            OnRemainingTimeChanged?.Invoke(_remainingTime);
        }

        private void ResetExtinguisher()
        {
            _fireExtinguisherController.SetInputEnabled(false);
            _fireExtinguisherController.ResetInputState();
            _fireExtinguisherController.Refill();
        }

        private void HandleAllFiresExtinguished()
        {
            if (IsPlaying) SetState(ApplicationState.Won);
        }
    }
}
