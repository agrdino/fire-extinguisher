namespace _Scripts.SceneManagement
{
    public interface IApplicationNavigator
    {
        bool IsTransitioning { get; }
        SceneId CurrentEnvironmentScene { get; }

        bool TryEnterEnvironment(SceneId environmentScene);
        bool TryReturnToEnvironmentSelection();
        bool TryRestartCurrentEnvironment();
    }
}
