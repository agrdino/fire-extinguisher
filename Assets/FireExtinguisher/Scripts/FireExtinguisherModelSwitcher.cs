using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
namespace _Scripts.FireExtinguishers
{
    [DisallowMultipleComponent]
    public sealed class FireExtinguisherModelSwitcher : MonoBehaviour
    {
        private const float FullyVisible = 0f;
        private const float FullyDissolved = 1f;

        [SerializeField] private FireExtinguisher _fireExtinguisher;
        [SerializeField] private Transform _unselectModel;
        [SerializeField] private Transform _co2Model;
        [SerializeField] private Transform _powderModel;

        [Header("Dissolve Transition")]
        [SerializeField] private Material _dissolveMaterial;
        [SerializeField, Min(0f)] private float _phaseDuration = 0.6f;

        private DissolveRendererMaterials _transitionMaterials;
        private Coroutine _transitionRoutine;
        private FireExtinguisherType _visualType;
        private FireExtinguisherType _requestedType;

        public bool IsTransitioning { get; private set; }

        public event Action OnTransitionStarted;
        public event Action OnTransitionCompleted;

        private void OnEnable()
        {
            if (_fireExtinguisher == null) return;

            _transitionMaterials = new DissolveRendererMaterials(_dissolveMaterial);
            _visualType = _fireExtinguisher.ExtinguisherType;
            _requestedType = _visualType;
            SetModelImmediate(_visualType);

            _fireExtinguisher.OnTypeChanged += RequestModel;
        }

        private void OnDisable()
        {
            if (_fireExtinguisher != null)
                _fireExtinguisher.OnTypeChanged -= RequestModel;

            CancelTransition();

            if (_fireExtinguisher != null)
            {
                _visualType = _fireExtinguisher.ExtinguisherType;
                _requestedType = _visualType;
                SetModelImmediate(_visualType);
            }
        }

        private void RequestModel(FireExtinguisherType extinguisherType)
        {
            _requestedType = extinguisherType;
            if (IsTransitioning || _visualType == _requestedType) return;

            if (_dissolveMaterial == null || _phaseDuration <= 0f)
            {
                SetModelImmediate(_requestedType);
                _visualType = _requestedType;
                return;
            }

            IsTransitioning = true;
            OnTransitionStarted?.Invoke();
            _transitionRoutine = StartCoroutine(TransitionToRequestedModel());
        }

        private IEnumerator TransitionToRequestedModel()
        {
            while (_visualType != _requestedType)
            {
                Transform outgoingModel = GetModel(_visualType);
                if (outgoingModel != null)
                {
                    SetActive(outgoingModel, true);
                    if (_transitionMaterials.Apply(GetRenderers(outgoingModel), FullyVisible))
                        yield return AnimateDissolve(FullyVisible, FullyDissolved);

                    _transitionMaterials.Restore();
                    SetActive(outgoingModel, false);
                }

                // Use the latest request at the midpoint so rapid input never reveals
                // a stale model before transitioning again.
                FireExtinguisherType incomingType = _requestedType;
                Transform incomingModel = GetModel(incomingType);
                _visualType = incomingType;

                if (incomingModel == null) continue;

                SetActive(incomingModel, true);
                if (_transitionMaterials.Apply(GetRenderers(incomingModel), FullyDissolved))
                    yield return AnimateDissolve(FullyDissolved, FullyVisible);

                _transitionMaterials.Restore();
            }

            _transitionRoutine = null;
            IsTransitioning = false;
            OnTransitionCompleted?.Invoke();
        }

        private IEnumerator AnimateDissolve(float from, float to)
        {
            float elapsed = 0f;
            while (elapsed < _phaseDuration)
            {
                elapsed += Time.unscaledDeltaTime;
                float progress = Mathf.Clamp01(elapsed / _phaseDuration);
                float easedProgress = 0.5f - 0.5f * Mathf.Cos(Mathf.PI * progress);
                _transitionMaterials.SetAmount(Mathf.LerpUnclamped(from, to, easedProgress));
                yield return null;
            }

            _transitionMaterials.SetAmount(to);
        }

        private void SetModelImmediate(FireExtinguisherType extinguisherType)
        {
            _transitionMaterials?.Restore();
            SetActive(_unselectModel, extinguisherType == FireExtinguisherType.Unselect);
            SetActive(_co2Model, extinguisherType == FireExtinguisherType.CO2);
            SetActive(_powderModel, extinguisherType == FireExtinguisherType.Powder);
        }

        private Transform GetModel(FireExtinguisherType extinguisherType)
        {
            return extinguisherType switch
            {
                FireExtinguisherType.CO2 => _co2Model,
                FireExtinguisherType.Powder => _powderModel,
                _ => _unselectModel
            };
        }

        private void CancelTransition()
        {
            bool wasTransitioning = IsTransitioning;
            if (_transitionRoutine != null)
            {
                StopCoroutine(_transitionRoutine);
                _transitionRoutine = null;
            }

            _transitionMaterials?.Restore();
            IsTransitioning = false;
            if (wasTransitioning) OnTransitionCompleted?.Invoke();
        }

        private static IEnumerable<Renderer> GetRenderers(Transform model)
        {
            Renderer[] renderers = model.GetComponentsInChildren<Renderer>(true);
            for (int index = 0; index < renderers.Length; index++)
            {
                Renderer renderer = renderers[index];
                if (renderer is MeshRenderer || renderer is SkinnedMeshRenderer)
                    yield return renderer;
            }
        }

        private static void SetActive(Transform model, bool isActive)
        {
            if (model != null && model.gameObject.activeSelf != isActive)
                model.gameObject.SetActive(isActive);
        }
    }
}
