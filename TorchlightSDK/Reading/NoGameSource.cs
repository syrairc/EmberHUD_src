namespace TorchlightSDK.Host.Reading;

internal sealed class NoGameSource : IGameSource
{
    public bool TryRead(out GameState state, out string? failure)
    {
        state = GameState.Empty;
        failure = "no game reader selected - run with --fake for synthetic data or --live to read the game";
        return false;
    }

    public void Dispose()
    {
    }
}
