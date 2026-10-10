using System.Numerics;
using TorchlightSDK.Host.Memory;

namespace TorchlightSDK.Host.Reading;

internal sealed class LiveReader(IReadBackend memory, nint moduleBase, int firstAreaId = 0) : IGameSource
{
    private readonly LogicWorldReader _world = new(memory, moduleBase);
    private readonly TerrainReader _terrain = new(memory, firstAreaId);
    private readonly MinimapReader _minimap = new(memory, moduleBase);
    private int _generation = firstAreaId;
    private Vector3? _anchor;
    private Vector2 _forward = new(0, 1);

    public bool TryRead(out GameState state, out string? failure)
    {
        state = GameState.Empty;
        if (!_world.TryReadScene(out var scene, out failure))
            return false;

        var terrain = _terrain.Read(scene);
        var (playerPosition, entities) = _world.ReadWorld(scene);
        if (_terrain.Generation != _generation)
        {
            _generation = _terrain.Generation;
            _anchor = null;
        }

        var player = playerPosition is { } position ? new Player(position, UpdateForward(position)) : null;
        if (player is null)
            _anchor = null;
        state = new GameState(player is null ? "live - no local player" : "live", _terrain.Generation, player, entities, terrain, _minimap.ReadRotationDegrees(_terrain.Generation));
        return true;
    }

    private Vector2 UpdateForward(Vector3 position)
    {
        if (_anchor is { } anchor)
        {
            var delta = new Vector2(position.X - anchor.X, position.Y - anchor.Y);
            var distance = delta.Length();
            if (distance * 10 <= Offsets.MinMoveTenths) return _forward;
            if (distance <= Offsets.MaxStepPerRead)
                _forward = delta / distance;
        }

        _anchor = position;
        return _forward;
    }

    public void Dispose()
    {
    }
}
