using System;
using UnityEngine;

namespace _Scripts.Environments.Factory
{
    [AddComponentMenu("XR/Visual/Hover Material Highlight")]
    [DisallowMultipleComponent]
    public sealed class HoverMaterialHighlight : MonoBehaviour
    {
        [SerializeField] private Renderer[] _renderers = Array.Empty<Renderer>();
        [SerializeField] private Material _highlightMaterial;

        private Material[][] _originalMaterials;
        private bool _isHovered;

        // The factory hand raycast supplies hover state instead of XRI hover events.
        // Append a shared overlay material, keeping each renderer's base materials intact.
        public void SetHovered(bool isHovered)
        {
            isHovered = isHovered && isActiveAndEnabled && _highlightMaterial != null;
            if (_isHovered == isHovered) return;

            _isHovered = isHovered;
            if (!isHovered)
            {
                RestoreMaterials();
                return;
            }

            _originalMaterials = new Material[_renderers.Length][];
            for (int index = 0; index < _renderers.Length; index++)
            {
                Renderer target = _renderers[index];
                if (target == null) continue;

                Material[] original = target.sharedMaterials;
                _originalMaterials[index] = original;
                Material[] highlighted = new Material[original.Length + 1];
                Array.Copy(original, highlighted, original.Length);
                highlighted[original.Length] = _highlightMaterial;
                target.sharedMaterials = highlighted;
            }
        }

        private void OnDisable()
        {
            _isHovered = false;
            RestoreMaterials();
        }

        private void OnDestroy()
        {
            RestoreMaterials();
        }

        private void RestoreMaterials()
        {
            if (_originalMaterials == null) return;
            for (int index = 0; index < _originalMaterials.Length; index++)
            {
                Renderer target = _renderers[index];
                if (target != null && _originalMaterials[index] != null)
                    target.sharedMaterials = _originalMaterials[index];
            }
            _originalMaterials = null;
        }
    }
}
