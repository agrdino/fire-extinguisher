using System;
using System.Collections.Generic;
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
        [SerializeField] private List<SceneUIAnchor> _uiAnchors = new();

        [Header("Cached Environment Points")]
        [SerializeField] private List<FireSpawnPoint> _fireSpawnPoints = new();
        [SerializeField] private List<EmergencyExitSpawnPoint> _exitSpawnPoints = new();

        public SceneId SceneId => _sceneId;
        public EnvironmentType EnvironmentType => _environmentType;
        public ApplicationState DefaultEntryState => _defaultEntryState;
        public Transform PlayerSpawnPoint => _playerSpawnPoint;
        public IReadOnlyList<FireSpawnPoint> FireSpawnPoints => _fireSpawnPoints;
        public IReadOnlyList<EmergencyExitSpawnPoint> ExitSpawnPoints => _exitSpawnPoints;

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

    }
}
