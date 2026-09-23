using UnityEngine;

namespace _Scripts.Fires.Visualizes
{
    [DisallowMultipleComponent]
    public sealed class FireCompletionVfxObserver : MonoBehaviour
    {
        [SerializeField] private FireController _fireController;
        [SerializeField] private GameObject _checkVfxPrefab;
        [SerializeField] private Vector3 _localOffset = new(0f, 0.25f, 0f);

        private GameObject _instance;
        private FireSpawnPoint _displayedSpawnPoint;

        private void OnEnable()
        {
            if (_fireController == null)
            {
                Debug.LogError("FireCompletionVfxObserver requires a FireController.", this);
                enabled = false;
                return;
            }

            _fireController.OnAllFiresExtinguished += HandleAllFiresExtinguished;
            SynchronizeState();
        }

        private void Update()
        {
            if (_instance == null || !_instance.activeSelf) return;

            if (!_fireController.AreAllFiresExtinguished()
                || _fireController.SelectedSpawnPoint != _displayedSpawnPoint)
            {
                Hide();
            }
        }

        private void OnDisable()
        {
            if (_fireController != null)
                _fireController.OnAllFiresExtinguished -= HandleAllFiresExtinguished;

            Hide();
        }

        private void HandleAllFiresExtinguished()
        {
            ShowAtSelectedFire();
        }

        private void SynchronizeState()
        {
            if (_fireController.AreAllFiresExtinguished())
                ShowAtSelectedFire();
            else
                Hide();
        }

        private void ShowAtSelectedFire()
        {
            if (_checkVfxPrefab == null)
            {
                Debug.LogError("FireCompletionVfxObserver requires a check VFX prefab.", this);
                return;
            }

            FireSpawnPoint spawnPoint = _fireController.SelectedSpawnPoint;
            if (spawnPoint == null) return;

            Transform target = spawnPoint.transform;
            Vector3 position = target.TransformPoint(_localOffset);

            if (_instance == null)
            {
                _instance = Instantiate(_checkVfxPrefab, position, target.rotation, transform);
                _instance.name = "Check VFX - Fire Extinguished";
            }
            else
            {
                _instance.transform.SetPositionAndRotation(position, target.rotation);
                _instance.SetActive(true);
            }

            _displayedSpawnPoint = spawnPoint;
            RestartParticles(_instance);
        }

        private void Hide()
        {
            if (_instance != null) _instance.SetActive(false);
            _displayedSpawnPoint = null;
        }

        private static void RestartParticles(GameObject instance)
        {
            ParticleSystem[] particleSystems = instance.GetComponentsInChildren<ParticleSystem>(true);
            for (int index = 0; index < particleSystems.Length; index++)
            {
                particleSystems[index].Clear(true);
                particleSystems[index].Play(true);
            }
        }
    }
}
