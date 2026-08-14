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
    }
}
