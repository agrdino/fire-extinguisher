using Unity.XR.CoreUtils;
using UnityEngine;

namespace _Scripts.Controller
{
    /// <summary>
    /// Routes horizontal room-scale tracking movement through a CharacterController.
    /// Without this, walking in the real world only moves the tracked camera and can
    /// leave the XR Origin's collision capsule behind.
    /// </summary>
    [DefaultExecutionOrder(1000)]
    [DisallowMultipleComponent]
    [RequireComponent(typeof(XROrigin), typeof(CharacterController))]
    public sealed class RoomScaleCollisionDriver : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private XROrigin _xrOrigin;
        [SerializeField] private CharacterController _characterController;
        [SerializeField] private Transform _trackingSpace;

        [Header("Tracking")]
        [SerializeField, Min(0f)] private float _minimumHorizontalOffset = 0.001f;
        [SerializeField, Min(0.1f)] private float _trackingJumpThreshold = 0.5f;

        private bool _hasProcessedTrackingPose;

        private void Reset()
        {
            ResolveReferences();
        }

        private void Awake()
        {
            ResolveReferences();
        }

        private void OnEnable()
        {
            Application.onBeforeRender += ProcessRoomScaleMovement;
        }

        private void OnDisable()
        {
            Application.onBeforeRender -= ProcessRoomScaleMovement;
        }

        private void LateUpdate()
        {
            ProcessRoomScaleMovement();
        }

        private void ProcessRoomScaleMovement()
        {
            if (_xrOrigin == null || _characterController == null || _trackingSpace == null)
                return;

            if (!_characterController.enabled || !_characterController.gameObject.activeInHierarchy)
                return;

            Vector3 horizontalOffset = _xrOrigin.CameraInOriginSpacePos;
            horizontalOffset.y = 0f;

            float minimumOffsetSquared = _minimumHorizontalOffset * _minimumHorizontalOffset;
            if (horizontalOffset.sqrMagnitude <= minimumOffsetSquared)
            {
                _hasProcessedTrackingPose = true;
                return;
            }

            Transform origin = _xrOrigin.Origin.transform;
            Vector3 requestedWorldMotion = origin.TransformVector(horizontalOffset);

            // A large one-frame change after startup is normally a tracking recenter,
            // not a physical step. Cancel that jump without moving the player body.
            bool isTrackingJump = _hasProcessedTrackingPose
                && horizontalOffset.sqrMagnitude > _trackingJumpThreshold * _trackingJumpThreshold;

            if (!isTrackingJump)
            {
                Vector3 center = _characterController.center;
                center.x = 0f;
                center.z = 0f;
                _characterController.center = center;
                _characterController.Move(requestedWorldMotion);
            }

            // CharacterController.Move moves the XR Origin as far as collision allows.
            // Removing the full tracked offset from the tracking space makes the HMD
            // and controllers follow that constrained result instead of crossing walls.
            _trackingSpace.position -= requestedWorldMotion;
            _hasProcessedTrackingPose = true;
        }

        private void ResolveReferences()
        {
            if (_xrOrigin == null) _xrOrigin = GetComponent<XROrigin>();
            if (_characterController == null) _characterController = GetComponent<CharacterController>();

            if (_trackingSpace == null && _xrOrigin != null && _xrOrigin.CameraFloorOffsetObject != null)
                _trackingSpace = _xrOrigin.CameraFloorOffsetObject.transform;
        }
    }
}
