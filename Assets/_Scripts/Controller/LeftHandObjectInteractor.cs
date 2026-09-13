using _Scripts.Environments.Factory;
using UnityEngine;

namespace _Scripts.Controller
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(HandController))]
    public sealed class LeftHandObjectInteractor : MonoBehaviour
    {
        [SerializeField, Min(0.1f)] private float _maximumDistance = 2f;
        [SerializeField] private LayerMask _interactionMask = 1 << 9;
        [SerializeField, Range(0f, 1f)] private float _pressThreshold = 0.5f;

        private HandController _handController;
        private FactoryEmergencyInteractable _hoveredInteractable;
        private bool _wasPressed;

        private void Awake()
        {
            _handController = GetComponent<HandController>();
        }

        private void OnEnable()
        {
            if (_handController != null) _handController.onPress += HandlePress;
        }

        private void OnDisable()
        {
            if (_handController != null) _handController.onPress -= HandlePress;
            SetHoveredInteractable(null);
            _wasPressed = false;
        }

        private void Update()
        {
            ApplicationManager applicationManager = ApplicationManager.Instance;
            if (applicationManager == null || !applicationManager.IsFactoryResponding)
            {
                SetHoveredInteractable(null);
                return;
            }

            FactoryEmergencyInteractable candidate = null;
            if (Physics.Raycast(transform.position, transform.forward, out RaycastHit hit, _maximumDistance, _interactionMask, QueryTriggerInteraction.Collide))
            {
                FactoryEmergencyInteractable interactable = hit.collider.GetComponentInParent<FactoryEmergencyInteractable>();
                if (interactable != null && interactable.IsInteractionEnabled) candidate = interactable;
            }

            SetHoveredInteractable(candidate);
        }

        private void HandlePress(float value)
        {
            bool isPressed = value >= _pressThreshold;
            if (isPressed && !_wasPressed && _hoveredInteractable != null)
                _hoveredInteractable.TryActivate();
            _wasPressed = isPressed;
        }

        private void SetHoveredInteractable(FactoryEmergencyInteractable interactable)
        {
            if (_hoveredInteractable == interactable) return;
            if (_hoveredInteractable != null) _hoveredInteractable.SetHovered(false);
            _hoveredInteractable = interactable;
            if (_hoveredInteractable != null) _hoveredInteractable.SetHovered(true);
        }
    }
}
