using System;
using UnityEngine;
using UnityEngine.InputSystem;

namespace _Scripts.FireExtinguishers
{
    [CreateAssetMenu(menuName = "Fire Extinguisher/Input Settings", fileName = "FireExtinguisherInputSettings")]
    public sealed class SO_FireExtinguisherInputSettings : ScriptableObject
    {
        [Header("Keyboard Bindings")]
        [SerializeField] private Key _defaultKey = Key.A;
        [SerializeField] private FireExtinguisherKeyBinding[] _bindings =
        {
            new(Key.A, SafetyPinState.Inserted, LeverState.Released),
            new(Key.B, SafetyPinState.Removed, LeverState.Released),
            new(Key.C, SafetyPinState.Removed, LeverState.Pressed),
            new(Key.D, SafetyPinState.Inserted,  LeverState.Pressed)
        };

        public Key DefaultKey => _defaultKey;

        public bool TryGetState(Key key, out FireExtinguisherState state)
        {
            state = FireExtinguisherState.DefaultState;
            if (_bindings == null) return false;
            
            for (int i = 0; i < _bindings.Length; i++)
            {
                if (_bindings[i].Key != key) continue;
                state = _bindings[i].State;
                return true;
            }
            
            return false;
        }
    }

    [Serializable]
    public struct FireExtinguisherKeyBinding
    {
        [SerializeField] private Key _key;
        [SerializeField] private SafetyPinState _safetyPin;
        [SerializeField] private LeverState _lever;

        public FireExtinguisherKeyBinding(Key key, SafetyPinState safetyPin, LeverState lever)
        {
            _key = key;
            _safetyPin = safetyPin;
            _lever = lever;
        }

        public readonly Key Key => _key;
        public readonly FireExtinguisherState State => new(_safetyPin, _lever);
    }

}
