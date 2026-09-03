using System;
using UnityEngine.SceneManagement;

namespace _Scripts.SceneManagement
{
    public interface ISceneNavigator
    {
        bool IsTransitioning { get; }
        event Action<SceneId> SceneLoadStarted;
        event Action<SceneId, Scene> SceneLoadCompleted;
        event Action<SceneId, string> SceneLoadFailed;

        bool TryLoad(SceneId sceneId);
    }
}
