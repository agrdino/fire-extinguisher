using System;
using System.Collections.Generic;
using _Scripts.Environments.EmergencyContact;
using _Scripts.Fires;
using _Scripts.SceneManagement;
using UnityEngine;

namespace _Scripts.Controller
{
    public interface IEnvironmentSceneContext
    {
        SceneId SceneId { get; }
        EnvironmentType EnvironmentType { get; }
        ApplicationState DefaultEntryState { get; }
        Transform PlayerSpawnPoint { get; }
        IReadOnlyList<FireSpawnPoint> FireSpawnPoints { get; }
        IReadOnlyList<EmergencyExitSpawnPoint> ExitSpawnPoints { get; }
        EmergencyContactController EmergencyContactController { get; }
        bool EscapeSmokeEnabled { get; }

        bool TryGetUIAnchor(ApplicationState state, out Transform anchor);
        bool ValidateConfiguration(out string error);
    }

    [Serializable]
    public sealed class SceneUIAnchor
    {
        [SerializeField] private ApplicationState _state;
        [SerializeField] private Transform _anchor;

        public ApplicationState State => _state;
        public Transform Anchor => _anchor;
    }

    [DisallowMultipleComponent]
    public sealed class EnvironmentSceneContext : MonoBehaviour, IEnvironmentSceneContext
    {
        [Header("Identity")]
        [SerializeField] private SceneId _sceneId;
        [SerializeField] private EnvironmentType _environmentType;
        [SerializeField] private ApplicationState _defaultEntryState = ApplicationState.Ready;

        [Header("Scene References")]
        [SerializeField] private Transform _playerSpawnPoint;
        [SerializeField] private Transform _environmentRoot;
        [SerializeField] private EmergencyContactController _emergencyContactController;
        [SerializeField] private List<SceneUIAnchor> _uiAnchors = new();

        [Header("Cached Environment Points")]
        [SerializeField] private List<FireSpawnPoint> _fireSpawnPoints = new();
        [SerializeField] private List<EmergencyExitSpawnPoint> _exitSpawnPoints = new();

        [Header("Environment Effects")]
        [SerializeField] private bool _escapeSmokeEnabled;

        public SceneId SceneId => _sceneId;
        public EnvironmentType EnvironmentType => _environmentType;
        public ApplicationState DefaultEntryState => _defaultEntryState;
        public Transform PlayerSpawnPoint => _playerSpawnPoint;
        public IReadOnlyList<FireSpawnPoint> FireSpawnPoints => _fireSpawnPoints;
        public IReadOnlyList<EmergencyExitSpawnPoint> ExitSpawnPoints => _exitSpawnPoints;
        public EmergencyContactController EmergencyContactController => _emergencyContactController;
        public bool EscapeSmokeEnabled => _escapeSmokeEnabled;

        public bool TryGetUIAnchor(ApplicationState state, out Transform anchor)
        {
            foreach (SceneUIAnchor entry in _uiAnchors)
            {
                if (entry.State != state) continue;

                anchor = entry.Anchor;
                return anchor != null;
            }

            anchor = null;
            return false;
        }

        public bool ValidateConfiguration(out string error)
        {
            if (_environmentRoot == null)
            {
                error = $"{name} requires an Environment Root.";
                return false;
            }

            if (_playerSpawnPoint == null)
            {
                error = $"{name} requires a Player Spawn Point.";
                return false;
            }

            if (_emergencyContactController == null)
            {
                error = $"{name} requires an Emergency Contact Controller.";
                return false;
            }

            var assignedStates = new HashSet<ApplicationState>();
            foreach (SceneUIAnchor entry in _uiAnchors)
            {
                if (entry.Anchor == null)
                {
                    error = $"{name} has no Transform assigned for UI state {entry.State}.";
                    return false;
                }

                if (!assignedStates.Add(entry.State))
                {
                    error = $"{name} has more than one UI anchor for state {entry.State}.";
                    return false;
                }
            }

            error = string.Empty;
            return true;
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            _fireSpawnPoints.RemoveAll(point => point == null);
            _exitSpawnPoints.RemoveAll(point => point == null);
        }

        private void OnDrawGizmos()
        {
            Color previousColor = Gizmos.color;
            Matrix4x4 previousMatrix = Gizmos.matrix;

            if (_playerSpawnPoint != null)
            {
                Gizmos.color = new Color(1f, 0.75f, 0.15f, 0.9f);
                Gizmos.matrix = Matrix4x4.TRS(_playerSpawnPoint.position, _playerSpawnPoint.rotation, Vector3.one);
                Gizmos.DrawWireSphere(Vector3.zero, 0.25f);
                Gizmos.DrawLine(Vector3.zero, Vector3.up * 1.6f);
                Gizmos.DrawWireSphere(Vector3.up * 1.6f, 0.15f);
                DrawForwardArrow();
                UnityEditor.Handles.Label(_playerSpawnPoint.position + Vector3.up * 1.85f, "Player Spawn Point");
            }

            foreach (SceneUIAnchor entry in _uiAnchors)
            {
                if (entry == null || entry.Anchor == null) continue;
                if (entry.State != ApplicationState.Guide && entry.State != ApplicationState.Explore) continue;

                Transform anchor = entry.Anchor;
                bool isGuide = entry.State == ApplicationState.Guide;
                Gizmos.color = isGuide
                    ? new Color(0.2f, 0.75f, 1f, 0.9f)
                    : new Color(0.35f, 1f, 0.4f, 0.9f);
                Gizmos.matrix = Matrix4x4.TRS(anchor.position, anchor.rotation, Vector3.one);
                Gizmos.DrawWireSphere(Vector3.zero, isGuide ? 0.15f : 0.2f);
                Gizmos.DrawWireCube(Vector3.zero, new Vector3(0.8f, 0.45f, 0.02f));
                DrawForwardArrow();
                UnityEditor.Handles.Label(anchor.position + Vector3.up * (isGuide ? 0.4f : 0.65f), $"UI Point - {entry.State}");
            }

            Gizmos.matrix = previousMatrix;
            Gizmos.color = previousColor;
        }

        private static void DrawForwardArrow()
        {
            Vector3 tip = Vector3.forward * 0.6f;
            Gizmos.DrawLine(Vector3.zero, tip);
            Gizmos.DrawLine(tip, tip - Vector3.forward * 0.15f + Vector3.right * 0.1f);
            Gizmos.DrawLine(tip, tip - Vector3.forward * 0.15f - Vector3.right * 0.1f);
        }
#endif
    }
}
