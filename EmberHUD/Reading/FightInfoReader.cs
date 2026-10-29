using EmberHUD.Host.Memory;

namespace EmberHUD.Host.Reading;

internal sealed class FightInfoReader(IReadBackend memory, nint moduleBase, Func<long> clockMilliseconds)
{
    private const int NoSide = -1;
    private const int MaxLevel = 1000;
    private const uint IdLimit = 100_000_000;

    private readonly record struct Cached(FightInfo? Info, long RefreshAt);

    private readonly FightComponents _fight = new(memory, moduleBase);
    private readonly Dictionary<int, Cached> _cache = [];
    private long? _generation;

    public FightInfo? Read(int entityId, long generation)
    {
        if (_generation != generation)
        {
            _cache.Clear();
            _generation = generation;
        }

        var now = clockMilliseconds();
        if (_cache.TryGetValue(entityId, out var cached) && now < cached.RefreshAt) return cached.Info;

        var refreshAt = now + Offsets.FightInfoCampRefreshMilliseconds;
        var info = cached.Info is { } known ? RefreshCamp(entityId, known) : ReadFull(entityId);
        _cache[entityId] = new Cached(info, refreshAt);
        return info;
    }

    private FightInfo? RefreshCamp(int entityId, FightInfo known) =>
        TryFind(entityId, out var fighter) ? known with { Camp = ReadCamp(fighter) } : known;

    private FightInfo? ReadFull(int entityId)
    {
        if (!TryFind(entityId, out var fighter)) return null;

        var components = _fight.ReadAll(fighter);
        if (components.Count == 0) return null;

        var archetype = FightClassifier.Classify(components.Keys.ToHashSet());
        return new FightInfo(archetype, ReadCamp(fighter), ReadLevel(components), ReadSource(components));
    }

    private bool TryFind(int entityId, out nint fighter)
    {
        fighter = 0;
        return _fight.TryReadWorld(out var world) && _fight.TryFindFighter(world, entityId, out fighter);
    }

    private int ReadCamp(nint fighter) =>
        _fight.TryReadComponent(fighter, Offsets.SideComponentId, out var side) && memory.TryRead(side + Offsets.SideCamp, out byte camp) ? camp : NoSide;

    private int ReadLevel(Dictionary<int, nint> components) =>
        components.TryGetValue(Offsets.RoleComponentId, out var role) && memory.TryRead(role + Offsets.FightRoleLevel, out int level) && level is >= 0 and <= MaxLevel ? level : 0;

    private int ReadSource(Dictionary<int, nint> components) =>
        components.TryGetValue(Offsets.NpcComponentId, out var npc) && memory.TryRead(npc + Offsets.NpcSource, out uint source) && source < IdLimit ? (int)source : 0;
}
