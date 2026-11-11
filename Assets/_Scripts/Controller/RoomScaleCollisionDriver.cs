using System.Collections.Generic;
using Unity.XR.CoreUtils;
using _Scripts.SceneManagement;
using UnityEngine;
using UnityEngine.XR;

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

        private readonly List<XRInputSubsystem> _availableInputSubsystems = new();
        private readonly List<XRInputSubsystem> _subscribedInputSubsystems = new();

        private bool _hasProcessedTrackingPose;
        private bool _hasStartBoundaryCenter;
        private Vector3 _startBoundaryCenter;

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
            RefreshInputSubsystemSubscriptions();

            if (ApplicationManager.Instance != null)
            {
                ApplicationManager.Instance.OnEnvironmentBound += HandleEnvironmentBound;
                ApplicationManager.Instance.OnStateChanged += HandleApplicationStateChanged;
                TryCaptureStartBoundaryCenter();
            }
        }

        private void OnDisable()
        {
            Application.onBeforeRender -= ProcessRoomScaleMovement;

            if (ApplicationManager.Instance != null)
            {
                ApplicationManager.Instance.OnEnvironmentBound -= HandleEnvironmentBound;
                ApplicationManager.Instance.OnStateChanged -= HandleApplicationStateChanged;
            }

            for (int index = 0; index < _subscribedInputSubsystems.Count; index++)
            {
                XRInputSubsystem subsystem = _subscribedInputSubsystems[index];
                if (subsystem != null)
                    subsystem.trackingOriginUpdated -= HandleTrackingOriginUpdated;
            }

            _subscribedInputSubsystems.Clear();
        }

        private void LateUpdate()
        {
            RefreshInputSubsystemSubscriptions();
            ProcessRoomScaleMovement();
        }

        private void ProcessRoomScaleMovement()
        {
            if (_xrOrigin == null || _characterController == null || _trackingSpace == null)
                return;

            if (!_characterController.enabled || !_characterController.gameObject.activeInHierarchy)
                return;

            Transform origin = _xrOrigin.Origin.transform;
            bool constrainToStartBoundary = IsStartBoundaryActive;
            if (constrainToStartBoundary)
                ClampOriginToStartBoundary(origin);

            Vector3 horizontalOffset = _xrOrigin.CameraInOriginSpacePos;
            horizontalOffset.y = 0f;

            float minimumOffsetSquared = _minimumHorizontalOffset * _minimumHorizontalOffset;
            if (horizontalOffset.sqrMagnitude <= minimumOffsetSquared)
            {
                _hasProcessedTrackingPose = true;
                return;
            }

            Vector3 requestedWorldMotion = origin.TransformVector(horizontalOffset);

            // A large one-frame change after startup is normally a tracking recenter,
            // not a physical step. Cancel that jump without moving the player body.
            bool isTrackingJump = _hasProcessedTrackingPose
                && horizontalOffset.sqrMagnitude > _trackingJumpThreshold * _trackingJumpThreshold;

            if (isTrackingJump && constrainToStartBoundary)
            {
                // Meta's system Reset View recenters the XR tracking origin. In the
                // Start scene, use that signal to return the player root to the saved
                // spawn center as well. Leave other scenes' recenter behavior intact.
                RestoreStartBoundaryCenter(origin);
                _hasProcessedTrackingPose = true;
                return;
            }

            if (!isTrackingJump)
            {
                Vector3 center = _characterController.center;
                center.x = 0f;
                center.z = 0f;
                _characterController.center = center;
                Vector3 allowedMotion = constrainToStartBoundary
                    ? ConstrainMotionToStartBoundary(origin, requestedWorldMotion)
                    : requestedWorldMotion;
                _characterController.Move(allowedMotion);
            }

            // CharacterController.Move moves the XR Origin as far as collision allows.
            // Removing the full tracked offset from the tracking space makes the HMD
            // and controllers follow that constrained result instead of crossing walls.
            _trackingSpace.position -= requestedWorldMotion;
            _hasProcessedTrackingPose = true;
        }

        private bool IsStartBoundaryActive =>
            _hasStartBoundaryCenter
            && ApplicationManager.Instance != null
            && ApplicationManager.Instance.CurrentEnvironment?.SceneId == SceneId.Start;

        private void HandleEnvironmentBound(IEnvironmentSceneContext _)
        {
            // BindEnvironment raises this before placing the XR Origin at the new
            // scene's spawn point. Capture the Start center after its state is set.
            _hasStartBoundaryCenter = false;
        }

        private void HandleApplicationStateChanged(ApplicationState _)
        {
            TryCaptureStartBoundaryCenter();
        }

        private void TryCaptureStartBoundaryCenter()
        {
            if (_hasStartBoundaryCenter
                || ApplicationManager.Instance == null
                || ApplicationManager.Instance.CurrentEnvironment?.SceneId != SceneId.Start
                || _xrOrigin == null
                || _xrOrigin.Origin == null)
                return;

            _startBoundaryCenter = _xrOrigin.Origin.transform.position;
            _hasStartBoundaryCenter = true;
        }

        private void HandleTrackingOriginUpdated(XRInputSubsystem _)
        {
            if (!IsStartBoundaryActive || _xrOrigin?.Origin == null)
                return;

            // The system-level Meta button recenter updates the XR tracking origin.
            RestoreStartBoundaryCenter(_xrOrigin.Origin.transform);
        }

        private void ClampOriginToStartBoundary(Transform origin)
        {
            Vector3 position = origin.position;
            Vector2 offset = new Vector2(
                position.x - _startBoundaryCenter.x,
                position.z - _startBoundaryCenter.z);
            float radius = CurrentStartBoundaryRadius;
            float radiusSquared = radius * radius;
            if (offset.sqrMagnitude <= radiusSquared)
                return;

            Vector2 clampedOffset = offset.normalized * radius;
            position.x = _startBoundaryCenter.x + clampedOffset.x;
            position.z = _startBoundaryCenter.z + clampedOffset.y;
            SetOriginPosition(origin, position);
        }

        private Vector3 ConstrainMotionToStartBoundary(Transform origin, Vector3 requestedMotion)
        {
            Vector3 currentPosition = origin.position;
            Vector3 targetPosition = currentPosition + requestedMotion;
            Vector2 offset = new Vector2(
                targetPosition.x - _startBoundaryCenter.x,
                targetPosition.z - _startBoundaryCenter.z);
            float radius = CurrentStartBoundaryRadius;
            float radiusSquared = radius * radius;
            if (offset.sqrMagnitude <= radiusSquared)
                return requestedMotion;

            Vector2 clampedOffset = offset.normalized * radius;
            targetPosition.x = _startBoundaryCenter.x + clampedOffset.x;
            targetPosition.z = _startBoundaryCenter.z + clampedOffset.y;
            return targetPosition - currentPosition;
        }

        private float CurrentStartBoundaryRadius =>
            ApplicationManager.Instance?.CurrentEnvironment?.StartBoundaryRadius ?? 1f;

        private void RestoreStartBoundaryCenter(Transform origin)
        {
            SetOriginPosition(origin, _startBoundaryCenter);
        }

        private void SetOriginPosition(Transform origin, Vector3 position)
        {
            bool wasEnabled = _characterController.enabled;
            if (wasEnabled) _characterController.enabled = false;
            origin.position = position;
            if (wasEnabled) _characterController.enabled = true;
        }

        private void RefreshInputSubsystemSubscriptions()
        {
            _availableInputSubsystems.Clear();
            SubsystemManager.GetSubsystems(_availableInputSubsystems);

            for (int index = _subscribedInputSubsystems.Count - 1; index >= 0; index--)
            {
                XRInputSubsystem subsystem = _subscribedInputSubsystems[index];
                if (subsystem != null
                    && subsystem.running
                    && _availableInputSubsystems.Contains(subsystem))
                    continue;

                if (subsystem != null)
                    subsystem.trackingOriginUpdated -= HandleTrackingOriginUpdated;
                _subscribedInputSubsystems.RemoveAt(index);
            }

            for (int index = 0; index < _availableInputSubsystems.Count; index++)
            {
                XRInputSubsystem subsystem = _availableInputSubsystems[index];
                if (subsystem == null
                    || !subsystem.running
                    || _subscribedInputSubsystems.Contains(subsystem))
                    continue;

                subsystem.trackingOriginUpdated += HandleTrackingOriginUpdated;
                _subscribedInputSubsystems.Add(subsystem);
            }
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
