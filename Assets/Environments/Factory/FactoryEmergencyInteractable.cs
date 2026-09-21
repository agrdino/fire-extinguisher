using System.Collections;
using _Scripts.Fires;
using UnityEngine;

namespace _Scripts.Environments.Factory
{
    public enum FactoryEmergencyInteractableKind
    {
        CircuitBreaker,
        FireAlarm
    }

    [DisallowMultipleComponent]
    [RequireComponent(typeof(Collider))]
    public sealed class FactoryEmergencyInteractable : MonoBehaviour
    {
        private static readonly int BaseColorProperty = Shader.PropertyToID("_BaseColor");
        private static readonly int ColorProperty = Shader.PropertyToID("_Color");
        private static readonly int EmissionColorProperty = Shader.PropertyToID("_EmissionColor");

        [SerializeField] private FactoryEmergencyInteractableKind _kind;
        [SerializeField] private FireSpawnPoint _fireSpawnPoint;
        [SerializeField] private bool _isPrimaryHintTarget;
        [SerializeField] private Transform _hintTarget;
        [Header("Feedback")]
        [SerializeField] private HoverMaterialHighlight _hoverHighlight;
        [SerializeField] private Color _pressedColor = new(0.65f, 1f, 1f, 1f);
        [SerializeField] private Color _pressedEmission = new(0.5f, 4f, 5f, 1f);
        [SerializeField, Min(0f)] private float _pressedDuration = 0.18f;
        [SerializeField, Range(0.8f, 1f)] private float _pressedScale = 0.94f;

        [Header("Circuit Breaker")]
        [SerializeField] private Transform _breakerHinge;
        [SerializeField] private Vector3 _breakerClosedEulerAngles = Vector3.zero;
        [SerializeField] private Vector3 _breakerOpenEulerAngles = new(0f, 120f, 0f);
        [SerializeField, Min(0f)] private float _breakerOpenDuration = 1f;

        [SerializeField] private FactoryEmergencyResponseController _responseController;
        [SerializeField] private Renderer[] _renderers = System.Array.Empty<Renderer>();
        private MaterialPropertyBlock _propertyBlock;
        private Color[] _baseColors;
        private Vector3 _initialScale;
        private Coroutine _pressedRoutine;
        private Coroutine _breakerHingeRoutine;
        private bool _isHovered;
        private bool _isPressed;
        private bool _isBreakerOpen;

        public FactoryEmergencyInteractableKind Kind => _kind;
        public FireSpawnPoint FireSpawnPoint => _fireSpawnPoint;
        public bool IsPrimaryHintTarget => _isPrimaryHintTarget;
        public Transform HintTarget => _hintTarget != null ? _hintTarget : transform;
        public bool IsInteractionEnabled { get; private set; }

        private void Awake()
        {
            _propertyBlock = new MaterialPropertyBlock();
            _baseColors = new Color[_renderers.Length];
            _initialScale = transform.localScale;

            for (int index = 0; index < _renderers.Length; index++)
            {
                Renderer targetRenderer = _renderers[index];
                Material sharedMaterial = targetRenderer.sharedMaterial;
                _baseColors[index] = GetMaterialColor(sharedMaterial);
                Material[] materials = targetRenderer.materials;
                for (int materialIndex = 0; materialIndex < materials.Length; materialIndex++)
                    materials[materialIndex].EnableKeyword("_EMISSION");
            }

            ResetBreakerHinge();
            ApplyVisual();
        }

        private void OnDisable()
        {
            if (_pressedRoutine != null) StopCoroutine(_pressedRoutine);
            if (_breakerHingeRoutine != null) StopCoroutine(_breakerHingeRoutine);
            _pressedRoutine = null;
            _breakerHingeRoutine = null;
            _isHovered = false;
            _isPressed = false;
            transform.localScale = _initialScale;
            ResetBreakerHinge();
            ApplyVisual();
        }

        public void SetInteractionEnabled(bool isEnabled)
        {
            IsInteractionEnabled = isEnabled;
            if (!isEnabled) _isHovered = false;
            ApplyVisual();
        }

        public void SetHovered(bool isHovered)
        {
            bool nextValue = isHovered && IsInteractionEnabled;
            if (_isHovered == nextValue) return;
            _isHovered = nextValue;
            ApplyVisual();
        }

        public bool TryActivate()
        {
            if (!IsInteractionEnabled || _responseController == null) return false;

            if (_kind == FactoryEmergencyInteractableKind.CircuitBreaker)
            {
                if (_breakerHingeRoutine != null) return false;
                if (!_isBreakerOpen)
                {
                    PlayPressedFeedbackFromStart();
                    OpenBreakerHinge();
                    return true;
                }
            }

            PlayPressedFeedbackFromStart();
            _responseController.HandleInteraction(this);
            return true;
        }

        public void ResetInteractionState()
        {
            if (_pressedRoutine != null) StopCoroutine(_pressedRoutine);
            if (_breakerHingeRoutine != null) StopCoroutine(_breakerHingeRoutine);
            _pressedRoutine = null;
            _breakerHingeRoutine = null;
            _isHovered = false;
            _isPressed = false;
            transform.localScale = _initialScale;
            ResetBreakerHinge();
            ApplyVisual();
        }

        private void PlayPressedFeedbackFromStart()
        {
            if (_pressedRoutine != null) StopCoroutine(_pressedRoutine);
            _pressedRoutine = StartCoroutine(PlayPressedFeedback());
        }

        private void OpenBreakerHinge()
        {
            _isBreakerOpen = true;
            if (_breakerHinge == null)
            {
                Debug.LogError("Circuit breaker requires a hinge Transform.", this);
                return;
            }

            if (_breakerOpenDuration <= 0f)
            {
                _breakerHinge.localRotation = Quaternion.Euler(_breakerOpenEulerAngles);
                return;
            }

            _breakerHingeRoutine = StartCoroutine(AnimateBreakerHingeOpen());
        }

        private IEnumerator AnimateBreakerHingeOpen()
        {
            Quaternion startRotation = _breakerHinge.localRotation;
            Quaternion targetRotation = Quaternion.Euler(_breakerOpenEulerAngles);
            float elapsedTime = 0f;

            while (elapsedTime < _breakerOpenDuration)
            {
                elapsedTime += Time.unscaledDeltaTime;
                float progress = Mathf.Clamp01(elapsedTime / _breakerOpenDuration);
                _breakerHinge.localRotation = Quaternion.Slerp(startRotation, targetRotation, progress);
                yield return null;
            }

            _breakerHinge.localRotation = targetRotation;
            _breakerHingeRoutine = null;
        }

        private void ResetBreakerHinge()
        {
            _isBreakerOpen = false;
            if (_kind == FactoryEmergencyInteractableKind.CircuitBreaker && _breakerHinge != null)
                _breakerHinge.localRotation = Quaternion.Euler(_breakerClosedEulerAngles);
        }

        private IEnumerator PlayPressedFeedback()
        {
            _isPressed = true;
            transform.localScale = _initialScale * _pressedScale;
            ApplyVisual();
            if (_pressedDuration > 0f) yield return new WaitForSecondsRealtime(_pressedDuration);
            _isPressed = false;
            transform.localScale = _initialScale;
            ApplyVisual();
            _pressedRoutine = null;
        }

        private void ApplyVisual()
        {
            _hoverHighlight?.SetHovered(_isHovered && IsInteractionEnabled);
            if (_renderers == null || _propertyBlock == null) return;

            for (int index = 0; index < _renderers.Length; index++)
            {
                Renderer targetRenderer = _renderers[index];
                if (targetRenderer == null) continue;

                Color color = _isPressed ? _pressedColor : _baseColors[index];
                Color emission = _isPressed ? _pressedEmission : Color.black;
                targetRenderer.GetPropertyBlock(_propertyBlock);
                _propertyBlock.SetColor(BaseColorProperty, color);
                _propertyBlock.SetColor(ColorProperty, color);
                _propertyBlock.SetColor(EmissionColorProperty, emission);
                targetRenderer.SetPropertyBlock(_propertyBlock);
            }
        }

        private static Color GetMaterialColor(Material material)
        {
            if (material == null) return Color.gray;
            if (material.HasProperty(BaseColorProperty)) return material.GetColor(BaseColorProperty);
            if (material.HasProperty(ColorProperty)) return material.GetColor(ColorProperty);
            return Color.gray;
        }
    }
}
