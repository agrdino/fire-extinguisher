using System;
using UnityEngine;
using UnityEngine.Rendering;

namespace _Scripts.Environments.Factory
{
    [CreateAssetMenu(
        fileName = "Factory Lighting Scenario",
        menuName = "Fire Extinguisher/Factory Lighting Scenario")]
    public sealed class FactoryLightingScenario : ScriptableObject
    {
        private const int ProbeCoefficientCount = 27;

        [Header("Baked Lightmaps")]
        [SerializeField] private LightmapsMode _lightmapsMode = LightmapsMode.NonDirectional;
        [SerializeField] private Texture2D[] _colorLightmaps = Array.Empty<Texture2D>();
        [SerializeField] private Texture2D[] _directionalLightmaps = Array.Empty<Texture2D>();
        [SerializeField] private Texture2D[] _shadowMasks = Array.Empty<Texture2D>();

        [Header("Baked Light Probes")]
        [SerializeField, Min(0)] private int _bakedProbeCount;
        [SerializeField] private float[] _bakedProbeCoefficients = Array.Empty<float>();

        [Header("Environment")]
        [SerializeField, Min(0f)] private float _ambientIntensity = 1f;
        [SerializeField, Min(0f)] private float _reflectionIntensity = 1f;

        [SerializeField, HideInInspector] private string _rendererLayoutSignature = string.Empty;

        public int LightmapCount => _colorLightmaps?.Length ?? 0;
        public int BakedProbeCount => _bakedProbeCount;
        public string RendererLayoutSignature => _rendererLayoutSignature;
        public bool IsReady => LightmapCount > 0;

        public void Apply()
        {
            if (!IsReady) return;

            int lightmapCount = _colorLightmaps.Length;
            var lightmaps = new LightmapData[lightmapCount];

            for (int index = 0; index < lightmapCount; index++)
            {
                lightmaps[index] = new LightmapData
                {
                    lightmapColor = _colorLightmaps[index],
                    lightmapDir = GetTexture(_directionalLightmaps, index),
                    shadowMask = GetTexture(_shadowMasks, index)
                };
            }

            LightmapSettings.lightmapsMode = _lightmapsMode;
            LightmapSettings.lightmaps = lightmaps;
            ApplyBakedProbes();

            RenderSettings.ambientIntensity = _ambientIntensity;
            RenderSettings.reflectionIntensity = _reflectionIntensity;
        }

        private void ApplyBakedProbes()
        {
            LightProbes lightProbes = LightmapSettings.lightProbes;
            if (lightProbes == null || _bakedProbeCount <= 0) return;
            if (lightProbes.count != _bakedProbeCount) return;
            if (_bakedProbeCoefficients == null ||
                _bakedProbeCoefficients.Length != _bakedProbeCount * ProbeCoefficientCount)
                return;

            var probes = new SphericalHarmonicsL2[_bakedProbeCount];
            int coefficientIndex = 0;

            for (int probeIndex = 0; probeIndex < probes.Length; probeIndex++)
            {
                var probe = new SphericalHarmonicsL2();
                for (int channel = 0; channel < 3; channel++)
                {
                    for (int coefficient = 0; coefficient < 9; coefficient++)
                        probe[channel, coefficient] = _bakedProbeCoefficients[coefficientIndex++];
                }

                probes[probeIndex] = probe;
            }

            lightProbes.bakedProbes = probes;
        }

        private static Texture2D GetTexture(Texture2D[] textures, int index)
        {
            return textures != null && index < textures.Length ? textures[index] : null;
        }

#if UNITY_EDITOR
        public void CaptureEditorData(
            LightmapsMode lightmapsMode,
            Texture2D[] colorLightmaps,
            Texture2D[] directionalLightmaps,
            Texture2D[] shadowMasks,
            SphericalHarmonicsL2[] bakedProbes,
            float ambientIntensity,
            float reflectionIntensity,
            string rendererLayoutSignature)
        {
            _lightmapsMode = lightmapsMode;
            _colorLightmaps = colorLightmaps ?? Array.Empty<Texture2D>();
            _directionalLightmaps = directionalLightmaps ?? Array.Empty<Texture2D>();
            _shadowMasks = shadowMasks ?? Array.Empty<Texture2D>();
            _ambientIntensity = Mathf.Max(0f, ambientIntensity);
            _reflectionIntensity = Mathf.Max(0f, reflectionIntensity);
            _rendererLayoutSignature = rendererLayoutSignature ?? string.Empty;

            _bakedProbeCount = bakedProbes?.Length ?? 0;
            _bakedProbeCoefficients = new float[_bakedProbeCount * ProbeCoefficientCount];

            int coefficientIndex = 0;
            for (int probeIndex = 0; probeIndex < _bakedProbeCount; probeIndex++)
            {
                for (int channel = 0; channel < 3; channel++)
                {
                    for (int coefficient = 0; coefficient < 9; coefficient++)
                        _bakedProbeCoefficients[coefficientIndex++] = bakedProbes[probeIndex][channel, coefficient];
                }
            }
        }
#endif
    }
}
