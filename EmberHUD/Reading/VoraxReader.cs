using System.Diagnostics;
using System.Globalization;
using EmberHUD.Host.Memory;

namespace EmberHUD.Host.Reading;

internal sealed class VoraxReader(IReadBackend memory, nint moduleBase, Func<string, string?>? loc = null, Func<long>? clock = null)
{
    private const int MaxPages = 32;
    private const int MaxChests = 16;
    private const int MaxDescParams = 16;

    private readonly record struct CardText(int Rarity, string Name, string Description, string TypeName)
    {
        public static readonly CardText Empty = new(0, "", "", "");
    }

    private readonly Dictionary<(VoraxCardKind, int), CardText> _texts = [];
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

        var manager = TryOpenManager(out var found) ? found : default;
        var hand = CardRows(data, Offsets.LuaVoraxHand);
        var chests = ReadChests(manager);
        return new VoraxState(ReadPhase(view, hand), round, rounds, activity, next, chests.Count(c => c != 0), rewards, rerolls, slots, ReadMonsters(data),
            ReadCards(manager, hand, false), ReadCards(manager, CardRows(data, Offsets.LuaVoraxRelics), true), chests);
    }

    private bool TryOpenManager(out LuaTable manager)
    {
        manager = default;
        return _lua.TryOpen(out var globals) && _lua.TryGetTable(globals, Offsets.LuaVoraxManager, out manager);
    }

    private VoraxPhase ReadPhase(LuaTable view, LuaTable[] hand) =>
        hand.Any(IsInitRelic) ? VoraxPhase.Instrument :
        _lua.TryGetString(view, Offsets.LuaVoraxState, out var state) && state == Offsets.VoraxStateRound ? VoraxPhase.Round : VoraxPhase.Other;

    private bool IsInitRelic(LuaTable row) => _lua.TryGetBool(row, Offsets.LuaVoraxInitRelic, out var init) && init;

    private LuaTable[] CardRows(LuaTable data, string key) =>
        _lua.TryGetTable(data, key, out var bag) ? [.. _lua.IntKeyedTables(bag).OrderBy(r => r.Key).Take(Offsets.MaxVoraxCards).Select(r => r.Value)] : [];

    private int[] ReadChests(LuaTable manager)
    {
        if (!_lua.TryGetTable(manager, Offsets.LuaVoraxChests, out var table)) return [];
        var chests = new List<int>();
        while (chests.Count < MaxChests && _lua.TryGetIntAt(table, chests.Count + 1, out var value) && value is >= int.MinValue and <= int.MaxValue) chests.Add((int)value);
        return [.. chests];
    }

    private VoraxCard[] ReadCards(LuaTable manager, LuaTable[] rows, bool owned) =>
        [.. rows.Select((row, position) => (ok: TryReadCard(manager, row, position, owned, out var card), card)).Where(r => r.ok).Select(r => r.card)];

    private bool TryReadCard(LuaTable manager, LuaTable row, int position, bool owned, out VoraxCard card)
    {
        card = default;
        if (!TryGetInt(row, Offsets.LuaVoraxId, out var id)) return false;
        var type = TryGetInt(row, Offsets.LuaVoraxType, out var raw) ? raw : 0;
        var kind = owned || IsInitRelic(row) ? VoraxCardKind.Instrument : KindOf(type);
        var slot = TryGetInt(row, Offsets.LuaVoraxIndex, out var index) ? index : position;
        var text = Resolve(manager, kind, id);
        card = new VoraxCard(slot, id, kind, text.Rarity) { Name = text.Name, Description = text.Description, TypeName = text.TypeName };
        return true;
    }

    private static VoraxCardKind KindOf(int type) =>
        type == Offsets.VoraxTypeMonster ? VoraxCardKind.Monster :
        type == Offsets.VoraxTypePotion ? VoraxCardKind.Potion :
        type == Offsets.VoraxTypeInstrument ? VoraxCardKind.Instrument :
        type == Offsets.VoraxTypeModifier ? VoraxCardKind.Modifier :
        type == Offsets.VoraxTypeBundleA || type == Offsets.VoraxTypeBundleB ? VoraxCardKind.Bundle :
        VoraxCardKind.Unknown;

    private static int ConfigTypeOf(VoraxCardKind kind) => kind switch
    {
        VoraxCardKind.Monster => Offsets.VoraxTypeMonster,
        VoraxCardKind.Potion => Offsets.VoraxTypePotion,
        VoraxCardKind.Instrument => Offsets.VoraxTypeInstrument,
        VoraxCardKind.Modifier => Offsets.VoraxTypeModifier,
        _ => Offsets.VoraxTypeBundleA,
    };

    private CardText Resolve(LuaTable manager, VoraxCardKind kind, int id)
    {
        if (kind == VoraxCardKind.Unknown) return CardText.Empty;
        if (_texts.TryGetValue((kind, id), out var cached)) return cached;
        if (!TryFindInfo(manager, ConfigTypeOf(kind), id, out var info, out var tableIndex)) return CardText.Empty;

        var keys = Offsets.VoraxTypeNameKeys.Split(',');
        var parameters = new List<int>();
        if (_lua.TryGetTable(info, Offsets.LuaVoraxInfoParams, out var list))
            while (parameters.Count < MaxDescParams && _lua.TryGetIntAt(list, parameters.Count + 1, out var value) && value is >= int.MinValue and <= int.MaxValue)
                parameters.Add((int)value);

        var text = new CardText(
            TryGetInt(info, Offsets.LuaVoraxInfoRarity, out var rarity) ? rarity : 0,
            _lua.TryGetString(info, Offsets.LuaVoraxInfoName, out var nameKey) ? Text(nameKey) : "",
            _lua.TryGetString(info, Offsets.LuaVoraxInfoDesc, out var descKey) ? VoraxText.StripRichText(VoraxText.FillParams(Text(descKey), parameters)) : "",
            tableIndex >= 0 && tableIndex < keys.Length ? Text(Offsets.VoraxTypeNamePrefix + keys[tableIndex]) : "");
        return _texts[(kind, id)] = text;
    }

    private bool TryFindInfo(LuaTable manager, int type, int id, out LuaTable info, out int tableIndex)
    {
        info = default;
        tableIndex = 0;
        if (!_lua.TryGetTable(manager, Offsets.LuaVoraxConfigType, out var config) || !_lua.TryGetIntAt(config, type, out var index) || index < 0 || index > int.MaxValue) return false;
        tableIndex = (int)index;
        return _lua.TryGetTable(manager, Offsets.LuaVoraxTables, out var tables) && _lua.TryGetTableAt(tables, index, out var table) &&
            _lua.TryGetTable(table, Offsets.LuaVoraxInfos, out var infos) && _lua.TryGetTableAt(infos, id, out info);
    }

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
