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

        [Header("Transition")]
        [SerializeField] private Camera _transitionCamera;
        [SerializeField] private Color _transitionColor = Color.black;
        [SerializeField, Min(0f)] private float _fadeToBlackDuration = 0.8f;
        [SerializeField, Min(0f)] private float _fadeFromBlackDuration = 0.8f;

        private SceneTransitionFader _transitionFader;

        public bool IsTransitioning { get; private set; }

        public event Action<SceneId> SceneLoadStarted;
        public event Action<SceneId, Scene> SceneLoadCompleted;
        public event Action<SceneId, string> SceneLoadFailed;

        private void Awake()
        {
            _transitionFader = GetComponent<SceneTransitionFader>();
            if (_transitionFader == null)
                _transitionFader = gameObject.AddComponent<SceneTransitionFader>();

            _transitionFader.Configure(
                _transitionCamera != null ? _transitionCamera : Camera.main,
                _transitionColor);
        }

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

            IsTransitioning = true;
            StartCoroutine(LoadScene(sceneId, scenePath));
            return true;
        }

        public bool TryRunTransition(Action actionWhileBlack)
        {
            if (IsTransitioning || actionWhileBlack == null) return false;

            IsTransitioning = true;
            StartCoroutine(RunTransition(actionWhileBlack));
            return true;
        }

        private IEnumerator LoadScene(SceneId sceneId, string scenePath)
        {
            SceneLoadStarted?.Invoke(sceneId);
            yield return _transitionFader.FadeToBlack(_fadeToBlackDuration);

            AsyncOperation operation = SceneManager.LoadSceneAsync(scenePath, LoadSceneMode.Single);
            if (operation == null)
            {
                yield return _transitionFader.FadeFromBlack(_fadeFromBlackDuration);
                Fail(sceneId, $"Unity could not start loading scene: {scenePath}");
                yield break;
            }

            while (!operation.isDone) yield return null;

            Scene loadedScene = SceneManager.GetSceneByPath(scenePath);
            if (!loadedScene.IsValid() || !loadedScene.isLoaded)
            {
                yield return _transitionFader.FadeFromBlack(_fadeFromBlackDuration);
                Fail(sceneId, $"Unity finished the load operation but the scene is unavailable: {scenePath}");
                yield break;
            }

            yield return _transitionFader.FadeFromBlack(_fadeFromBlackDuration);

            IsTransitioning = false;
            SceneLoadCompleted?.Invoke(sceneId, loadedScene);
        }

        private IEnumerator RunTransition(Action actionWhileBlack)
        {
            yield return _transitionFader.FadeToBlack(_fadeToBlackDuration);

            try
            {
                actionWhileBlack.Invoke();
            }
            catch (Exception exception)
            {
                Debug.LogException(exception, this);
            }

            yield return _transitionFader.FadeFromBlack(_fadeFromBlackDuration);
            IsTransitioning = false;
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
