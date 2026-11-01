using UnityEngine;
using UnityEngine.InputSystem;

namespace _Scripts.FireExtinguishers
{
    [DisallowMultipleComponent]
    public sealed class FireExtinguisherXRInput : MonoBehaviour
    {
        private const string RightTriggerBinding =
            "<XRController>{RightHand}/{TriggerButton}";
        private const string RightAButtonBinding =
            "<XRController>{RightHand}/{PrimaryButton}";
        private const string RightBButtonBinding =
            "<XRController>{RightHand}/{SecondaryButton}";

        [SerializeField] private FireExtinguisherController _controller;

        private InputAction _triggerAction;
        private InputAction _aButtonAction;
        private InputAction _bButtonAction;
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
            _aButtonAction.Enable();
            _bButtonAction.Enable();
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

            bool isTriggerPressed = _triggerAction.IsPressed();
            bool didTriggerChange = isTriggerPressed != _wasTriggerPressed;
            bool didPressA = _aButtonAction.WasPressedThisFrame();
            bool didPressB = _bButtonAction.WasPressedThisFrame();

            if (didTriggerChange || didPressA || didPressB)
            {
                _controller.ReceiveXRControls(
                    isTriggerPressed,
                    didPressA,
                    didPressB);
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
                _controller.ReceiveXRControls(false, false, false);

            _wasTriggerPressed = false;
            _hasReceivedXRInput = false;
            _triggerAction?.Disable();
            _aButtonAction?.Disable();
            _bButtonAction?.Disable();
        }

        private void OnDestroy()
        {
            _triggerAction?.Dispose();
            _aButtonAction?.Dispose();
            _bButtonAction?.Dispose();
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
                    InputActionType.Button,
                    RightTriggerBinding,
                    expectedControlType: "Button");
            }

            if (_aButtonAction == null)
            {
                _aButtonAction = new InputAction(
                    "Fire Extinguisher Remove Safety Pin (A)",
                    InputActionType.Button,
                    RightAButtonBinding,
                    expectedControlType: "Button");
            }

            if (_bButtonAction == null)
            {
                _bButtonAction = new InputAction(
                    "Fire Extinguisher Insert Safety Pin (B)",
                    InputActionType.Button,
                    RightBButtonBinding,
                    expectedControlType: "Button");
            }
        }
    }
}
