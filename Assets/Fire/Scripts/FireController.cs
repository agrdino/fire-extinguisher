using System;
using System.Collections;
using System.Collections.Generic;
using _Scripts.Controller;
using _Scripts.Fires.Visualizes;
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
        [SerializeField, Min(0f)] private float _minimumSpawnDistanceFromPlayer = 2f;

        [Header("Electrical Fire Ignition")]
        [SerializeField] private GameObject _electricalSparksPrefab;
        [SerializeField, Min(0f)] private float _preIgnitionDuration = 3f;
        [SerializeField, Min(0f)] private float _fireRevealDuration = 1f;
        [SerializeField, Min(0f)] private float _sparksOverlapDuration = 2f;

        private bool _hasRaisedAllFiresExtinguished;
        private IEnvironmentSceneContext _environment;
        private Coroutine _electricalFireIgnitionRoutine;
        private GameObject _activeElectricalSparks;

        public event Action OnAllFiresExtinguished;
        public event Action OnFireFlareUpStarted;
        public event Action OnFireFlareUpCompleted;
        public event Action<FireType> OnFireTypeSelected;
        
        public IReadOnlyList<Fire> ActiveFires => _activeFires;
        public bool HasBurningFires => _activeFires.Exists(fire => fire != null && !fire.IsExtinguished);
        public FireType CurrentFireType { get; private set; }
        public FireSpawnPoint SelectedSpawnPoint { get; private set; }

        public void BindEnvironment(IEnvironmentSceneContext environment)
        {
            ClearFires();
            _environment = environment;
        }

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

        public void SpawnFires(Transform playerRoot, bool keepElectricalSparksUntilPowerOff = false)
        {
            ClearFires();

            // if (playerRoot == null) return;

            FireSpawnPoint spawnPoint = GetRandomSpawnPoint(playerRoot.position);
            // if (spawnPoint == null) return;
            SelectedSpawnPoint = spawnPoint;

            Fire firePrefab = GetRandomFirePrefab(spawnPoint.FireType);
            // if (firePrefab == null) return;

            CurrentFireType = spawnPoint.FireType;
            OnFireTypeSelected?.Invoke(CurrentFireType);

            if (CurrentFireType == FireType.Electrical && _electricalSparksPrefab != null)
            {
                _electricalFireIgnitionRoutine = StartCoroutine(RunElectricalFireIgnition(firePrefab, spawnPoint, playerRoot, keepElectricalSparksUntilPowerOff));
                return;
            }

            SpawnFire(firePrefab, spawnPoint, playerRoot, false);
        }

        private IEnumerator RunElectricalFireIgnition(Fire firePrefab, FireSpawnPoint spawnPoint, Transform playerRoot, bool keepSparksUntilPowerOff)
        {
            _activeElectricalSparks = Instantiate(
                _electricalSparksPrefab,
                spawnPoint.transform.position,
                spawnPoint.transform.rotation,
                transform);

            if (_preIgnitionDuration > 0f)
                yield return new WaitForSeconds(_preIgnitionDuration);

            SpawnFire(firePrefab, spawnPoint, playerRoot, true);

            if (keepSparksUntilPowerOff)
            {
                _electricalFireIgnitionRoutine = null;
                yield break;
            }

            if (_sparksOverlapDuration > 0f) yield return new WaitForSeconds(_sparksOverlapDuration);
            DestroyActiveElectricalSparks();
            _electricalFireIgnitionRoutine = null;
        }

        public void StopElectricalSparks()
        {
            DestroyActiveElectricalSparks();
        }

        private void SpawnFire(
            Fire firePrefab,
            FireSpawnPoint spawnPoint,
            Transform playerRoot,
            bool revealFromZero)
        {
            Fire fire = Instantiate(firePrefab, spawnPoint.transform.position, spawnPoint.transform.rotation, transform);
            fire.name = $"{firePrefab.name} ({spawnPoint.FireType})";

            if (revealFromZero)
            {
                FireVFX[] fireVisuals = fire.GetComponentsInChildren<FireVFX>(true);
                for (int i = 0; i < fireVisuals.Length; i++)
                    fireVisuals[i].RevealFromZero(_fireRevealDuration);
            }

            FireProximityWarning proximityWarning = fire.GetComponentInChildren<FireProximityWarning>(true);
            if (proximityWarning != null) proximityWarning.Arm(playerRoot);
            fire.OnIntensityChanged += Fire_OnIntensityChanged;
            fire.OnFlareUpStarted += Fire_OnFlareUpStarted;
            fire.OnFlareUpCompleted += Fire_OnFlareUpCompleted;
            _activeFires.Add(fire);
        }

        private FireSpawnPoint GetRandomSpawnPoint(Vector3 playerPosition)
        {
            // if (_environment == null) return null;

            IReadOnlyList<FireSpawnPoint> spawnPoints = _environment.FireSpawnPoints;
            float minimumDistanceSquared = _minimumSpawnDistanceFromPlayer * _minimumSpawnDistanceFromPlayer;
            int validPointCount = 0;
            for (int i = 0; i < spawnPoints.Count; i++)
            {
                if (IsValidSpawnPoint(spawnPoints[i], playerPosition, minimumDistanceSquared)) validPointCount++;
            }

            // if (validPointCount == 0) return null;

            int selectedIndex = UnityEngine.Random.Range(0, validPointCount);
            for (int i = 0; i < spawnPoints.Count; i++)
            {
                FireSpawnPoint spawnPoint = spawnPoints[i];
                if (!IsValidSpawnPoint(spawnPoint, playerPosition, minimumDistanceSquared)) continue;
                if (selectedIndex-- == 0) return spawnPoint;
            }

            return null;
        }

        private static bool IsValidSpawnPoint(
            FireSpawnPoint spawnPoint,
            Vector3 playerPosition,
            float minimumDistanceSquared)
        {
            if (spawnPoint == null || !spawnPoint.gameObject.activeInHierarchy) return false;

            Vector3 offset = spawnPoint.transform.position - playerPosition;
            offset.y = 0f;
            return offset.sqrMagnitude >= minimumDistanceSquared;
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
            if (_electricalFireIgnitionRoutine != null)
            {
                StopCoroutine(_electricalFireIgnitionRoutine);
                _electricalFireIgnitionRoutine = null;
            }

            DestroyActiveElectricalSparks();
            UnsubscribeFromFires();
            for (int i = _activeFires.Count - 1; i >= 0; i--)
            {
                Fire fire = _activeFires[i];
                if (fire != null) Destroy(fire.gameObject);
            }

            _activeFires.Clear();
            _hasRaisedAllFiresExtinguished = false;
            SelectedSpawnPoint = null;
        }

        private void DestroyActiveElectricalSparks()
        {
            if (_activeElectricalSparks == null) return;

            Destroy(_activeElectricalSparks);
            _activeElectricalSparks = null;
        }

        private void UnsubscribeFromFires()
        {
            foreach (Fire fire in _activeFires)
            {
                if (fire == null) continue;

                fire.OnIntensityChanged -= Fire_OnIntensityChanged;
                fire.OnFlareUpStarted -= Fire_OnFlareUpStarted;
                fire.OnFlareUpCompleted -= Fire_OnFlareUpCompleted;
            }
        }

        private void Fire_OnIntensityChanged(float currentIntensity)
        {
            if (_hasRaisedAllFiresExtinguished) return;
            if (!AreAllFiresExtinguished()) return;

            _hasRaisedAllFiresExtinguished = true;
            OnAllFiresExtinguished?.Invoke();
        }

        private void Fire_OnFlareUpStarted() => OnFireFlareUpStarted?.Invoke();

        private void Fire_OnFlareUpCompleted() => OnFireFlareUpCompleted?.Invoke();

    }
}
