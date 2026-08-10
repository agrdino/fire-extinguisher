using System;
using UnityEngine;

namespace _Scripts.FireExtinguishers
{
    [DisallowMultipleComponent]
    public sealed class FireExtinguisher : MonoBehaviour
    {
        [SerializeField] private FireExtinguisherState _state;

        public event Action<FireExtinguisherState> OnStateChanged;

        public FireExtinguisherState CurrentState => _state;

        private void Awake()
        {
            OnStateChanged?.Invoke(_state);
        }

        public void ApplyState(FireExtinguisherState state)
        {
            bool stateChanged = _state != state;
            _state = state;
            if (stateChanged) OnStateChanged?.Invoke(_state);
        }

        public void ApplyState(SafetyPinState safetyPin, LeverState lever)
        {
            ApplyState(new FireExtinguisherState(safetyPin, lever));
        }

    }
}
