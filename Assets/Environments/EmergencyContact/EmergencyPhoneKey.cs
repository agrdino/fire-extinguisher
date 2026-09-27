using System.Collections;
using _Scripts.Controller;
using _Scripts.UI;
using UnityEngine;

namespace _Scripts.Environments.EmergencyContact
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Collider))]
    public sealed class EmergencyPhoneKey : HandRayInteractable
    {
        private static readonly int BaseColorProperty = Shader.PropertyToID("_BaseColor");
        private static readonly int ColorProperty = Shader.PropertyToID("_Color");
        private static readonly int EmissionColorProperty = Shader.PropertyToID("_EmissionColor");

        [SerializeField] private string _symbol;
        [SerializeField] private EmergencyPhoneKeypadController _keypad;
        [SerializeField] private Renderer _keyRenderer;
        [SerializeField] private Transform _visualTransform;

        [Header("Feedback")]
        [SerializeField] private Color _hoverColor = new(0.2f, 0.9f, 1f, 1f);
        [SerializeField] private Color _pressedColor = new(0f, 0.7843137f, 1f, 1f);
        [SerializeField] private Color _hoverEmission = new(0f, 1.2f, 1.8f, 1f);
        [SerializeField] private Color _pressedEmission = new(0f, 2.5f, 4f, 1f);
        [SerializeField, Min(0f)] private float _pressDepth = 0.004f;
        [SerializeField, Min(0.01f)] private float _pressDuration = 0.12f;

        private MaterialPropertyBlock _propertyBlock;
        private Color _baseColor = Color.white;
        private Vector3 _restLocalPosition;
        private Coroutine _feedbackRoutine;
        private bool _isEnabled;
        private bool _isHovered;
        private bool _isPressed;

        public string Symbol => _symbol;
        public override bool IsInteractionEnabled => _isEnabled && _keypad != null;

        private void Awake()
        {
            if (_keypad == null) _keypad = GetComponentInParent<EmergencyPhoneKeypadController>();
            if (_keyRenderer == null) _keyRenderer = GetComponent<Renderer>();
            if (_visualTransform == null) _visualTransform = _keyRenderer != null ? _keyRenderer.transform : transform;

            _restLocalPosition = _visualTransform.localPosition;
            _propertyBlock = new MaterialPropertyBlock();
            _baseColor = ReadBaseColor(_keyRenderer != null ? _keyRenderer.sharedMaterial : null);
            ApplyVisual();
        }

        private void OnDisable()
        {
            if (_feedbackRoutine != null) StopCoroutine(_feedbackRoutine);
            _feedbackRoutine = null;
            _isHovered = false;
            _isPressed = false;
            if (_visualTransform != null) _visualTransform.localPosition = _restLocalPosition;
            ApplyVisual();
        }

        public void Configure(
            string symbol,
            EmergencyPhoneKeypadController keypad,
            Renderer keyRenderer,
            Transform visualTransform)
        {
            _symbol = symbol;
            _keypad = keypad;
            _keyRenderer = keyRenderer;
            _visualTransform = visualTransform;
        }

        public void SetInteractionEnabled(bool isEnabled)
        {
            _isEnabled = isEnabled;
            if (!isEnabled) _isHovered = false;
            ApplyVisual();
        }

        public override void SetHovered(bool isHovered)
        {
            bool nextValue = isHovered && IsInteractionEnabled;
            if (_isHovered == nextValue) return;

            _isHovered = nextValue;
            if (_isHovered) UIAudioFeedback.PlayHover();
            ApplyVisual();
        }

        public override bool TryActivate()
        {
            if (!IsInteractionEnabled || !_keypad.TryAppendSymbol(_symbol)) return false;

            if (_feedbackRoutine != null) StopCoroutine(_feedbackRoutine);
            _feedbackRoutine = StartCoroutine(PlayPressedFeedback());
            UIAudioFeedback.PlayClick();
            return true;
        }

        private IEnumerator PlayPressedFeedback()
        {
            _isPressed = true;
            if (_visualTransform != null)
                _visualTransform.localPosition = _restLocalPosition + Vector3.back * _pressDepth;
            ApplyVisual();

            yield return new WaitForSecondsRealtime(_pressDuration);

            if (_visualTransform != null) _visualTransform.localPosition = _restLocalPosition;
            _isPressed = false;
            ApplyVisual();
            _feedbackRoutine = null;
        }

        private void ApplyVisual()
        {
            if (_keyRenderer == null || _propertyBlock == null) return;

            Color color = _isPressed ? _pressedColor : _isHovered ? _hoverColor : _baseColor;
            Color emission = _isPressed ? _pressedEmission : _isHovered ? _hoverEmission : Color.black;
            _keyRenderer.GetPropertyBlock(_propertyBlock);
            _propertyBlock.SetColor(BaseColorProperty, color);
            _propertyBlock.SetColor(ColorProperty, color);
            _propertyBlock.SetColor(EmissionColorProperty, emission);
            _keyRenderer.SetPropertyBlock(_propertyBlock);
        }

        private static Color ReadBaseColor(Material material)
        {
            if (material == null) return Color.white;
            if (material.HasProperty(BaseColorProperty)) return material.GetColor(BaseColorProperty);
            if (material.HasProperty(ColorProperty)) return material.GetColor(ColorProperty);
            return Color.white;
        }
    }
}
