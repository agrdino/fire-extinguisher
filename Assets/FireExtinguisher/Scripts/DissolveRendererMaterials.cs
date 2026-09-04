using System.Collections.Generic;
using UnityEngine;

namespace _Scripts.FireExtinguishers
{
    /// <summary>
    /// Replaces renderer materials with temporary dissolve copies, then restores
    /// the exact original material arrays after the transition.
    /// </summary>
    internal sealed class DissolveRendererMaterials
    {
        private static readonly int DissolveAmountId = Shader.PropertyToID("_Slider");
        private static readonly int DissolveMinYId = Shader.PropertyToID("_Dissolve_Min_Y");
        private static readonly int DissolveMaxYId = Shader.PropertyToID("_Dissolve_Max_Y");
        private static readonly HashSet<DissolveRendererMaterials> ActiveInstances = new();

        private readonly Material _template;
        private readonly List<RendererBinding> _bindings = new();
        private readonly List<Material> _runtimeMaterials = new();

        public DissolveRendererMaterials(Material template)
        {
            _template = template;
        }

        public bool Apply(IEnumerable<Renderer> renderers, float initialAmount)
        {
            Restore();
            if (_template == null || _template.shader == null || !_template.shader.isSupported || renderers == null)
                return false;

            foreach (Renderer renderer in renderers)
            {
                if (renderer == null) continue;

                Material[] originals = renderer.sharedMaterials;
                if (originals == null || originals.Length == 0) continue;

                Material[] replacements = new Material[originals.Length];
                bool hasReplacement = false;

                for (int index = 0; index < originals.Length; index++)
                {
                    Material source = originals[index];
                    if (source == null) continue;

                    Material dissolve = new Material(_template)
                    {
                        name = $"{source.name} (Dissolve Runtime)",
                        hideFlags = HideFlags.DontSave
                    };

                    CopyLitProperties(source, dissolve);
                    dissolve.SetFloat(DissolveAmountId, Mathf.Clamp01(initialAmount));

                    replacements[index] = dissolve;
                    _runtimeMaterials.Add(dissolve);
                    hasReplacement = true;
                }

                if (!hasReplacement) continue;

                _bindings.Add(new RendererBinding(renderer, originals));
                renderer.sharedMaterials = replacements;
            }

            if (_bindings.Count > 0)
            {
                ActiveInstances.Add(this);
                ApplySharedWorldYBounds();
            }

            return _bindings.Count > 0;
        }

        public void SetAmount(float amount)
        {
            float clampedAmount = Mathf.Clamp01(amount);
            for (int index = 0; index < _runtimeMaterials.Count; index++)
            {
                Material material = _runtimeMaterials[index];
                if (material != null) material.SetFloat(DissolveAmountId, clampedAmount);
            }
        }

        public void Restore()
        {
            ActiveInstances.Remove(this);

            for (int index = 0; index < _bindings.Count; index++)
            {
                RendererBinding binding = _bindings[index];
                if (binding.Renderer != null)
                    binding.Renderer.sharedMaterials = binding.OriginalMaterials;
            }

            _bindings.Clear();

            for (int index = 0; index < _runtimeMaterials.Count; index++)
            {
                Material material = _runtimeMaterials[index];
                if (material != null) Object.Destroy(material);
            }

            _runtimeMaterials.Clear();
            ApplySharedWorldYBounds();
        }

        private static void ApplySharedWorldYBounds()
        {
            bool hasBounds = false;
            float minWorldY = float.PositiveInfinity;
            float maxWorldY = float.NegativeInfinity;

            foreach (DissolveRendererMaterials instance in ActiveInstances)
            {
                for (int index = 0; index < instance._bindings.Count; index++)
                {
                    Renderer renderer = instance._bindings[index].Renderer;
                    if (renderer == null) continue;

                    Bounds bounds = renderer.bounds;
                    minWorldY = Mathf.Min(minWorldY, bounds.min.y);
                    maxWorldY = Mathf.Max(maxWorldY, bounds.max.y);
                    hasBounds = true;
                }
            }

            if (!hasBounds) return;

            foreach (DissolveRendererMaterials instance in ActiveInstances)
            {
                for (int index = 0; index < instance._runtimeMaterials.Count; index++)
                {
                    Material material = instance._runtimeMaterials[index];
                    if (material == null) continue;

                    material.SetFloat(DissolveMinYId, minWorldY);
                    material.SetFloat(DissolveMaxYId, maxWorldY);
                }
            }
        }

        private static void CopyLitProperties(Material source, Material target)
        {
            CopyTexture(source, "_BaseMap", target, "_MainTex");
            CopyTexture(source, "_BumpMap", target, "_NormalTex");
            CopyTexture(source, "_MetallicGlossMap", target, "_MetallicTex");
            CopyTexture(source, "_OcclusionMap", target, "_OcclusionTex");
        }

        private static void CopyTexture(Material source, string sourceName, Material target, string targetName)
        {
            if (!source.HasProperty(sourceName) || !target.HasProperty(targetName)) return;

            target.SetTexture(targetName, source.GetTexture(sourceName));
            target.SetTextureScale(targetName, source.GetTextureScale(sourceName));
            target.SetTextureOffset(targetName, source.GetTextureOffset(sourceName));
        }

        private readonly struct RendererBinding
        {
            public RendererBinding(Renderer renderer, Material[] originalMaterials)
            {
                Renderer = renderer;
                OriginalMaterials = originalMaterials;
            }

            public Renderer Renderer { get; }
            public Material[] OriginalMaterials { get; }
        }
    }
}
