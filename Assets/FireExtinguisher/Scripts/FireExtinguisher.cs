using System;
using UnityEngine;

namespace _Scripts.FireExtinguishers
{
    [DisallowMultipleComponent]
    public sealed class FireExtinguisher : MonoBehaviour
    {
        [SerializeField] private FireExtinguisherState _state;

        [Header("Capacity")]
        [SerializeField, Min(0f)] private float _capacity = 10f;
        [SerializeField, Min(0f)] private float _consumptionPerSecond = 1f;
        [SerializeField, Min(0f)] private float _remainingAmount = 10f;
        [SerializeField, Range(0f, 1f)] private float _lowAmountThresholdRatio = 0.2f;

        public event Action<FireExtinguisherState> OnStateChanged;
        public event Action<float> OnCapacityChanged;
        public event Action<float, float> OnRemainingAmountChanged;
        public event Action<bool> OnCanSprayChanged;
        public event Action OnLowAmount;
        public event Action OnDepleted;
        public event Action OnRefilled;

        public FireExtinguisherState CurrentState => _state;
        public float Capacity => _capacity;
        public float ConsumptionPerSecond => _consumptionPerSecond;
        public float RemainingAmount => _remainingAmount;
        public float RemainingRatio => _capacity > 0f ? _remainingAmount / _capacity : 0f;
        public float LowAmountThresholdRatio => _lowAmountThresholdRatio;
        public bool HasRemainingAmount => _remainingAmount > 0f;
        public bool IsDepleted => !HasRemainingAmount;
        public bool IsLowAmount => HasRemainingAmount && RemainingRatio <= _lowAmountThresholdRatio;
        public bool CanSpray => _state.CanSpray && HasRemainingAmount;

        private void Awake()
        {
            OnStateChanged?.Invoke(_state);
            OnRemainingAmountChanged?.Invoke(_remainingAmount, RemainingRatio);
        }

        private void Update()
        {
            if (!CanSpray || _consumptionPerSecond <= 0f) return;
            Consume(_consumptionPerSecond * Time.deltaTime);
        }

        public void SetState(SafetyPinState safetyPin, LeverState lever) => SetState(new FireExtinguisherState(safetyPin, lever));
        public void SetState(FireExtinguisherState state)
        {
            bool couldSpray = CanSpray;
            bool stateChanged = _state != state;
            _state = state;
            if (stateChanged) OnStateChanged?.Invoke(_state);
            NotifyCanSprayChanged(couldSpray);
        }

        public void Consume(float amount)
        {
            if (amount <= 0f) return;
            SetRemainingAmount(_remainingAmount - amount);
        }

        public void Refill() => SetRemainingAmount(_capacity);
        public void SetRemainingRatio(float ratio) => SetRemainingAmount(_capacity * Mathf.Clamp01(ratio));
        public void SetRemainingAmount(float amount)
        {
            float clampedAmount = Mathf.Clamp(amount, 0f, _capacity);
            if (Mathf.Approximately(_remainingAmount, clampedAmount)) return;

            bool couldSpray = CanSpray;
            bool wasDepleted = !HasRemainingAmount;
            bool wasLowAmount = IsLowAmount;
            _remainingAmount = clampedAmount;
            bool isDepleted = !HasRemainingAmount;
            bool isLowAmount = IsLowAmount;

            OnRemainingAmountChanged?.Invoke(_remainingAmount, RemainingRatio);
            if (!wasLowAmount && isLowAmount) OnLowAmount?.Invoke();
            if (!wasDepleted && isDepleted) OnDepleted?.Invoke();
            if (wasDepleted && !isDepleted) OnRefilled?.Invoke();
            NotifyCanSprayChanged(couldSpray);
        }

        public void SetCapacity(float capacity)
        {
            float clampedCapacity = Mathf.Max(0f, capacity);
            if (Mathf.Approximately(_capacity, clampedCapacity)) return;

            bool couldSpray = CanSpray;
            bool wasDepleted = !HasRemainingAmount;
            bool wasLowAmount = IsLowAmount;
            _capacity = clampedCapacity;
            _remainingAmount = Mathf.Clamp(_remainingAmount, 0f, _capacity);
            bool isDepleted = !HasRemainingAmount;
            bool isLowAmount = IsLowAmount;

            OnCapacityChanged?.Invoke(_capacity);
            OnRemainingAmountChanged?.Invoke(_remainingAmount, RemainingRatio);
            if (!wasLowAmount && isLowAmount) OnLowAmount?.Invoke();
            if (!wasDepleted && isDepleted) OnDepleted?.Invoke();
            if (wasDepleted && !isDepleted) OnRefilled?.Invoke();
            NotifyCanSprayChanged(couldSpray);
        }

        private void NotifyCanSprayChanged(bool previousCanSpray)
        {
            if (previousCanSpray != CanSpray) OnCanSprayChanged?.Invoke(CanSpray);
        }

    }
}
