using System;
using UnityEngine;

namespace _Scripts.Environments.Factory
{
    [ExecuteAlways]
    [DisallowMultipleComponent]
    public sealed class FactoryBakedLightingSwitcher : MonoBehaviour
    {
        [SerializeField] private FactoryLightingScenario _normalScenario;
        [SerializeField] private FactoryLightingScenario _blackoutScenario;

        [Header("Direct Lightmap Textures")]
        [Tooltip("Lightmap color textures used while the factory power is on, in lightmap index order.")]
        [SerializeField] private Texture2D[] _lightLightmaps = Array.Empty<Texture2D>();
        [Tooltip("Lightmap color textures used while the factory power is off, in lightmap index order.")]
        [SerializeField] private Texture2D[] _darkLightmaps = Array.Empty<Texture2D>();

        [SerializeField] private bool _previewBlackoutInEditor;

        public bool IsBlackout { get; private set; }

        private void OnEnable()
        {
            SetBlackout(Application.isPlaying ? false : _previewBlackoutInEditor);
        }

        public void SetBlackout(bool isBlackout)
        {
            FactoryLightingScenario scenario = isBlackout ? _blackoutScenario : _normalScenario;
            bool applied = scenario != null && scenario.IsReady;
            if (applied)
                scenario.Apply();
            else
                applied = ApplyDirectLightmaps(isBlackout ? _darkLightmaps : _lightLightmaps);

            if (!applied) return;
            IsBlackout = isBlackout;
        }

        private static bool ApplyDirectLightmaps(Texture2D[] colorTextures)
        {
            if (colorTextures == null || colorTextures.Length == 0) return false;

            for (int index = 0; index < colorTextures.Length; index++)
            {
                if (colorTextures[index] == null) return false;
            }

            LightmapData[] currentLightmaps = LightmapSettings.lightmaps;
            var updatedLightmaps = new LightmapData[colorTextures.Length];
            for (int index = 0; index < colorTextures.Length; index++)
            {
                LightmapData current = currentLightmaps != null && index < currentLightmaps.Length
                    ? currentLightmaps[index]
                    : null;
                updatedLightmaps[index] = new LightmapData
                {
                    lightmapColor = colorTextures[index],
                    lightmapDir = current?.lightmapDir,
                    shadowMask = current?.shadowMask
                };
            }

            LightmapSettings.lightmaps = updatedLightmaps;
            return true;
        }

#if UNITY_EDITOR
        public void ConfigureEditor(
            FactoryLightingScenario normalScenario,
            FactoryLightingScenario blackoutScenario)
        {
            _normalScenario = normalScenario;
            _blackoutScenario = blackoutScenario;
            _previewBlackoutInEditor = false;
        }

        [ContextMenu("Preview Normal Lighting")]
        private void PreviewNormalLighting()
        {
            _previewBlackoutInEditor = false;
            SetBlackout(false);
        }

        [ContextMenu("Preview Blackout Lighting")]
        private void PreviewBlackoutLighting()
        {
            _previewBlackoutInEditor = true;
            SetBlackout(true);
        }
#endif
    }
}
