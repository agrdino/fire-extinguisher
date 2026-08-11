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

        [SerializeField] private Fire _firePrefab;
        [SerializeField] private List<Transform> _spawnPoints = new();
        [SerializeField] private List<Fire> _activeFires = new();

        private bool _hasRaisedAllFiresExtinguished;

        public event Action OnAllFiresExtinguished;
        
        public IReadOnlyList<Fire> ActiveFires => _activeFires;

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
            foreach (Transform spawnPoint in _spawnPoints)
            {
                Fire fire = Instantiate(_firePrefab, spawnPoint.position, spawnPoint.rotation);
                fire.OnIntensityChanged += Fire_OnIntensityChanged;
                _activeFires.Add(fire);
            }
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
