using System;
using System.Collections;
using _Scripts.Controller;
using _Scripts.Environments.Factory;
using UnityEngine;

namespace _Scripts.Environments.EmergencyContact
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Collider))]
    public sealed class EmergencyContactInteractable : HandRayInteractable
    {
        [SerializeField] private EmergencyContactController _contactController;
        [SerializeField] private Transform _hintTarget;
        [SerializeField] private HoverMaterialHighlight _hoverHighlight;
        [SerializeField] private Renderer[] _renderers = Array.Empty<Renderer>();
        [SerializeField] private Color _pressedColor = new(0.65f, 1f, 1f, 1f);
        [SerializeField] private Color _pressedEmission = new(0.5f, 4f, 5f, 1f);
        [SerializeField, Min(0f)] private float _pressedDuration = 0.18f;

        private static readonly int BaseColorProperty = Shader.PropertyToID("_BaseColor");
        private static readonly int ColorProperty = Shader.PropertyToID("_Color");
        private static readonly int EmissionColorProperty = Shader.PropertyToID("_EmissionColor");

        private MaterialPropertyBlock _propertyBlock;
        private Color[] _baseColors;
        private Coroutine _pressedRoutine;
        private bool _isHovered;
        private bool _isPressed;
        private bool _isInteractionEnabled;

        public Transform HintTarget => _hintTarget != null ? _hintTarget : transform;
        public override bool IsInteractionEnabled => _isInteractionEnabled;

        private void Awake()
        {
            _propertyBlock = new MaterialPropertyBlock();
            _baseColors = new Color[_renderers.Length];
            for (int index = 0; index < _renderers.Length; index++)
            {
                Renderer targetRenderer = _renderers[index];
                if (targetRenderer == null) continue;
                Material sharedMaterial = targetRenderer.sharedMaterial;
                _baseColors[index] = GetMaterialColor(sharedMaterial);
                Material[] materials = targetRenderer.materials;
                for (int materialIndex = 0; materialIndex < materials.Length; materialIndex++) materials[materialIndex].EnableKeyword("_EMISSION");
            }
            ApplyVisual();
        }

        private void OnDisable()
        {
            if (_pressedRoutine != null) StopCoroutine(_pressedRoutine);
            _pressedRoutine = null;
            _isHovered = false;
            _isPressed = false;
            ApplyVisual();
        }

        public void SetInteractionEnabled(bool isEnabled)
        {
            _isInteractionEnabled = isEnabled;
            if (!isEnabled) _isHovered = false;
            ApplyVisual();
        }

        public override void SetHovered(bool isHovered)
        {
            bool nextValue = isHovered && IsInteractionEnabled;
            if (_isHovered == nextValue) return;
            _isHovered = nextValue;
            ApplyVisual();
        }

        public override bool TryActivate()
        {
            if (!IsInteractionEnabled || _contactController == null) return false;
            if (_pressedRoutine != null) StopCoroutine(_pressedRoutine);
            _pressedRoutine = StartCoroutine(PlayPressedFeedback());
            _contactController.HandleInteraction(this);
            return true;
        }

        public void ResetInteractionState()
        {
            if (_pressedRoutine != null) StopCoroutine(_pressedRoutine);
            _pressedRoutine = null;
            _isHovered = false;
            _isPressed = false;
            ApplyVisual();
        }

        private IEnumerator PlayPressedFeedback()
        {
            _isPressed = true;
            ApplyVisual();
            if (_pressedDuration > 0f) yield return new WaitForSecondsRealtime(_pressedDuration);
            _isPressed = false;
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
