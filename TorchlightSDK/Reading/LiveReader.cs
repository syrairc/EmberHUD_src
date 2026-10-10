using TorchlightSDK.Host.Memory;

namespace TorchlightSDK.Host.Reading;

internal sealed class LiveReader(IReadBackend memory, nint moduleBase, int firstAreaId = 0) : IGameSource
{
    private readonly LogicWorldReader _world = new(memory, moduleBase);
    private readonly TerrainReader _terrain = new(memory, firstAreaId);

    public bool TryRead(out GameState state, out string? failure)
    {
        state = GameState.Empty;
        if (!_world.TryReadScene(out var scene, out failure))
            return false;

        var terrain = _terrain.Read(scene);
        state = new GameState("live - local player not pinned yet", _terrain.Generation, null, _world.ReadEntities(scene), terrain);
        return true;
    }

    public void Dispose()
    {
    }
}
