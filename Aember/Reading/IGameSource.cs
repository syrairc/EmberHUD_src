namespace Aember.Host.Reading;

internal interface IGameSource : IDisposable
{
    bool TryRead(out GameState state, out string? failure);
}

internal sealed record ReaderSnapshot(GameState State, long PlayerAt);
