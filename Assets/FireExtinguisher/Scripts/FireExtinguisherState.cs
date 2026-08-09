using System;
using UnityEngine;

namespace _Scripts.FireExtinguishers
{
    [Serializable]
    public struct FireExtinguisherState : IEquatable<FireExtinguisherState>
    {
        [SerializeField] private SafetyPinState _safetyPin;
        [SerializeField] private LeverState _lever;

        public FireExtinguisherState(SafetyPinState safetyPin, LeverState lever)
        {
            _safetyPin = safetyPin;
            _lever = lever;
        }

        public SafetyPinState SafetyPin => _safetyPin;
        public LeverState Lever => _lever;

        public bool CanSpray =>
            _safetyPin == SafetyPinState.Removed &&
            _lever == LeverState.Pressed;

        public static FireExtinguisherState DefaultState =>
            new FireExtinguisherState(
                SafetyPinState.Inserted,
                LeverState.Released);

        public FireExtinguisherState WithLever(LeverState lever)
        {
            return new FireExtinguisherState(_safetyPin, lever);
        }

        public bool Equals(FireExtinguisherState other)
        {
            return _safetyPin == other._safetyPin && _lever == other._lever;
        }

        public override bool Equals(object obj)
        {
            return obj is FireExtinguisherState other && Equals(other);
        }

        public override int GetHashCode()
        {
            return ((int)_safetyPin * 397) ^ (int)_lever;
        }

        public static bool operator ==(
            FireExtinguisherState left,
            FireExtinguisherState right)
        {
            return left.Equals(right);
        }

        public static bool operator !=(
            FireExtinguisherState left,
            FireExtinguisherState right)
        {
            return !left.Equals(right);
        }
    }
}
