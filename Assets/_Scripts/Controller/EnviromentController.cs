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

            Transform exitMarker = GetExitMarker(enviromentType);
            if (_emergencyExit != null && exitMarker != null)
                _emergencyExit.transform.SetPositionAndRotation(exitMarker.position, exitMarker.rotation);

            OnEnviromentChanged?.Invoke(_currentEnviroment);
        }

        public bool TryGetUIPoint(EnviromentType enviromentType, ApplicationState state, out Transform point)
        {
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

        private Transform GetExitMarker(EnviromentType enviromentType)
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
                ApplicationState.Selecting,
                ApplicationState.Playing,
                ApplicationState.Escape,
                ApplicationState.Won,
                ApplicationState.Lost);
            EnsureUIStates(
                EnviromentType.Park,
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
