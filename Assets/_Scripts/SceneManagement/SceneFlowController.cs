using System;
using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace _Scripts.SceneManagement
{
    [DisallowMultipleComponent]
    public sealed class SceneFlowController : MonoBehaviour, ISceneNavigator
    {
        [SerializeField] private SceneCatalog _catalog;

        public bool IsTransitioning { get; private set; }

        public event Action<SceneId> SceneLoadStarted;
        public event Action<SceneId, Scene> SceneLoadCompleted;
        public event Action<SceneId, string> SceneLoadFailed;

        public bool TryLoad(SceneId sceneId)
        {
            if (IsTransitioning) return false;

            if (_catalog == null)
                return Fail(sceneId, "SceneFlowController requires a SceneCatalog.");

            if (!_catalog.TryGetScenePath(sceneId, out string scenePath))
                return Fail(sceneId, $"Scene Catalog has no assigned scene for {sceneId}.");

            if (!Application.CanStreamedLevelBeLoaded(scenePath))
                return Fail(sceneId, $"Scene is not enabled in Build Settings: {scenePath}");

            Scene activeScene = SceneManager.GetActiveScene();
            if (string.Equals(activeScene.path, scenePath, StringComparison.OrdinalIgnoreCase))
            {
                SceneLoadCompleted?.Invoke(sceneId, activeScene);
                return true;
            }

            StartCoroutine(LoadScene(sceneId, scenePath));
            return true;
        }

        private IEnumerator LoadScene(SceneId sceneId, string scenePath)
        {
            IsTransitioning = true;
            SceneLoadStarted?.Invoke(sceneId);

            AsyncOperation operation = SceneManager.LoadSceneAsync(scenePath, LoadSceneMode.Single);
            if (operation == null)
            {
                Fail(sceneId, $"Unity could not start loading scene: {scenePath}");
                yield break;
            }

            while (!operation.isDone) yield return null;

            IsTransitioning = false;
            Scene loadedScene = SceneManager.GetSceneByPath(scenePath);
            if (!loadedScene.IsValid() || !loadedScene.isLoaded)
            {
                Fail(sceneId, $"Unity finished the load operation but the scene is unavailable: {scenePath}");
                yield break;
            }

            SceneLoadCompleted?.Invoke(sceneId, loadedScene);
        }

        private bool Fail(SceneId sceneId, string message)
        {
            IsTransitioning = false;
            Debug.LogError(message, this);
            SceneLoadFailed?.Invoke(sceneId, message);
            return false;
        }
    }
}
