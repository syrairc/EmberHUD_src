using System.Globalization;
using EmberHUD.Host.Memory;

namespace EmberHUD.Host.Reading;

internal sealed class MapInfoReader(IReadBackend memory, nint moduleBase, LocText loc, GameUiReader ui, Names names, Func<long>? clockMilliseconds = null)
{
    private readonly record struct ContentSpec(string Name, bool FromModifier, int Id, string Icon, string State, string ClaimKey);

    private static readonly HashSet<int> TownTypes = [.. Offsets.TownSceneTypes.Split(' ', StringSplitOptions.RemoveEmptyEntries).Select(s => int.Parse(s, CultureInfo.InvariantCulture))];

    private static readonly ContentSpec[] Specs = [.. Offsets.MapContent
        .Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
        .Select(entry => entry.Split(',', StringSplitOptions.TrimEntries))
        .Select(f => new ContentSpec(f[0], f[1] == "mod", int.Parse(f[2], CultureInfo.InvariantCulture), f[3], f[4], f[5]))];

    private static readonly string[] TarotNameIds = Offsets.TarotNameIds.Split(' ', StringSplitOptions.RemoveEmptyEntries);

    private readonly record struct StatueSpec(string Class, string NameKey, string Icon, string Component, int ActiveOffset, string ClaimKey);

    private static readonly Dictionary<string, StatueSpec> StatueSpecs = Offsets.Statues
        .Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
        .Select(entry => entry.Split(',', StringSplitOptions.TrimEntries))
        .Select(f => new StatueSpec(f[0], f[1], f[2], f[3], f[4].StartsWith("0x", StringComparison.OrdinalIgnoreCase) ? Convert.ToInt32(f[4], 16) : int.Parse(f[4], CultureInfo.InvariantCulture), f[5]))
        .ToDictionary(s => s.Class);

    private readonly Func<long> _clock = clockMilliseconds ?? (() => Environment.TickCount64);
    private readonly record struct SeenStatue(StatueSpec Spec, ContentState State, int Absent);

    private readonly Dictionary<int, SeenStatue> _statuesSeen = [];
    private MapInfo? _last;
    private int _lastArea = int.MinValue;
    private long _nextAt;
    private Dictionary<int, int> _tarotDone = [];
    private readonly HashSet<int> _tarotSeen = [];
    private readonly HashSet<int> _fugitiveSeen = [];
    private int _fugitiveStanding;

    public MapInfo? Read(nint scene, int areaId, int aliveMonsters, MapIcon[] icons)
    {
        var now = _clock();
        if (areaId == _lastArea && now < _nextAt) return _last;
        _nextAt = now + Offsets.MapInfoRefreshMilliseconds;
        if (areaId != _lastArea)
        {
            _tarotDone = [];
            _tarotSeen.Clear();
            _fugitiveSeen.Clear();
            _fugitiveStanding = 0;
            _statuesSeen.Clear();
            ui.ForgetHud();
        }
        _lastArea = areaId;
        return _last = TryRead(scene, aliveMonsters, icons);
    }

    private IEnumerable<MapContent> TarotRows((int Type, nint Record)[] gameplays, MapIcon[] icons)
    {
        var byType = gameplays.Where(g => g.Type >= Offsets.TarotFirstType && g.Type < Offsets.TarotFirstType + TarotNameIds.Length).GroupBy(c => c.Type).OrderBy(g => g.Key).ToArray();
        if (byType.Length == 0) return [];
        if (icons.Length > 0)
        {
            var standing = icons.Where(i => i.Type == Offsets.TarotIconType).Select(i => i.EntityId).ToHashSet();
            _tarotSeen.UnionWith(standing);
            _tarotDone = byType.ToDictionary(g => g.Key, g => g.Count(c => TryVar(c.Record, Offsets.TarotEntityVar, out var id) && id != 0 && _tarotSeen.Contains(id) && !standing.Contains(id)));
        }
        return byType.Select(g =>
        {
            var total = g.Count();
            var done = Math.Min(_tarotDone.GetValueOrDefault(g.Key), total);
            var state = done == total ? ContentState.Done : done > 0 ? ContentState.Active : ContentState.Pending;
            return new MapContent(loc.Get(Offsets.TarotNameKey + TarotNameIds[g.Key - Offsets.TarotFirstType]) ?? $"Tarot {g.Key}", Offsets.TarotIcon, state, done, total);
        });
    }

    private MapContent FugitiveRow(ContentSpec spec, MapIcon[] icons)
    {
        if (icons.Length > 0)
        {
            var standing = icons.Where(i => i.Type == Offsets.FugitiveIconType).Select(i => i.EntityId).ToHashSet();
            _fugitiveSeen.UnionWith(standing);
            _fugitiveStanding = standing.Count;
        }
        var total = _fugitiveSeen.Count;
        var done = total - _fugitiveStanding;
        return new MapContent(spec.Name, spec.Icon, total == 0 || done == 0 ? ContentState.Pending : done == total ? ContentState.Done : ContentState.Active, done, total);
    }

    private MapContent[] StatueRows()
    {
        if (memory.TryReadPtr(moduleBase + Offsets.FightMgrGlobalRva, out var fightMgr) && fightMgr != 0)
        {
            HashSet<int> present = [];
            foreach (var (id, entity) in memory.ReadTMapPointers(fightMgr + Offsets.FightMapGamePlay))
                if (names.TryReadClassName(entity, out var cls) && StatueSpecs.TryGetValue(cls, out var spec))
                {
                    present.Add(id);
                    _statuesSeen[id] = new(spec, IsActive(spec, entity) ? ContentState.Active : ContentState.Pending, 0);
                }

            foreach (var (id, seen) in _statuesSeen.Where(s => !present.Contains(s.Key)).ToArray())
                _statuesSeen[id] = seen with { Absent = seen.Absent + 1, State = seen.Absent + 1 >= Offsets.StatueAbsentRefreshes ? ContentState.Done : seen.State };
        }

        return [.. _statuesSeen.OrderBy(s => s.Key).Select(s => new MapContent(
            (s.Value.Spec.NameKey == "" ? null : loc.Get(s.Value.Spec.NameKey)) ?? $"Statue of {s.Value.Spec.Class.Replace(Offsets.StatueClassPrefix, "")}",
            s.Value.Spec.Icon,
            s.Value.State))];
    }

    private bool IsActive(StatueSpec spec, nint entity) =>
        spec.Component != "" && memory.ReadTMapPointers(entity + Offsets.EntityComponents).Any(c => names.TryReadClassName(c.Value, out var cls) && cls == spec.Component && memory.TryRead(c.Value + spec.ActiveOffset, out byte on) && on != 0);

    private MapInfo? TryRead(nint scene, int aliveMonsters, MapIcon[] icons)
    {
        if (!memory.TryReadPtr(scene + Offsets.SceneGeometry, out var info) || info == 0 ||
            !memory.TryRead(info + Offsets.GeometryLevelId, out int levelId) ||
            !memory.TryRead(info + Offsets.GeometrySceneType, out int sceneType) ||
            !memory.TryRead(info + Offsets.GeometryMonsterLevel, out int monsterLevel))
            return null;

        memory.TryReadPtr(scene + Offsets.SceneGridModule, out var module);
        int? remaining = module != 0 && memory.TryRead(module + Offsets.GridModuleToSpawn, out int toSpawn) && toSpawn >= 0 && toSpawn <= Offsets.MaxRemaining
            ? toSpawn + (Offsets.RemainingAddsAlive == 1 ? aliveMonsters : 0)
            : null;

        var modifierIds = module == 0 ? [] : ReadModifierIds(module);
        var gameplays = ReadGameplays(scene);
        var types = gameplays.Select(g => g.Type).ToHashSet();
        var found = Specs.Where(s => s.FromModifier ? modifierIds.Contains(s.Id) : types.Contains(s.Id)).ToArray();
        var statues = StatueRows();
        var claimed = found.Where(s => s.FromModifier).Select(s => loc.Get(s.ClaimKey.Length > 0 ? s.ClaimKey : Offsets.ModifierTextKey + s.Id))
            .Concat(_statuesSeen.Values.Where(s => s.Spec.ClaimKey.Length > 0).Select(s => loc.Get(s.Spec.ClaimKey)))
            .OfType<string>().ToHashSet();

        return new MapInfo(
            loc.Get(Offsets.LevelNameKey + levelId) ?? "",
            monsterLevel,
            remaining,
            XpOf(ui.HudText(Offsets.MapXpWidget, Offsets.ShrinkTextContent)),
            TownTypes.Contains(sceneType),
            [.. found.Select(s => s.State == "fugitive" ? FugitiveRow(s, icons) : new MapContent(s.Name, s.Icon, StateOf(s, gameplays))), .. TarotRows(gameplays, icons), .. statues],
            [.. ui.HudTexts(Offsets.MapAffixWidget, Offsets.TextBlockText).Where(line => !claimed.Contains(line))]);
    }

    internal static string? XpOf(string? text)
    {
        var at = text?.LastIndexOf(": ", StringComparison.Ordinal) ?? -1;
        return at < 0 ? null : text![(at + 2)..].Trim() is { Length: > 0 } value ? value : null;
    }

    private HashSet<int> ReadModifierIds(nint module)
    {
        if (!memory.TryRead(module + Offsets.GridModuleModifierCount, out uint count) || count > Offsets.MaxModifiers ||
            !memory.TryReadPtr(module + Offsets.GridModuleModifiers, out var entries) || entries == 0)
            return [];
        HashSet<int> ids = [];
        for (var i = 0; i < count; i++)
            if (memory.TryRead(entries + i * Offsets.ModifierStride + Offsets.ModifierId, out int id))
                ids.Add(id);
        return ids;
    }

    private (int Type, nint Record)[] ReadGameplays(nint scene)
    {
        if (!memory.TryRead(scene + Offsets.SceneGameplayCount, out uint count) || count > Offsets.MaxGameplays ||
            !memory.TryReadPtr(scene + Offsets.SceneGameplays, out var array) || array == 0 ||
            !memory.TryReadArray(array, (int)count, out nint[] records))
            return [];
        return [.. records.Where(r => r != 0).Select(r => (Type: memory.TryRead(r + Offsets.GameplayType, out int type) ? type : -1, Record: r)).Where(g => g.Type >= 0)];
    }

    private ContentState StateOf(ContentSpec spec, (int Type, nint Record)[] gameplays) => spec.State switch
    {
        "vigil" => AnyVar(gameplays, Offsets.VigilDoneVar) ? ContentState.Done : AnyVar(gameplays, Offsets.VigilStartedVar) ? ContentState.Active : ContentState.Pending,
        _ => ContentState.Pending,
    };

    private bool AnyVar((int Type, nint Record)[] gameplays, string name) => gameplays.Any(g => TryVar(g.Record, name, out var value) && value != 0);

    private bool TryVar(nint record, string name, out int value)
    {
        var found = 0;
        var hit = false;
        var prefix = name + "_";
        memory.TryWalkBuckets(record + Offsets.GameplayVarBuckets + Offsets.HashBucketCount, record + Offsets.GameplayVarBuckets, Offsets.MaxVarBuckets, Offsets.VarNodeSize, Offsets.VarNodeNext, int.MaxValue,
            (_, node) =>
            {
                if (VarName(node) is not { } varName || varName != name && !varName.StartsWith(prefix, StringComparison.Ordinal))
                    return true;

                found = BitConverter.ToInt32(node[Offsets.VarNodeValue..]);
                hit = true;
                return false;
            });
        value = found;
        return hit;
    }

    private string? VarName(ReadOnlySpan<byte> node)
    {
        var length = BitConverter.ToInt32(node[Offsets.VarNodeNameLength..]);
        var at = (nint)BitConverter.ToInt64(node[Offsets.VarNodeName..]);
        return length > 0 && length <= Offsets.MaxVarNameLength && at != 0 && memory.TryReadArray(at, length, out byte[] bytes)
            ? System.Text.Encoding.UTF8.GetString(bytes) : null;
    }
}
