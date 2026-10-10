namespace TorchlightSDK.Host.Reading;

internal sealed class NoGameSource : IGameSource
{
    public bool TryRead(out GameState state, out string? failure)
    {
        state = GameState.Empty;
        failure = "no game reader yet - run with --fake to see synthetic data";
        return false;
    }

    public void Dispose()
    {
    }
}
