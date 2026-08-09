using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;
using UnityEngine.InputSystem.Utilities;

namespace _Scripts.FireExtinguishers
{
    [DisallowMultipleComponent]
    public sealed class FireExtinguisherKeyboardInput : MonoBehaviour
    {
        [SerializeField] private FireExtinguisherController _controller;

        private void Reset()
        {
            _controller = GetComponent<FireExtinguisherController>();

            if (_controller == null)
                _controller = FireExtinguisherController.Instance;
        }

        private void Start()
        {
            if (_controller == null) _controller = FireExtinguisherController.Instance;
            if (_controller == null) _controller = FindFirstObjectByType<FireExtinguisherController>();
            if (_controller == null)
            {
                Debug.LogError("FireExtinguisherKeyboardSimulator requires a FireExtinguisherController.", this);
                enabled = false;
                return;
            }
        }

        private void Update()
        {
            if (_controller == null) return;

            Keyboard keyboard = Keyboard.current;
            if (keyboard == null) return;

            ReadOnlyArray<KeyControl> keys = keyboard.allKeys;
            for (int i = 0; i < keys.Count; i++)
            {
                KeyControl keyControl = keys[i];
                if (!keyControl.wasPressedThisFrame) continue;
                if (_controller.TryReceiveKey(keyControl.keyCode)) return;
            }
        }

    }
}
