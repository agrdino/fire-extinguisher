using UnityEngine;
using UnityEngine.AI;
using UnityEngine.Rendering;

namespace _Scripts.Controller
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(LineRenderer))]
    public sealed class EmergencyExitPathGuide : MonoBehaviour
    {
        [SerializeField] private LineRenderer _lineRenderer;
        [SerializeField] private Transform _playerGroundMarker;
        [SerializeField, Min(0.01f)] private float _lineWidth = 0.1f;
        [SerializeField, Min(0f)] private float _groundOffset = 0.035f;
        [SerializeField, Min(0f)] private float _markerGroundOffset = 0.01f;
        [SerializeField, Min(0f)] private float _playerPathStartOffset = 0.5f;
        [SerializeField, Min(0f)] private float _exitPathEndOffset = 0.25f;
        [SerializeField, Min(0.01f)] private float _markerPositionSmoothTime = 0.08f;
        [SerializeField, Min(0f)] private float _markerRotationSpeed = 360f;
        [SerializeField, Min(0.1f)] private float _sampleRadius = 2f;
        [SerializeField, Min(0.02f)] private float _refreshInterval = 0.2f;

        private EmergencyExit _emergencyExit;
        private Transform _playerRoot;
        private NavMeshPath _path;
        private float _nextRefreshTime;
        private Vector3 _markerTargetPosition;
        private Vector3 _markerPositionVelocity;
        private float _markerTargetYaw;
        private bool _hasMarkerTarget;
        private bool _isVisible;

        private void Awake()
        {
            _path = new NavMeshPath();
            ConfigureLineRenderer();
        }

        private void OnDisable()
        {
            if (_lineRenderer != null) _lineRenderer.enabled = false;
            SetMarkerVisible(false);
        }

        public void Initialize(EmergencyExit emergencyExit, Transform playerRoot)
        {
            _emergencyExit = emergencyExit;
            _playerRoot = playerRoot;
            SetVisible(false);
        }

        public void SetVisible(bool isVisible)
        {
            _isVisible = isVisible;
            _nextRefreshTime = 0f;

            if (!_isVisible)
            {
                if (_lineRenderer != null) _lineRenderer.enabled = false;
                SetMarkerVisible(false);
                return;
            }

            RefreshPath();
        }

        private void LateUpdate()
        {
            if (!_isVisible) return;

            if (Time.unscaledTime >= _nextRefreshTime)
                RefreshPath();

            SmoothPlayerGroundMarker();
        }

        private void RefreshPath()
        {
            _nextRefreshTime = Time.unscaledTime + _refreshInterval;
            if (_lineRenderer == null || _emergencyExit == null || _playerRoot == null)
            {
                HideGuide();
                return;
            }

            if (!NavMesh.SamplePosition(_playerRoot.position, out NavMeshHit startHit, _sampleRadius,  NavMesh.AllAreas)
                || !NavMesh.SamplePosition(_emergencyExit.transform.position, out NavMeshHit endHit, _sampleRadius, NavMesh.AllAreas)
                || !NavMesh.CalculatePath(startHit.position, endHit.position, NavMesh.AllAreas, _path)
                || _path.status != NavMeshPathStatus.PathComplete)
            {
                HideGuide();
                return;
            }

            Vector3[] corners = _path.corners;
            if (corners.Length < 2)
            {
                HideGuide();
                return;
            }

            // TransformZ makes the ribbon use this transform's Z axis as its normal.
            // Pointing Z upward keeps the line flat on the ground instead of facing the camera.
            transform.rotation = Quaternion.LookRotation(Vector3.up, Vector3.forward);
            _lineRenderer.positionCount = corners.Length;
            for (int i = 0; i < corners.Length; i++)
                _lineRenderer.SetPosition(i, corners[corners.Length - 1 - i] + Vector3.up * _groundOffset);

            Vector3 pathStart = Vector3.MoveTowards(corners[0], corners[1], _playerPathStartOffset);
            Vector3 pathEnd = Vector3.MoveTowards(corners[corners.Length - 1], corners[corners.Length - 2], _exitPathEndOffset);
            _lineRenderer.SetPosition(corners.Length - 1, pathStart + Vector3.up * _groundOffset);
            _lineRenderer.SetPosition(0, pathEnd + Vector3.up * _groundOffset);

            _lineRenderer.enabled = true;
            UpdatePlayerGroundMarker(startHit.position, corners[1]);
        }

        private void ConfigureLineRenderer()
        {
            if (_lineRenderer == null) return;

            _lineRenderer.enabled = false;
            _lineRenderer.useWorldSpace = true;
            _lineRenderer.loop = false;
            _lineRenderer.widthMultiplier = _lineWidth;
            _lineRenderer.alignment = LineAlignment.TransformZ;
        }


        private void UpdatePlayerGroundMarker(Vector3 groundPosition, Vector3 firstCorner)
        {
            if (_playerGroundMarker == null) return;

            Vector3 direction = firstCorner - groundPosition;
            direction.y = 0f;

            _markerTargetPosition = groundPosition + Vector3.up * _markerGroundOffset;
            if (direction.sqrMagnitude > Mathf.Epsilon)
                _markerTargetYaw = Mathf.Atan2(direction.x, direction.z) * Mathf.Rad2Deg;

            if (!_hasMarkerTarget)
            {
                _playerGroundMarker.position = _markerTargetPosition;
                _playerGroundMarker.rotation = Quaternion.Euler(0f, _markerTargetYaw, 0f);
                _markerPositionVelocity = Vector3.zero;
            }

            _hasMarkerTarget = true;
            SetMarkerVisible(true);
        }

        private void SmoothPlayerGroundMarker()
        {
            if (!_hasMarkerTarget || _playerGroundMarker == null) return;

            float deltaTime = Time.unscaledDeltaTime;
            _playerGroundMarker.position = Vector3.SmoothDamp(
                _playerGroundMarker.position,
                _markerTargetPosition,
                ref _markerPositionVelocity,
                _markerPositionSmoothTime,
                Mathf.Infinity,
                deltaTime);

            float yaw = Mathf.MoveTowardsAngle(
                _playerGroundMarker.eulerAngles.y,
                _markerTargetYaw,
                _markerRotationSpeed * deltaTime);
            _playerGroundMarker.rotation = Quaternion.Euler(0f, yaw, 0f);
        }

        private void SetMarkerVisible(bool isVisible)
        {
            if (!isVisible)
            {
                _hasMarkerTarget = false;
                _markerPositionVelocity = Vector3.zero;
            }

            if (_playerGroundMarker != null)
                _playerGroundMarker.gameObject.SetActive(isVisible);
        }

        private void HideGuide()
        {
            if (_lineRenderer != null) _lineRenderer.enabled = false;
            SetMarkerVisible(false);
        }
    }
}
