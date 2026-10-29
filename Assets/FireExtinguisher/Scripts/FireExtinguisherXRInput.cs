using UnityEngine;
using UnityEngine.InputSystem;

namespace _Scripts.FireExtinguishers
{
    [DisallowMultipleComponent]
    public sealed class FireExtinguisherXRInput : MonoBehaviour
    {
        private const string RightTriggerBinding =
            "<XRController>{RightHand}/{Trigger}";
        private const string RightThumbBinding =
            "<XRController>{RightHand}/{Primary2DAxisClick}";

        [SerializeField] private FireExtinguisherController _controller;
        [SerializeField, Range(0.01f, 1f)] private float _triggerPressThreshold = 0.5f;

        private InputAction _triggerAction;
        private InputAction _thumbAction;
        private bool _wasTriggerPressed;
        private bool _hasReceivedXRInput;

        private void Reset()
        {
            ResolveController();
        }

        private void Awake()
        {
            ResolveController();
            CreateInputActions();
        }

        private void OnEnable()
        {
            CreateInputActions();
            _triggerAction.Enable();
            _thumbAction.Enable();
        }

        private void Start()
        {
            ResolveController();
            if (_controller != null) return;

            enabled = false;
        }

        private void Update()
        {
            if (_controller == null) return;

            bool isTriggerPressed =
                _triggerAction.ReadValue<float>() >= _triggerPressThreshold;
            bool didTriggerChange = isTriggerPressed != _wasTriggerPressed;
            bool didPressThumb = _thumbAction.WasPressedThisFrame();

            if (didTriggerChange || didPressThumb)
            {
                _controller.ReceiveXRControls(isTriggerPressed, didPressThumb);
                _hasReceivedXRInput = true;
                _wasTriggerPressed = isTriggerPressed;
                return;
            }

            if (_hasReceivedXRInput)
                _controller.RefreshXRInputConnection();
        }

        private void OnDisable()
        {
            if (_wasTriggerPressed && _controller != null)
                _controller.ReceiveXRControls(false, false);

            _wasTriggerPressed = false;
            _hasReceivedXRInput = false;
            _triggerAction?.Disable();
            _thumbAction?.Disable();
        }

        private void OnDestroy()
        {
            _triggerAction?.Dispose();
            _thumbAction?.Dispose();
        }

        private void ResolveController()
        {
            if (_controller != null) return;

            _controller = GetComponentInParent<FireExtinguisherController>();
            if (_controller == null) _controller = FireExtinguisherController.Instance;
            if (_controller == null)
                _controller = FindFirstObjectByType<FireExtinguisherController>();
        }

        private void CreateInputActions()
        {
            if (_triggerAction == null)
            {
                _triggerAction = new InputAction(
                    "Fire Extinguisher Trigger",
                    InputActionType.Value,
                    RightTriggerBinding,
                    expectedControlType: "Axis");
            }

            if (_thumbAction == null)
            {
                _thumbAction = new InputAction(
                    "Fire Extinguisher Thumb",
                    InputActionType.Button,
                    RightThumbBinding,
                    expectedControlType: "Button");
            }
        }
    }
}
