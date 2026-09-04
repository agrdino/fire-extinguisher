namespace _Scripts.SceneManagement
{
    public interface IApplicationNavigator
    {
        bool IsTransitioning { get; }

        bool TryEnterEnvironment(SceneId environmentScene);
        bool TryReturnToEnvironmentSelection();
        bool TryRestartCurrentEnvironment();
    }
}
