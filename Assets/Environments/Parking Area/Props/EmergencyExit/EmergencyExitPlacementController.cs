using System.Collections.Generic;
using UnityEngine;

namespace _Scripts.Controller
{
    [DisallowMultipleComponent]
    public sealed class EmergencyExitPlacementController : MonoBehaviour
    {
        [SerializeField] private EmergencyExit _emergencyExit;
        [SerializeField, Min(0f)] private float _minimumDistanceFromPlayer = 2f;
        [SerializeField, Range(-1f, 1f)] private float _behindPlayerDotThreshold;

        private IEnvironmentSceneContext _environment;

        public EmergencyExit EmergencyExit => _emergencyExit;
        public EmergencyExitSpawnPoint SelectedSpawnPoint { get; private set; }

        public void BindEnvironment(IEnvironmentSceneContext environment)
        {
            _environment = environment;
            SelectedSpawnPoint = null;
        }

        public bool TryPosition(Transform playerView)
        {
            if (_emergencyExit == null || _environment == null || playerView == null) return false;

            SelectedSpawnPoint = GetPreferredSpawnPoint(
                _environment.ExitSpawnPoints,
                playerView.position,
                playerView.forward);
            if (SelectedSpawnPoint == null) return false;

            Transform marker = SelectedSpawnPoint.transform;
            _emergencyExit.transform.SetPositionAndRotation(marker.position, marker.rotation);
            return true;
        }

        private EmergencyExitSpawnPoint GetPreferredSpawnPoint(
            IReadOnlyList<EmergencyExitSpawnPoint> spawnPoints,
            Vector3 playerPosition,
            Vector3 playerForward)
        {
            if (spawnPoints == null || spawnPoints.Count == 0) return null;

            playerForward.y = 0f;
            if (playerForward.sqrMagnitude < 0.0001f) playerForward = Vector3.forward;
            else playerForward.Normalize();

            float minimumDistanceSquared = _minimumDistanceFromPlayer * _minimumDistanceFromPlayer;
            bool requirePointBehindPlayer = HasPointBehindPlayer(
                spawnPoints,
                playerPosition,
                playerForward,
                minimumDistanceSquared);

            float totalWeight = 0f;
            for (int i = 0; i < spawnPoints.Count; i++)
            {
                EmergencyExitSpawnPoint spawnPoint = spawnPoints[i];
                if (!TryGetOffset(spawnPoint, playerPosition, minimumDistanceSquared, out Vector3 offset))
                    continue;
                if (requirePointBehindPlayer && !IsBehindPlayer(offset, playerForward)) continue;

                totalWeight += Mathf.Max(offset.sqrMagnitude, 0.0001f);
            }

            if (totalWeight <= 0f) return null;

            float selection = Random.value * totalWeight;
            EmergencyExitSpawnPoint lastValidPoint = null;
            for (int i = 0; i < spawnPoints.Count; i++)
            {
                EmergencyExitSpawnPoint spawnPoint = spawnPoints[i];
                if (!TryGetOffset(spawnPoint, playerPosition, minimumDistanceSquared, out Vector3 offset))
                    continue;
                if (requirePointBehindPlayer && !IsBehindPlayer(offset, playerForward)) continue;

                lastValidPoint = spawnPoint;
                selection -= Mathf.Max(offset.sqrMagnitude, 0.0001f);
                if (selection <= 0f) return spawnPoint;
            }

            return lastValidPoint;
        }

        private bool HasPointBehindPlayer(
            IReadOnlyList<EmergencyExitSpawnPoint> spawnPoints,
            Vector3 playerPosition,
            Vector3 playerForward,
            float minimumDistanceSquared)
        {
            for (int i = 0; i < spawnPoints.Count; i++)
            {
                if (!TryGetOffset(spawnPoints[i], playerPosition, minimumDistanceSquared, out Vector3 offset))
                    continue;
                if (IsBehindPlayer(offset, playerForward)) return true;
            }

            return false;
        }

        private bool IsBehindPlayer(Vector3 offset, Vector3 playerForward)
        {
            return offset.sqrMagnitude >= 0.0001f
                && Vector3.Dot(offset.normalized, playerForward) <= _behindPlayerDotThreshold;
        }

        private static bool TryGetOffset(
            EmergencyExitSpawnPoint spawnPoint,
            Vector3 playerPosition,
            float minimumDistanceSquared,
            out Vector3 offset)
        {
            offset = Vector3.zero;
            if (spawnPoint == null || !spawnPoint.gameObject.activeInHierarchy) return false;

            offset = spawnPoint.transform.position - playerPosition;
            offset.y = 0f;
            return offset.sqrMagnitude >= minimumDistanceSquared;
        }
    }
}
