using System;
using System.Collections.Generic;
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
        [SerializeField] private List<Transform> _spawnPoints = new();
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
            Fire firePrefab = GetRandomFirePrefab();
            if (firePrefab == null) return;
            
            CurrentFireType = firePrefab.FireType;
            OnFireTypeSelected?.Invoke(CurrentFireType);
            foreach (Transform spawnPoint in _spawnPoints)
            {
                if (spawnPoint == null) continue;
                Fire fire = Instantiate(firePrefab, spawnPoint.position, spawnPoint.rotation);
                fire.transform.parent = transform;
                fire.name = $"{firePrefab.name} ({firePrefab.FireType})";
                fire.OnIntensityChanged += Fire_OnIntensityChanged;
                _activeFires.Add(fire);
            }
        }

        private Fire GetRandomFirePrefab()
        {
            int validPrefabCount = 0;
            foreach (Fire prefab in _firePrefabs)
            {
                if (prefab != null) validPrefabCount++;
            }

            if (validPrefabCount == 0) return null;

            int selectedIndex = UnityEngine.Random.Range(0, validPrefabCount);
            foreach (Fire prefab in _firePrefabs)
            {
                if (prefab == null) continue;
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
