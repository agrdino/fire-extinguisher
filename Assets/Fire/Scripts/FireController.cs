using System;
using System.Collections.Generic;
using _Scripts.Controller;
using UnityEngine;

namespace _Scripts.Fires
{
    [DefaultExecutionOrder(-101)]
    [DisallowMultipleComponent]
    public sealed class FireController : MonoBehaviour
    {
        private static FireController _instance;
        public static FireController Instance 
        { 
            get => _instance; 
            private set => _instance = value; 
        }

        [SerializeField] private List<Fire> _firePrefabs = new();
        [SerializeField] private List<Fire> _activeFires = new();

        private bool _hasRaisedAllFiresExtinguished;

        public event Action OnAllFiresExtinguished;
        public event Action<FireType> OnFireTypeSelected;
        
        public IReadOnlyList<Fire> ActiveFires => _activeFires;
        public FireType CurrentFireType { get; private set; }

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;
        }

        private void OnDestroy()
        {
            UnsubscribeFromFires();
            if (Instance == this) Instance = null;
        }

        public bool AreAllFiresExtinguished() => _activeFires.Count > 0 && _activeFires.TrueForAll(fire => fire.IsExtinguished);

        public void SpawnFires()
        {
            ClearFires();

            FireSpawnPoint spawnPoint = GetRandomSpawnPoint();
            if (spawnPoint == null)
            {
                Debug.LogWarning("No fire spawn point is available in the selected enviroment.", this);
                return;
            }

            Fire firePrefab = GetRandomFirePrefab(spawnPoint.FireType);
            if (firePrefab == null)
            {
                Debug.LogWarning($"No fire prefab is configured for {spawnPoint.FireType}.", this);
                return;
            }

            CurrentFireType = spawnPoint.FireType;
            OnFireTypeSelected?.Invoke(CurrentFireType);

            Fire fire = Instantiate(firePrefab, spawnPoint.transform.position, spawnPoint.transform.rotation, transform);
            fire.name = $"{firePrefab.name} ({spawnPoint.FireType})";
            fire.OnIntensityChanged += Fire_OnIntensityChanged;
            _activeFires.Add(fire);
        }

        private static FireSpawnPoint GetRandomSpawnPoint()
        {
            EnviromentController enviromentController = EnviromentController.Instance;
            if (enviromentController == null) return null;

            IReadOnlyList<FireSpawnPoint> spawnPoints = enviromentController.GetActiveFireSpawnPoints();
            int validPointCount = 0;
            for (int i = 0; i < spawnPoints.Count; i++)
            {
                if (spawnPoints[i] != null && spawnPoints[i].gameObject.activeInHierarchy) validPointCount++;
            }

            if (validPointCount == 0) return null;

            int selectedIndex = UnityEngine.Random.Range(0, validPointCount);
            for (int i = 0; i < spawnPoints.Count; i++)
            {
                FireSpawnPoint spawnPoint = spawnPoints[i];
                if (spawnPoint == null || !spawnPoint.gameObject.activeInHierarchy) continue;
                if (selectedIndex-- == 0) return spawnPoint;
            }

            return null;
        }

        private Fire GetRandomFirePrefab(FireType fireType)
        {
            int validPrefabCount = 0;
            foreach (Fire prefab in _firePrefabs)
            {
                if (prefab != null && prefab.FireType == fireType) validPrefabCount++;
            }

            if (validPrefabCount == 0) return null;

            int selectedIndex = UnityEngine.Random.Range(0, validPrefabCount);
            foreach (Fire prefab in _firePrefabs)
            {
                if (prefab == null || prefab.FireType != fireType) continue;
                if (selectedIndex-- == 0) return prefab;
            }

            return null;
        }

        public void ClearFires()
        {
            UnsubscribeFromFires();
            for (int i = _activeFires.Count - 1; i >= 0; i--)
            {
                Fire fire = _activeFires[i];
                if (fire != null) Destroy(fire.gameObject);
            }

            _activeFires.Clear();
            _hasRaisedAllFiresExtinguished = false;
        }

        private void UnsubscribeFromFires()
        {
            foreach (Fire fire in _activeFires)
            {
                if (fire != null) fire.OnIntensityChanged -= Fire_OnIntensityChanged;
            }
        }

        private void Fire_OnIntensityChanged(float currentIntensity)
        {
            if (_hasRaisedAllFiresExtinguished) return;
            if (!AreAllFiresExtinguished()) return;

            _hasRaisedAllFiresExtinguished = true;
            OnAllFiresExtinguished?.Invoke();
        }

    }
}
