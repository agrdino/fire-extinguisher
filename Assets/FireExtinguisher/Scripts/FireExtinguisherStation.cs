using System.Collections;
using _Scripts.Controller;
using _Scripts.Fires;
using UnityEngine;

namespace _Scripts.FireExtinguishers
{
    [DisallowMultipleComponent]
    public sealed class FireExtinguisherStation : MonoBehaviour
    {
        [SerializeField] private FireSpawnPoint _fireSpawnPoint;
        [SerializeField] private FireExtinguisherDisplayItem _co2;
        [SerializeField] private FireExtinguisherDisplayItem _powder;

        [Header("Selection Feedback")]
        [SerializeField] private GameObject _selectionFeedbackPrefab;
        [SerializeField] private Vector3 _selectionFeedbackOffset = new(0f, 0.78f, 0f);

        private bool _isActiveStation;
        private bool _isTransitioning;
        private GameObject _selectionFeedbackInstance;
        private Coroutine _selectionFeedbackRoutine;

        public FireSpawnPoint FireSpawnPoint => _fireSpawnPoint;
        public bool IsActiveStation => _isActiveStation;

        public bool Matches(FireSpawnPoint selectedSpawnPoint)
        {
            return _fireSpawnPoint == null || _fireSpawnPoint == selectedSpawnPoint;
        }

        public void Configure(bool isActiveStation, FireExtinguisherType selectedType)
        {
            StopSelectionFeedback();
            _isActiveStation = isActiveStation;
            _isTransitioning = false;
            SetDisplayImmediate(selectedType);
            RefreshInteraction();
        }

        public bool TrySelect(FireExtinguisherType type)
        {
            return _isActiveStation
                   && !_isTransitioning
                   && ApplicationManager.Instance != null
                   && ApplicationManager.Instance.TrySelectExtinguisher(type, this);
        }

        public void BeginTransition()
        {
            _isTransitioning = true;
            RefreshInteraction();
        }

        public void PlayOutgoingToGround(FireExtinguisherType outgoingType, float duration)
        {
            GetItem(outgoingType)?.PlayDissolve(true, duration);
        }

        public void PlayIncomingFromGround(FireExtinguisherType incomingType, float duration)
        {
            GetItem(incomingType)?.PlayDissolve(false, duration);
        }

        public void CompleteTransition()
        {
            _isTransitioning = false;
            RefreshInteraction();
        }

        public void PlaySelectionFeedback(FireExtinguisherType selectedType)
        {
            FireExtinguisherDisplayItem selectedItem = GetItem(selectedType);
            if (selectedItem == null || _selectionFeedbackPrefab == null) return;

            StopSelectionFeedback();
            Vector3 position = selectedItem.transform.position + _selectionFeedbackOffset;
            _selectionFeedbackInstance = Instantiate(
                _selectionFeedbackPrefab,
                position,
                Quaternion.identity);
            _selectionFeedbackRoutine = StartCoroutine(WaitForSelectionFeedback());
        }

        public Transform GetHintTarget()
        {
            if (_co2 != null && _co2.IsVisible) return _co2.transform;
            if (_powder != null && _powder.IsVisible) return _powder.transform;
            return transform;
        }

        public void RefreshInteractionState() => RefreshInteraction();

        private void OnDisable() => StopSelectionFeedback();

        private IEnumerator WaitForSelectionFeedback()
        {
            Transform staticCheck = FindChild(_selectionFeedbackInstance.transform, "Check Static");
            if (staticCheck != null) staticCheck.gameObject.SetActive(false);

            Transform animatedCheck = FindChild(_selectionFeedbackInstance.transform, "Check Animation");
            ParticleSystem animationParticles = animatedCheck != null
                ? animatedCheck.GetComponent<ParticleSystem>()
                : null;

            ParticleSystem[] particles = _selectionFeedbackInstance.GetComponentsInChildren<ParticleSystem>(true);
            for (int index = 0; index < particles.Length; index++)
            {
                ParticleSystem particle = particles[index];
                if (staticCheck != null
                    && (particle.transform == staticCheck || particle.transform.IsChildOf(staticCheck)))
                    continue;
                particle.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
                particle.Play(true);
            }

            // Let the selected check animate once, then remove the entire feedback
            // (including the accompanying beat) rather than leaving a static check.
            yield return null;
            if (animationParticles != null)
            {
                while (animationParticles != null && animationParticles.IsAlive(true))
                    yield return null;
            }

            if (_selectionFeedbackInstance != null)
                Destroy(_selectionFeedbackInstance);
            _selectionFeedbackInstance = null;
            _selectionFeedbackRoutine = null;
        }

        private void StopSelectionFeedback()
        {
            if (_selectionFeedbackRoutine != null)
            {
                StopCoroutine(_selectionFeedbackRoutine);
                _selectionFeedbackRoutine = null;
            }
            if (_selectionFeedbackInstance != null)
            {
                Destroy(_selectionFeedbackInstance);
                _selectionFeedbackInstance = null;
            }
        }

        private static Transform FindChild(Transform root, string childName)
        {
            if (root == null) return null;
            if (root.name == childName) return root;
            for (int index = 0; index < root.childCount; index++)
            {
                Transform result = FindChild(root.GetChild(index), childName);
                if (result != null) return result;
            }
            return null;
        }

        private void SetDisplayImmediate(FireExtinguisherType selectedType)
        {
            // Inactive factory stations remain environmental props. Only the active
            // station mirrors the extinguisher currently held by the player.
            FireExtinguisherType hiddenType = _isActiveStation
                ? selectedType
                : FireExtinguisherType.Unselect;
            _co2?.SetVisibleImmediate(hiddenType != FireExtinguisherType.CO2);
            _powder?.SetVisibleImmediate(hiddenType != FireExtinguisherType.Powder);
        }

        private void RefreshInteraction()
        {
            bool canInteract = _isActiveStation
                               && !_isTransitioning
                               && ApplicationManager.Instance != null
                               && ApplicationManager.Instance.IsFighting;
            _co2?.SetStationInteractionEnabled(canInteract);
            _powder?.SetStationInteractionEnabled(canInteract);
        }

        private FireExtinguisherDisplayItem GetItem(FireExtinguisherType type)
        {
            return type switch
            {
                FireExtinguisherType.CO2 => _co2,
                FireExtinguisherType.Powder => _powder,
                _ => null
            };
        }
    }
}
