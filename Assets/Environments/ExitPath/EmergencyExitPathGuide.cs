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
        [SerializeField, Min(0.01f)] private float _lineWidth = 0.1f;
        [SerializeField, Min(0f)] private float _groundOffset = 0.035f;
        [SerializeField, Min(0.1f)] private float _sampleRadius = 2f;
        [SerializeField, Min(0.02f)] private float _refreshInterval = 0.2f;

        private EmergencyExit _emergencyExit;
        private Transform _playerRoot;
        private NavMeshPath _path;
        private float _nextRefreshTime;
        private bool _isVisible;

        private void Awake()
        {
            _path = new NavMeshPath();
            ConfigureLineRenderer();
        }

        private void OnDisable()
        {
            if (_lineRenderer != null) _lineRenderer.enabled = false;
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
                return;
            }

            RefreshPath();
        }

        private void LateUpdate()
        {
            if (!_isVisible || Time.unscaledTime < _nextRefreshTime) return;
            RefreshPath();
        }

        private void RefreshPath()
        {
            _nextRefreshTime = Time.unscaledTime + _refreshInterval;
            if (_lineRenderer == null || _emergencyExit == null || _playerRoot == null)
            {
                HideLine();
                return;
            }

            if (!NavMesh.SamplePosition(_playerRoot.position, out NavMeshHit startHit, _sampleRadius,  NavMesh.AllAreas)
                || !NavMesh.SamplePosition(_emergencyExit.transform.position, out NavMeshHit endHit, _sampleRadius, NavMesh.AllAreas)
                || !NavMesh.CalculatePath(startHit.position, endHit.position, NavMesh.AllAreas, _path)
                || _path.status != NavMeshPathStatus.PathComplete)
            {
                HideLine();
                return;
            }

            Vector3[] corners = _path.corners;
            if (corners.Length < 2)
            {
                HideLine();
                return;
            }

            // TransformZ makes the ribbon use this transform's Z axis as its normal.
            // Pointing Z upward keeps the line flat on the ground instead of facing the camera.
            transform.rotation = Quaternion.LookRotation(Vector3.up, Vector3.forward);
            _lineRenderer.positionCount = corners.Length;
            for (int i = 0; i < corners.Length; i++)
                _lineRenderer.SetPosition(i, corners[corners.Length - 1 - i] + Vector3.up * _groundOffset);

            _lineRenderer.enabled = true;
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


        private void HideLine()
        {
            if (_lineRenderer != null) _lineRenderer.enabled = false;
        }
    }
}
