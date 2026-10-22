using System.Collections.Generic;
using UnityEngine;

namespace _Scripts.UI
{
    /// <summary>
    /// World-space controller overlay used only while the Guide Scene is visible.
    /// Its hierarchy is authored in Guide Controller Tracked Visual.prefab and the
    /// instance is parented to the tracked left controller at runtime.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class GuideTrackedControllerVisual : MonoBehaviour
    {
        [SerializeField] private Transform _triggerTarget;
        [SerializeField] private Transform _stickTarget;
        [SerializeField] private Transform _xButtonTarget;
        [SerializeField] private Transform _yButtonTarget;
        [SerializeField] private Material _highlightMaterial;

        private readonly Dictionary<Renderer, Material[]> _originalMaterials = new();
        private readonly List<Renderer> _highlightedRenderers = new();

        private void Awake()
        {
            CacheOriginalMaterials(_triggerTarget);
            CacheOriginalMaterials(_stickTarget);
            CacheOriginalMaterials(_xButtonTarget);
            CacheOriginalMaterials(_yButtonTarget);
        }

        private void OnDisable()
        {
            RestoreHighlights();
        }

        public void ShowStep(int stepIndex)
        {
            RestoreHighlights();
            gameObject.SetActive(true);

            switch (Mathf.Clamp(stepIndex, 0, GuideControllerTutorialView.StepCount - 1))
            {
                case 0:
                    Highlight(_triggerTarget);
                    break;
                case 1:
                    Highlight(_stickTarget);
                    break;
                default:
                    Highlight(_xButtonTarget);
                    Highlight(_yButtonTarget);
                    break;
            }
        }

        public void Hide()
        {
            RestoreHighlights();
            gameObject.SetActive(false);
        }

        private void Highlight(Transform target)
        {
            if (target == null || _highlightMaterial == null) return;

            foreach (Renderer targetRenderer in target.GetComponentsInChildren<Renderer>(true))
            {
                if (targetRenderer == null) continue;
                CacheOriginalMaterial(targetRenderer);

                Material[] materials = targetRenderer.sharedMaterials;
                for (int index = 0; index < materials.Length; index++)
                    materials[index] = _highlightMaterial;
                targetRenderer.sharedMaterials = materials;
                _highlightedRenderers.Add(targetRenderer);
            }
        }

        private void RestoreHighlights()
        {
            foreach (Renderer targetRenderer in _highlightedRenderers)
            {
                if (targetRenderer != null && _originalMaterials.TryGetValue(targetRenderer, out Material[] materials))
                    targetRenderer.sharedMaterials = materials;
            }

            _highlightedRenderers.Clear();
        }

        private void CacheOriginalMaterials(Transform target)
        {
            if (target == null) return;
            foreach (Renderer targetRenderer in target.GetComponentsInChildren<Renderer>(true))
                CacheOriginalMaterial(targetRenderer);
        }

        private void CacheOriginalMaterial(Renderer targetRenderer)
        {
            if (targetRenderer != null && !_originalMaterials.ContainsKey(targetRenderer))
                _originalMaterials.Add(targetRenderer, targetRenderer.sharedMaterials);
        }
    }
}
