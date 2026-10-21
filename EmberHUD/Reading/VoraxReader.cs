using System.Diagnostics;
using System.Globalization;
using EmberHUD.Host.Memory;

namespace EmberHUD.Host.Reading;

internal sealed class VoraxReader(IReadBackend memory, nint moduleBase, Func<string, string?>? loc = null, Func<long>? clock = null)
{
    private const int MaxPages = 32;

    private readonly Func<long> _clock = clock ?? Stopwatch.GetTimestamp;
    private readonly LuaReader _lua = new(memory, moduleBase);
    private long _nextRefresh;
    private VoraxState? _state;

    public VoraxState? Read()
    {
        var now = _clock();
        if (now < _nextRefresh) return _state;
        _nextRefresh = now + Stopwatch.Frequency * Offsets.VoraxRefreshMilliseconds / 1000;
        return _state = Build();
    }

    private VoraxState? Build()
    {
        if (!TryOpenView(out var view, out var data)) return null;
        var result = Read(view, data);
        return _lua.TryGetTable(view, Offsets.LuaVoraxViewData, out var again) && again == data ? result : _state;
    }

    private bool TryOpenView(out LuaTable view, out LuaTable data)
    {
        view = data = default;
        return _lua.TryOpen(out var globals) &&
            _lua.TryGetTable(globals, Offsets.LuaVoraxViewManager, out var manager) &&
            _lua.TryGetTable(manager, Offsets.LuaVoraxViewList, out var list) &&
            TryFindPage(list, out var page) &&
            _lua.TryGetTable(page, Offsets.LuaVoraxObject, out var luaObject) &&
            _lua.TryGetTable(luaObject, Offsets.LuaVoraxView, out view) &&
            _lua.TryGetTable(view, Offsets.LuaVoraxViewData, out data);
    }

    private bool TryFindPage(LuaTable list, out LuaTable page)
    {
        page = default;
        var row = _lua.Entries(list, MaxPages, 0, Offsets.LuaVoraxPageSuffix, out _)
            .FirstOrDefault(r => r.Key.EndsWith(Offsets.LuaVoraxPageSuffix, StringComparison.Ordinal));
        return row.Key is not null && _lua.TryGetTable(list, row.Key, out page);
    }

    private VoraxState? Read(LuaTable view, LuaTable data)
    {
        if (!_lua.TryGetTable(data, Offsets.LuaVoraxBaseAttr, out var attrs) ||
            !TryAttr(attrs, Offsets.VoraxAttrRound, out var round) ||
            !TryAttr(attrs, Offsets.VoraxAttrRounds, out var rounds) ||
            round < 1 || rounds < 1 || round > rounds)
            return null;

        TryAttr(attrs, Offsets.VoraxAttrRerolls, out var rerolls);
        TryAttr(attrs, Offsets.VoraxAttrSlots, out var slots);
        TryGetInt(data, Offsets.LuaVoraxTotalScore, out var activity);
        TryGetInt(view, Offsets.LuaVoraxNextScore, out var next);
        TryGetInt(view, Offsets.LuaVoraxRewardCount, out var rewards);

        return new VoraxState(ReadPhase(view), round, rounds, activity, next, 0, rewards, rerolls, slots, ReadMonsters(data), [], [], []);
    }

    private VoraxPhase ReadPhase(LuaTable view) =>
        _lua.TryGetString(view, Offsets.LuaVoraxState, out var state) && state == Offsets.VoraxStateRound ? VoraxPhase.Round : VoraxPhase.Other;

    private VoraxMonster[] ReadMonsters(LuaTable data)
    {
        if (!_lua.TryGetTable(data, Offsets.LuaVoraxMonsters, out var bag)) return [];
        var rows = _lua.IntKeyedTables(bag).OrderBy(r => r.Key).Take(Offsets.MaxVoraxMonsters).ToArray();
        return [.. rows.Select((r, position) => (ok: TryReadMonster(r.Value, position, out var monster), monster)).Where(r => r.ok).Select(r => r.monster)];
    }

    private bool TryReadMonster(LuaTable row, int position, out VoraxMonster monster)
    {
        monster = default;
        if (!TryGetInt(row, Offsets.LuaVoraxId, out var id) || !TryGetInt(row, Offsets.LuaVoraxSpecies, out var species) ||
            !TryGetInt(row, Offsets.LuaVoraxCount, out var count) || !TryGetInt(row, Offsets.LuaVoraxActivity, out var activity) ||
            !TryGetInt(row, Offsets.LuaVoraxRarity, out var rarity) || count < 0 || activity < 0 || (long)count * activity > int.MaxValue)
            return false;

        var slot = TryGetInt(row, Offsets.LuaVoraxIndex, out var index) ? index : position;
        monster = new VoraxMonster(slot, id, species, rarity, count, activity)
        {
            Name = Text(Offsets.VoraxMonsterNameKey + id.ToString(CultureInfo.InvariantCulture)),
            RarityName = Text(Offsets.VoraxRarityNameKey + rarity.ToString(CultureInfo.InvariantCulture)),
        };
        return true;
    }

    private string Text(string key) => loc?.Invoke(key) ?? "";

    private bool TryAttr(LuaTable attrs, int index, out int value)
    {
        value = 0;
        if (!_lua.TryGetIntAt(attrs, index, out var raw) || raw < int.MinValue || raw > int.MaxValue) return false;
        value = (int)raw;
        return true;
    }

    private bool TryGetInt(LuaTable table, string key, out int value)
    {
        value = 0;
        if (!_lua.TryGetInt(table, key, out var raw) || raw < int.MinValue || raw > int.MaxValue) return false;
        value = (int)raw;
        return true;
    }
}
