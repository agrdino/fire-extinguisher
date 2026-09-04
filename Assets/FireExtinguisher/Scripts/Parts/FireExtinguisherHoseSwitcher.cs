using System.Collections;
using UnityEngine;

namespace _Scripts.FireExtinguishers.Visualizes
{
    [DisallowMultipleComponent]
    public sealed class FireExtinguisherHoseSwitcher : MonoBehaviour
    {
        private const float FullyVisible = 0f;
        private const float FullyDissolved = 1f;

        [SerializeField] private FireExtinguisher _fireExtinguisher;
        [SerializeField] private FireExtinguisherHose _hose;

        [Header("Start Anchors")]
        [SerializeField] private Transform _unselectAnchor;
        [SerializeField] private Transform _co2Anchor;
        [SerializeField] private Transform _powderAnchor;

        [Header("Materials")]
        [SerializeField] private Material _selectedMaterial;
        [SerializeField] private Material _unselectMaterial;

        [Header("Dissolve Transition")]
        [SerializeField] private Material _dissolveMaterial;
        [SerializeField, Min(0f)] private float _phaseDuration = 0.6f;

        private DissolveRendererMaterials _transitionMaterials;
        private Coroutine _transitionRoutine;
        private FireExtinguisherType _visualType;
        private FireExtinguisherType _requestedType;
        private bool _isTransitioning;

        private void OnEnable()
        {
            if (_fireExtinguisher == null || _hose == null) return;

            _transitionMaterials = new DissolveRendererMaterials(_dissolveMaterial);
            _visualType = _fireExtinguisher.ExtinguisherType;
            _requestedType = _visualType;
            ApplyHoseType(_visualType);

            _fireExtinguisher.OnTypeChanged += RequestHose;
        }

        private void OnDisable()
        {
            if (_fireExtinguisher != null)
                _fireExtinguisher.OnTypeChanged -= RequestHose;

            CancelTransition();

            if (_fireExtinguisher != null && _hose != null)
            {
                _visualType = _fireExtinguisher.ExtinguisherType;
                _requestedType = _visualType;
                ApplyHoseType(_visualType);
            }
        }

        private void RequestHose(FireExtinguisherType extinguisherType)
        {
            _requestedType = extinguisherType;
            if (_isTransitioning || _visualType == _requestedType) return;

            if (_dissolveMaterial == null || _phaseDuration <= 0f || _hose.Renderer == null)
            {
                ApplyHoseType(_requestedType);
                _visualType = _requestedType;
                return;
            }

            _isTransitioning = true;
            _transitionRoutine = StartCoroutine(TransitionToRequestedHose());
        }

        private IEnumerator TransitionToRequestedHose()
        {
            while (_visualType != _requestedType)
            {
                if (_transitionMaterials.Apply(new[] { _hose.Renderer }, FullyVisible))
                    yield return AnimateDissolve(FullyVisible, FullyDissolved);

                _transitionMaterials.Restore();

                FireExtinguisherType incomingType = _requestedType;
                ApplyHoseType(incomingType);
                _visualType = incomingType;

                if (_transitionMaterials.Apply(new[] { _hose.Renderer }, FullyDissolved))
                    yield return AnimateDissolve(FullyDissolved, FullyVisible);

                _transitionMaterials.Restore();
            }

            _transitionRoutine = null;
            _isTransitioning = false;
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

        private void ApplyHoseType(FireExtinguisherType extinguisherType)
        {
            _hose.SetStartAnchor(GetAnchor(extinguisherType));
            _hose.SetMaterial(extinguisherType == FireExtinguisherType.Unselect
                ? _unselectMaterial
                : _selectedMaterial);
        }

        private void CancelTransition()
        {
            if (_transitionRoutine != null)
            {
                StopCoroutine(_transitionRoutine);
                _transitionRoutine = null;
            }

            _transitionMaterials?.Restore();
            _isTransitioning = false;
        }

        private Transform GetAnchor(FireExtinguisherType extinguisherType)
        {
            return extinguisherType switch
            {
                FireExtinguisherType.CO2 => _co2Anchor,
                FireExtinguisherType.Powder => _powderAnchor,
                _ => _unselectAnchor
            };
        }
    }
}
