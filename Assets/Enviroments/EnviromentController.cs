using System;
using System.Collections.Generic;
using _Scripts.Fires;
using UnityEngine;

namespace _Scripts.Controller
{
    public enum EnviromentType
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
    public sealed class EnviromentUIPoints
    {
        [SerializeField] private EnviromentType _enviroment;
        [SerializeField] private List<UIStatePoint> _points = new();

        public EnviromentType Enviroment => _enviroment;

        public EnviromentUIPoints(EnviromentType enviroment)
        {
            _enviroment = enviroment;
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
    public sealed class EnviromentController : MonoBehaviour
    {
        private static EnviromentController _instance;
        public static EnviromentController Instance => _instance;

        [Header("Enviroments")]
        [SerializeField] private GameObject _startEnviroment;
        [SerializeField] private GameObject _factoryEnviroment;
        [SerializeField] private GameObject _parkEnviroment;

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
        [SerializeField] private List<EnviromentUIPoints> _uiPoints = new();

        [Header("Runtime")]
        [SerializeField] private EnviromentType _currentEnviroment = EnviromentType.Start;

        public EnviromentType CurrentEnviroment => _currentEnviroment;
        public event Action<EnviromentType> OnEnviromentChanged;

        private void Awake()
        {
            if (_instance != null && _instance != this)
            {
                Destroy(gameObject);
                return;
            }

            _instance = this;
            ShowStartEnviroment();
        }

        private void OnDestroy()
        {
            if (_instance == this) _instance = null;
        }

        public void ShowStartEnviroment() => SetEnviroment(EnviromentType.Start);
        public void SelectFactory() => SetEnviroment(EnviromentType.Factory);
        public void SelectPark() => SetEnviroment(EnviromentType.Park);

        public void SetEnviroment(EnviromentType enviromentType)
        {
            _currentEnviroment = enviromentType;

            SetActive(_startEnviroment, enviromentType == EnviromentType.Start);
            SetActive(_factoryEnviroment, enviromentType == EnviromentType.Factory);
            SetActive(_parkEnviroment, enviromentType == EnviromentType.Park);

            OnEnviromentChanged?.Invoke(_currentEnviroment);
        }

        public bool PositionEmergencyExit(Transform playerView)
        {
            if (_emergencyExit == null || playerView == null) return false;

            _selectedExitSpawnPoint = GetPreferredExitSpawnPoint(playerView.position, playerView.forward);
            Transform exitMarker = _selectedExitSpawnPoint != null
                ? _selectedExitSpawnPoint.transform
                : GetLegacyExitMarker(_currentEnviroment);
            if (exitMarker == null) return false;

            _emergencyExit.transform.SetPositionAndRotation(exitMarker.position, exitMarker.rotation);
            return true;
        }

        public bool TryGetUIPoint(EnviromentType enviromentType, ApplicationState state, out Transform point)
        {
            if (state == ApplicationState.Won
                && enviromentType == _currentEnviroment
                && _selectedExitSpawnPoint != null
                && _selectedExitSpawnPoint.CompleteUIPoint != null)
            {
                point = _selectedExitSpawnPoint.CompleteUIPoint;
                return true;
            }

            foreach (EnviromentUIPoints enviromentPoints in _uiPoints)
            {
                if (enviromentPoints.Enviroment != enviromentType) continue;
                if (enviromentPoints.TryGetPoint(state, out point)) return true;

                break;
            }

            GameObject enviroment = GetEnviroment(enviromentType);
            Transform uiPointsRoot = enviroment != null ? enviroment.transform.Find("UI Points") : null;
            point = uiPointsRoot != null ? uiPointsRoot.Find($"UI Point - {state}") : null;
            return point != null;
        }

        public IReadOnlyList<FireSpawnPoint> GetActiveFireSpawnPoints()
        {
            GameObject activeEnviroment = GetEnviroment(_currentEnviroment);
            if (activeEnviroment == null || _currentEnviroment == EnviromentType.Start)
                return Array.Empty<FireSpawnPoint>();

            return activeEnviroment.GetComponentsInChildren<FireSpawnPoint>(true);
        }

        public IReadOnlyList<EmergencyExitSpawnPoint> GetActiveExitSpawnPoints()
        {
            GameObject activeEnviroment = GetEnviroment(_currentEnviroment);
            if (activeEnviroment == null || _currentEnviroment == EnviromentType.Start)
                return Array.Empty<EmergencyExitSpawnPoint>();

            return activeEnviroment.GetComponentsInChildren<EmergencyExitSpawnPoint>(true);
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

        private GameObject GetEnviroment(EnviromentType enviromentType)
        {
            return enviromentType switch
            {
                EnviromentType.Start => _startEnviroment,
                EnviromentType.Factory => _factoryEnviroment,
                EnviromentType.Park => _parkEnviroment,
                _ => null
            };
        }

        private Transform GetLegacyExitMarker(EnviromentType enviromentType)
        {
            return enviromentType switch
            {
                EnviromentType.Factory => _factoryExit,
                EnviromentType.Park => _parkExit,
                _ => null
            };
        }

        private static void SetActive(GameObject target, bool isActive)
        {
            if (target != null && target.activeSelf != isActive) target.SetActive(isActive);
        }

        private void OnValidate()
        {
            EnsureUIStates(EnviromentType.Start, ApplicationState.Start);
            EnsureUIStates(
                EnviromentType.Factory,
                ApplicationState.Guide,
                ApplicationState.Explore,
                ApplicationState.Selecting,
                ApplicationState.Playing,
                ApplicationState.Escape,
                ApplicationState.Won,
                ApplicationState.Lost);
            EnsureUIStates(
                EnviromentType.Park,
                ApplicationState.Guide,
                ApplicationState.Explore,
                ApplicationState.Selecting,
                ApplicationState.Playing,
                ApplicationState.Escape,
                ApplicationState.Won,
                ApplicationState.Lost);
        }

        private void EnsureUIStates(EnviromentType enviromentType, params ApplicationState[] states)
        {
            EnviromentUIPoints enviromentPoints = null;
            foreach (EnviromentUIPoints candidate in _uiPoints)
            {
                if (candidate.Enviroment != enviromentType) continue;

                enviromentPoints = candidate;
                break;
            }

            if (enviromentPoints == null)
            {
                enviromentPoints = new EnviromentUIPoints(enviromentType);
                _uiPoints.Add(enviromentPoints);
            }

            foreach (ApplicationState state in states) enviromentPoints.EnsureState(state);
        }
    }
}
