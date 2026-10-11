using System.Numerics;
using Aember.Host.Memory;

namespace Aember.Host.Reading;

internal sealed class LiveReader(IReadBackend memory, nint moduleBase, int firstAreaId = 0) : IGameSource
{
    private readonly LogicWorldReader _world = new(memory, moduleBase);
    private readonly TerrainReader _terrain = new(memory, firstAreaId);
    private readonly MinimapReader _minimap = new(memory, moduleBase);
    private readonly MapIconReader _icons = new(memory);
    private readonly ExploredReader _explored = new(memory);
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

        var rotation = _minimap.ReadRotationDegrees(_terrain.Generation);
        var heading = _minimap.ReadHeadingDegrees();
        var player = playerPosition is { } position ? new Player(position, Forward(UpdateForward(position), heading)) : null;
        if (player is null)
            _anchor = null;

        var positions = new Dictionary<int, Vector2>(entities.Length);
        foreach (var entity in entities)
            positions[entity.Id] = new Vector2(entity.Position.X, entity.Position.Y);

        var icons = _icons.Read(_minimap, positions, id => _world.TryReadEntityPosition(scene, id, out var at) ? new Vector2(at.X, at.Y) : null);
        var explored = _explored.Read(_minimap.Widget, scene);
        var status = $"{(player is null ? "live - no local player" : "live")} - entities {entities.Length}, map icons {icons.Length}, explored {(explored is null ? "off" : explored.Points.Length)}";
        state = new GameState(status, _terrain.Generation, player, entities, terrain, rotation)
        {
            MapIcons = icons,
            Explored = explored,
        };
        return true;
    }

    private static Vector2 Forward(Vector2 moved, float? heading) =>
        heading is { } degrees ? new Vector2(MathF.Cos(degrees * MathF.PI / 180), MathF.Sin(degrees * MathF.PI / 180)) : moved;

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
