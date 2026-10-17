using System.Numerics;
using EmberHUD.Host.Memory;

namespace EmberHUD.Host.Reading;

internal sealed class LiveReader
{
    private readonly IReadBackend _memory;
    private readonly LogicWorldReader _world;
    private readonly TerrainReader _terrain;
    private readonly MinimapReader _minimap;
    private readonly MapIconReader _icons;
    private readonly QuestTargetReader _questTargets;
    private readonly ExploredReader _explored;
    private readonly GameUiReader _ui;
    private readonly MapInfoReader _mapInfo;
    private readonly WoodStakeReader _woodStake;
    private readonly CombatReader? _combat;
    private readonly EntityNamer _namer;
    private readonly HpBarReader _hpBars;
    private int _generation;
    private Vector3? _anchor;
    private Vector2 _forward = new(0, 1);

    public LiveReader(IReadBackend memory, nint moduleBase, int firstAreaId = 0, CombatReader? combat = null)
    {
        _memory = memory;
        _combat = combat;
        var names = new Names(memory, moduleBase);
        _world = new LogicWorldReader(memory, moduleBase);
        _terrain = new TerrainReader(memory, firstAreaId);
        _minimap = new MinimapReader(memory, moduleBase, names);
        _icons = new MapIconReader(memory, moduleBase, names);
        _questTargets = new QuestTargetReader(memory);
        _explored = new ExploredReader(memory);
        _ui = new GameUiReader(memory, moduleBase, names);
        _mapInfo = new MapInfoReader(memory, moduleBase, new LocText(memory, moduleBase), _ui, names);
        _woodStake = new WoodStakeReader(memory, moduleBase);
        _namer = new EntityNamer(new LocText(memory, moduleBase));
        _hpBars = new HpBarReader(memory, moduleBase, names, _ui.TryOrigin);
        _generation = firstAreaId;
    }

    public bool TryRead(out GameState state, out string? failure)
    {
        state = GameState.Empty;
        if (!_world.TryReadScene(out var scene, out failure))
            return false;

        _memory.TryReadPtr(scene + Offsets.SceneGeometry, out var geometry);
        _memory.TryReadPtr(scene + Offsets.SceneGridModule, out var gridModule);
        var terrain = _terrain.Read(geometry, gridModule);
        var (playerPosition, entities, pawn) = _world.ReadWorld(scene, _terrain.Generation);
        entities = _namer.Apply(entities);
        entities = _hpBars.Apply(entities);
        if (_terrain.Generation != _generation)
        {
            _generation = _terrain.Generation;
            _anchor = null;
            _hpBars.Reset();
        }

        if (Game.TakeMinimapRescan()) _minimap.Reset();
        var rotation = _minimap.ReadRotationDegrees(_terrain.Generation);
        var heading = _minimap.ReadHeadingDegrees();
        var player = playerPosition is { } position ? new Player(position, Forward(UpdateForward(position), heading))
        {
            Life = _world.PlayerVitals.Life,
            MaxLife = _world.PlayerVitals.MaxLife,
            Shield = _world.PlayerVitals.Shield,
            MaxShield = _world.PlayerVitals.MaxShield,
        } : null;
        if (player is null)
            _anchor = null;

        var monsters = 0;
        var positions = new Dictionary<int, Vector2>(entities.Length);
        foreach (var entity in entities)
        {
            positions[entity.Id] = new Vector2(entity.Position.X, entity.Position.Y);
            if (entity.Kind == EntityKind.Monster) monsters++;
        }

        var icons = _icons.Read(_minimap, positions, id => _world.TryReadEntityPosition(scene, id, out var at) ? new Vector2(at.X, at.Y) : null);
        var explored = _explored.Read(_minimap.Widget, geometry, _terrain.Generation);
        var status = $"{(player is null ? "live - no local player" : "live")} - entities {entities.Length}, map icons {icons.Length}, explored {(explored is null ? "off" : explored.Count)}{(_minimap.Scans > 0 ? $", scans {_minimap.Scans}" : "")}";
        state = new GameState
        {
            Status = status,
            AreaId = _terrain.Generation,
            Player = player,
            Entities = entities,
            Terrain = terrain,
            MinimapRotation = rotation,
            MapIcons = icons,
            QuestTargets = _questTargets.Read(_minimap),
            Explored = explored,
            Camera = pawn != 0 ? _world.ReadCamera(pawn) : null,
            Ui = _ui.Read(),
            Trade = _ui.ReadTrade(),
            Area = _mapInfo.Read(scene, _terrain.Generation, monsters, icons),
            WoodStake = _woodStake.Read(),
            Combat = _combat?.Snapshot,
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
}
