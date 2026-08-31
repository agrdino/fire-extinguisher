using System;
using System.Collections.Generic;
using _Scripts.Fires;
using UnityEngine;

namespace _Scripts.Controller
{
    public enum EnvironmentType
    {
        Start,
        Factory,
        Park
    }

    [Serializable]
    public sealed class UIStatePoint
    {
        [SerializeField] private ApplicationState _state;
        [SerializeField] private Transform _point;

        public ApplicationState State => _state;
        public Transform Point => _point;

        public UIStatePoint(ApplicationState state)
        {
            _state = state;
        }
    }

    [Serializable]
    public sealed class EnvironmentUIPoints
    {
        [SerializeField] private EnvironmentType _Environment;
        [SerializeField] private List<UIStatePoint> _points = new();

        public EnvironmentType Environment => _Environment;

        public EnvironmentUIPoints(EnvironmentType Environment)
        {
            _Environment = Environment;
        }

        public bool TryGetPoint(ApplicationState state, out Transform point)
        {
            foreach (UIStatePoint statePoint in _points)
            {
                if (statePoint.State != state) continue;

                point = statePoint.Point;
                return point != null;
            }

            point = null;
            return false;
        }

        public void EnsureState(ApplicationState state)
        {
            foreach (UIStatePoint statePoint in _points)
                if (statePoint.State == state)
                    return;

            _points.Add(new UIStatePoint(state));
        }
    }

    [DefaultExecutionOrder(-102)]
    [DisallowMultipleComponent]
    public sealed class EnvironmentController : MonoBehaviour
    {
        private static EnvironmentController _instance;
        public static EnvironmentController Instance => _instance;

        [Header("Environments")]
        [SerializeField] private GameObject _startEnvironment;
        [SerializeField] private GameObject _factoryEnvironment;
        [SerializeField] private GameObject _parkEnvironment;

        [Header("Exit Markers")]
        [SerializeField] private Transform _factoryExit;
        [SerializeField] private Transform _parkExit;
        [SerializeField] private EmergencyExit _emergencyExit;
        [SerializeField, Min(0f)] private float _minimumExitDistanceFromPlayer = 2f;
        [SerializeField, Range(-1f, 1f)] private float _behindPlayerDotThreshold = 0f;

        [Header("Exit Runtime")]
        [SerializeField] private EmergencyExitSpawnPoint _selectedExitSpawnPoint;

        [Header("UI Points")]
        [Tooltip("Start only needs a Start point. Factory and Park need one point for each gameplay UI state.")]
        [SerializeField] private List<EnvironmentUIPoints> _uiPoints = new();

        [Header("Runtime")]
        [SerializeField] private EnvironmentType _currentEnvironment = EnvironmentType.Start;

        public EnvironmentType CurrentEnvironment => _currentEnvironment;
        public event Action<EnvironmentType> OnEnvironmentChanged;

        private void Awake()
        {
            if (_instance != null && _instance != this)
            {
                Destroy(gameObject);
                return;
            }

            _instance = this;
            ResolveEnvironmentReferences();
            ShowStartEnvironment();
        }

        private void OnDestroy()
        {
            if (_instance == this) _instance = null;
        }

        public void ShowStartEnvironment() => SetEnvironment(EnvironmentType.Start);
        public void SelectFactory() => SetEnvironment(EnvironmentType.Factory);
        public void SelectPark() => SetEnvironment(EnvironmentType.Park);

        public void SetEnvironment(EnvironmentType EnvironmentType)
        {
            _currentEnvironment = EnvironmentType;

            SetActive(_startEnvironment, EnvironmentType == EnvironmentType.Start);
            SetActive(_factoryEnvironment, EnvironmentType == EnvironmentType.Factory);
            SetActive(_parkEnvironment, EnvironmentType == EnvironmentType.Park);

            OnEnvironmentChanged?.Invoke(_currentEnvironment);
        }

        public bool PositionEmergencyExit(Transform playerView)
        {
            if (_emergencyExit == null || playerView == null) return false;

            _selectedExitSpawnPoint = GetPreferredExitSpawnPoint(playerView.position, playerView.forward);
            Transform exitMarker = _selectedExitSpawnPoint != null
                ? _selectedExitSpawnPoint.transform
                : GetLegacyExitMarker(_currentEnvironment);
            if (exitMarker == null) return false;

            _emergencyExit.transform.SetPositionAndRotation(exitMarker.position, exitMarker.rotation);
            return true;
        }

        public bool TryGetUIPoint(EnvironmentType EnvironmentType, ApplicationState state, out Transform point)
        {
            if (state == ApplicationState.Completed
                && EnvironmentType == _currentEnvironment
                && _selectedExitSpawnPoint != null
                && _selectedExitSpawnPoint.CompleteUIPoint != null)
            {
                point = _selectedExitSpawnPoint.CompleteUIPoint;
                return true;
            }

            foreach (EnvironmentUIPoints EnvironmentPoints in _uiPoints)
            {
                if (EnvironmentPoints.Environment != EnvironmentType) continue;
                if (EnvironmentPoints.TryGetPoint(state, out point)) return true;

                break;
            }

            GameObject Environment = GetEnvironment(EnvironmentType);
            Transform uiPointsRoot = Environment != null ? Environment.transform.Find("UI Points") : null;
            point = FindUIPoint(uiPointsRoot, state);
            return point != null;
        }

        public IReadOnlyList<FireSpawnPoint> GetActiveFireSpawnPoints()
        {
            GameObject activeEnvironment = GetEnvironment(_currentEnvironment);
            if (activeEnvironment == null || _currentEnvironment == EnvironmentType.Start)
                return Array.Empty<FireSpawnPoint>();

            return activeEnvironment.GetComponentsInChildren<FireSpawnPoint>(true);
        }

        public IReadOnlyList<EmergencyExitSpawnPoint> GetActiveExitSpawnPoints()
        {
            GameObject activeEnvironment = GetEnvironment(_currentEnvironment);
            if (activeEnvironment == null || _currentEnvironment == EnvironmentType.Start)
                return Array.Empty<EmergencyExitSpawnPoint>();

            return activeEnvironment.GetComponentsInChildren<EmergencyExitSpawnPoint>(true);
        }

        private EmergencyExitSpawnPoint GetPreferredExitSpawnPoint(
            Vector3 playerPosition,
            Vector3 playerForward)
        {
            IReadOnlyList<EmergencyExitSpawnPoint> spawnPoints = GetActiveExitSpawnPoints();
            if (spawnPoints.Count == 0) return null;

            playerForward.y = 0f;
            if (playerForward.sqrMagnitude < 0.0001f) playerForward = Vector3.forward;
            else playerForward.Normalize();

            float minimumDistanceSquared = _minimumExitDistanceFromPlayer * _minimumExitDistanceFromPlayer;
            bool hasPointBehindPlayer = false;
            for (int i = 0; i < spawnPoints.Count; i++)
            {
                EmergencyExitSpawnPoint spawnPoint = spawnPoints[i];
                if (!TryGetExitOffset(spawnPoint, playerPosition, minimumDistanceSquared, out Vector3 offset))
                    continue;

                if (IsBehindPlayer(offset, playerForward))
                {
                    hasPointBehindPlayer = true;
                    break;
                }
            }

            float totalWeight = 0f;
            for (int i = 0; i < spawnPoints.Count; i++)
            {
                EmergencyExitSpawnPoint spawnPoint = spawnPoints[i];
                if (!TryGetExitOffset(spawnPoint, playerPosition, minimumDistanceSquared, out Vector3 offset))
                    continue;
                if (hasPointBehindPlayer && !IsBehindPlayer(offset, playerForward)) continue;

                // Squared distance keeps the choice random while favoring farther exits strongly.
                totalWeight += Mathf.Max(offset.sqrMagnitude, 0.0001f);
            }

            if (totalWeight <= 0f) return null;

            float selection = UnityEngine.Random.value * totalWeight;
            EmergencyExitSpawnPoint lastValidPoint = null;
            for (int i = 0; i < spawnPoints.Count; i++)
            {
                EmergencyExitSpawnPoint spawnPoint = spawnPoints[i];
                if (!TryGetExitOffset(spawnPoint, playerPosition, minimumDistanceSquared, out Vector3 offset))
                    continue;
                if (hasPointBehindPlayer && !IsBehindPlayer(offset, playerForward)) continue;

                lastValidPoint = spawnPoint;
                selection -= Mathf.Max(offset.sqrMagnitude, 0.0001f);
                if (selection <= 0f) return spawnPoint;
            }

            return lastValidPoint;
        }

        private bool IsBehindPlayer(Vector3 offset, Vector3 playerForward)
        {
            if (offset.sqrMagnitude < 0.0001f) return false;
            return Vector3.Dot(offset.normalized, playerForward) <= _behindPlayerDotThreshold;
        }

        private static bool TryGetExitOffset(
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

        private GameObject GetEnvironment(EnvironmentType EnvironmentType)
        {
            return EnvironmentType switch
            {
                EnvironmentType.Start => _startEnvironment,
                EnvironmentType.Factory => _factoryEnvironment,
                EnvironmentType.Park => _parkEnvironment,
                _ => null
            };
        }

        private void ResolveEnvironmentReferences()
        {
            _startEnvironment ??= FindEnvironmentChild(
                "Start Environment",
                "Start Enviroment");
            _factoryEnvironment ??= FindEnvironmentChild(
                "Factory Environment",
                "Factory Enviroment");
            _parkEnvironment ??= FindEnvironmentChild(
                "Park Environment",
                "Park Enviroment");
        }

        private GameObject FindEnvironmentChild(string currentName, string legacyName)
        {
            Transform child = transform.Find(currentName) ?? transform.Find(legacyName);
            if (child != null) return child.gameObject;

            Transform[] loadedTransforms = FindObjectsByType<Transform>(
                FindObjectsInactive.Include,
                FindObjectsSortMode.None);
            foreach (Transform candidate in loadedTransforms)
            {
                if (candidate == null || candidate.gameObject.scene != gameObject.scene) continue;
                if (candidate.name == currentName || candidate.name == legacyName)
                    return candidate.gameObject;
            }

            return null;
        }

        private static Transform FindUIPoint(Transform uiPointsRoot, ApplicationState state)
        {
            if (uiPointsRoot == null) return null;

            Transform point = uiPointsRoot.Find($"UI Point - {state}");
            if (point != null) return point;

            string legacyStateName = state switch
            {
                ApplicationState.SelectExtinguisher => "Selecting",
                ApplicationState.Fighting => "Playing",
                ApplicationState.Completed => "Won",
                ApplicationState.Failed => "Lost",
                _ => null
            };
            return legacyStateName != null
                ? uiPointsRoot.Find($"UI Point - {legacyStateName}")
                : null;
        }

        private Transform GetLegacyExitMarker(EnvironmentType EnvironmentType)
        {
            return EnvironmentType switch
            {
                EnvironmentType.Factory => _factoryExit,
                EnvironmentType.Park => _parkExit,
                _ => null
            };
        }

        private static void SetActive(GameObject target, bool isActive)
        {
            if (target != null && target.activeSelf != isActive) target.SetActive(isActive);
        }

        private void OnValidate()
        {
            EnsureUIStates(EnvironmentType.Start, ApplicationState.Start);
            EnsureUIStates(
                EnvironmentType.Factory,
                ApplicationState.Guide,
                ApplicationState.Explore,
                ApplicationState.SelectExtinguisher,
                ApplicationState.Fighting,
                ApplicationState.Escape,
                ApplicationState.Completed,
                ApplicationState.Failed);
            EnsureUIStates(
                EnvironmentType.Park,
                ApplicationState.Guide,
                ApplicationState.Explore,
                ApplicationState.SelectExtinguisher,
                ApplicationState.Fighting,
                ApplicationState.Escape,
                ApplicationState.Completed,
                ApplicationState.Failed);
        }

        private void EnsureUIStates(EnvironmentType EnvironmentType, params ApplicationState[] states)
        {
            EnvironmentUIPoints EnvironmentPoints = null;
            foreach (EnvironmentUIPoints candidate in _uiPoints)
            {
                if (candidate.Environment != EnvironmentType) continue;

                EnvironmentPoints = candidate;
                break;
            }

            if (EnvironmentPoints == null)
            {
                EnvironmentPoints = new EnvironmentUIPoints(EnvironmentType);
                _uiPoints.Add(EnvironmentPoints);
            }

            foreach (ApplicationState state in states) EnvironmentPoints.EnsureState(state);
        }
    }
}
