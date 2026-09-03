using UnityEngine;

namespace _Scripts.SceneManagement
{
    /// <summary>
    /// Ensures an environment scene can also be started directly in the Editor.
    /// In the normal application flow, the persistent runtime is created by Start Scene
    /// and this component deliberately does nothing.
    /// </summary>
    [DefaultExecutionOrder(-20000)]
    [DisallowMultipleComponent]
    public sealed class SceneRuntimeBootstrap : MonoBehaviour
    {
        [SerializeField] private PersistentRuntimeRoot _runtimePrefab;

        private void Awake()
        {
            if (PersistentRuntimeRoot.Instance != null) return;

            if (_runtimePrefab == null)
            {
                Debug.LogError(
                    $"{nameof(SceneRuntimeBootstrap)} requires an Application Runtime prefab.",
                    this);
                return;
            }

            Instantiate(_runtimePrefab);
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            if (_runtimePrefab == null)
                Debug.LogError(
                    $"{nameof(SceneRuntimeBootstrap)} requires an Application Runtime prefab.",
                    this);
        }
#endif
    }
}
