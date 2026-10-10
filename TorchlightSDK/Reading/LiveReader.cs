using System.Numerics;
using TorchlightSDK.Host.Memory;

namespace TorchlightSDK.Host.Reading;

internal sealed class LiveReader(IReadBackend memory, nint moduleBase, int firstAreaId = 0) : IGameSource
{
    private readonly LogicWorldReader _world = new(memory, moduleBase);
    private readonly TerrainReader _terrain = new(memory, firstAreaId);
    private const float MinMove = 0.5f;
    private Vector3? _anchor;
    private Vector2 _forward = new(0, 1);

    public bool TryRead(out GameState state, out string? failure)
    {
        state = GameState.Empty;
        if (!_world.TryReadScene(out var scene, out failure))
            return false;

        var terrain = _terrain.Read(scene);
        var (playerPosition, entities) = _world.ReadWorld(scene);
        var player = playerPosition is { } position ? new Player(position, UpdateForward(position)) : null;
        state = new GameState(player is null ? "live - no local player" : "live", _terrain.Generation, player, entities, terrain);
        return true;
    }

    private Vector2 UpdateForward(Vector3 position)
    {
        if (_anchor is { } anchor)
        {
            var delta = new Vector2(position.X - anchor.X, position.Y - anchor.Y);
            if (delta.Length() <= MinMove) return _forward;
            _forward = Vector2.Normalize(delta);
        }

        _anchor = position;
        return _forward;
    }

    public void Dispose()
    {
    }
}
