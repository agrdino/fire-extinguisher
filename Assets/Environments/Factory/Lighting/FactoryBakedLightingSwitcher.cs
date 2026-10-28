using UnityEngine;

namespace _Scripts.Environments.Factory
{
    [ExecuteAlways]
    [DisallowMultipleComponent]
    public sealed class FactoryBakedLightingSwitcher : MonoBehaviour
    {
        [SerializeField] private FactoryLightingScenario _normalScenario;
        [SerializeField] private FactoryLightingScenario _blackoutScenario;
        [SerializeField] private bool _previewBlackoutInEditor;

        public bool IsBlackout { get; private set; }

        private void OnEnable()
        {
            SetBlackout(Application.isPlaying ? false : _previewBlackoutInEditor);
        }

        public void SetBlackout(bool isBlackout)
        {
            FactoryLightingScenario scenario = isBlackout ? _blackoutScenario : _normalScenario;
            if (scenario == null || !scenario.IsReady) return;

            IsBlackout = isBlackout;
            scenario.Apply();
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
