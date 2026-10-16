using System.Numerics;
using EmberHUD.Host.Memory;

namespace EmberHUD.Host.Reading;

internal sealed class HpBarReader(IReadBackend memory, nint moduleBase, Names names, Func<Vector2?> origin)
{
    private readonly SlateWidgets _slate = new(memory);
    private readonly Dictionary<int, nint> _components = [];
    private readonly Dictionary<int, nint> _monsters = [];
    private readonly List<(int Key, nint Value)> _pairs = [];

    public void Reset() => _components.Clear();

    public Entity[] Apply(Entity[] entities)
    {
        Entity[]? result = null;
        var loaded = false;
        Vector2? screenOrigin = null;
        for (var i = 0; i < entities.Length; i++)
        {
            var entity = entities[i];
            if (entity.Kind != EntityKind.Monster) continue;
            if (!loaded)
            {
                LoadMonsters();
                screenOrigin = origin();
                loaded = true;
            }

            var hasBar = TryBar(entity.Id, screenOrigin, out var bar);
            if (hasBar == entity.HasGameBar && bar == entity.GameBar) continue;
            result ??= [.. entities];
            result[i] = entity with { HasGameBar = hasBar, GameBar = bar };
        }

        return result ?? entities;
    }

    private void LoadMonsters()
    {
        _monsters.Clear();
        _pairs.Clear();
        if (!memory.TryReadPtr(moduleBase + Offsets.FightMgrGlobalRva, out var fightMgr)) return;
        memory.ReadTMapPointers(fightMgr + Offsets.FightMapMonster, _pairs);
        foreach (var (id, monster) in _pairs)
            _monsters[id] = monster;
    }

    private bool TryBar(int id, Vector2? screenOrigin, out ScreenRect? bar)
    {
        bar = null;
        if (!_monsters.TryGetValue(id, out var monster) || !TryComponent(id, monster, out var component) ||
            !TryWidget(component, out var widget, out var trackOffset))
            return false;

        if (screenOrigin is { } at && Visible(widget) && memory.TryReadPtr(widget + trackOffset, out var track) && track != 0 && Visible(track) &&
            _slate.TryWeak(track + Offsets.UWidgetSlate, out var slate) && _slate.TryRect(slate, at, out var rect))
            bar = new ScreenRect(rect.Min, rect.Max);
        return true;
    }

    private bool TryComponent(int id, nint monster, out nint component)
    {
        if (_components.TryGetValue(id, out component) && memory.TryReadPtr(component + Offsets.ComponentEntity, out var owner) && owner == monster)
            return true;

        _components.Remove(id);
        _pairs.Clear();
        memory.ReadTMapPointers(monster + Offsets.EntityComponents, _pairs);
        foreach (var (_, candidate) in _pairs)
            if (names.TryReadClassName(candidate, out var className) && className == Offsets.HpBarComponentClassName)
            {
                _components[id] = component = candidate;
                return true;
            }

        component = 0;
        return false;
    }

    private bool TryWidget(nint component, out nint widget, out int trackOffset)
    {
        widget = 0;
        trackOffset = 0;
        if (!memory.TryRead(component + Offsets.HpBarWidgetWeak, out uint index) ||
            !memory.TryRead(component + Offsets.HpBarWidgetWeak + Offsets.WeakPtrSerial, out int serial) || serial == 0 ||
            !memory.TryReadGObject(moduleBase, index, out widget, out var itemSerial) || itemSerial != serial ||
            !names.TryReadClassName(widget, out var className))
            return false;

        if (className == Offsets.HpBarNormalClassName) trackOffset = Offsets.HpBarNormalTrack;
        else if (className == Offsets.HpBarEliteClassName) trackOffset = Offsets.HpBarEliteTrack;
        return trackOffset != 0;
    }

    private bool Visible(nint widget) =>
        memory.TryRead(widget + Offsets.UWidgetVisibility, out byte visibility) && visibility is 0 or 3 or 4;
}
