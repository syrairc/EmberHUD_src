using System.Diagnostics;
using System.Globalization;
using System.Numerics;
using System.Text;
using EmberHUD.Host.Memory;

namespace EmberHUD.Host.Reading;

internal sealed class VoraxReader(IReadBackend memory, nint moduleBase, Func<string, string?>? loc = null, Func<long>? clock = null, Func<Vector2?>? origin = null)
{
    private const int MaxPages = 32;
    private const int MaxChests = 16;
    private const int MaxDescParams = 16;
    private const int MaxProperties = 512;
    private const int MaxClassDepth = 16;

    private readonly record struct CardText(int Rarity, string Name, string Description, string TypeName)
    {
        public static readonly CardText Empty = new(0, "", "", "");
    }

    private readonly Dictionary<(VoraxCardKind, int), CardText> _texts = [];
    private readonly Func<long> _clock = clock ?? Stopwatch.GetTimestamp;
    private readonly LuaReader _lua = new(memory, moduleBase);
    private readonly SlateWidgets _slate = new(memory);
    private readonly Names _names = new(memory, moduleBase);
    private readonly Dictionary<nint, int> _selectedOffsets = [];
    private readonly Func<Vector2?> _origin = origin ?? (() => Vector2.Zero);
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
        foreach (var row in _lua.Entries(list, MaxPages, 0, Offsets.LuaVoraxPageSuffix, out _))
            if (row.Key.EndsWith(Offsets.LuaVoraxPageSuffix, StringComparison.Ordinal) && _lua.TryGetTable(list, row.Key, out page)) return true;
        page = default;
        return false;
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
        var next = NextReward(view, data);
        TryGetInt(view, Offsets.LuaVoraxRewardCount, out var rewards);

        var manager = TryOpenManager(out var found) ? found : default;
        var hand = CardRows(data, Offsets.LuaVoraxHand);
        var chests = ReadChests(manager);
        var (protocols, bonus) = ReadProtocols(manager, data);
        var origin = _origin();
        var (monsters, selectedMonsters) = ReadMonsterWidgets(view, ReadMonsters(manager, data), origin);
        VoraxCard[] offered = [.. ReadCards(manager, hand, false).Select((c, position) => WithRect(c, view, origin, position))];
        return new VoraxState(ReadPhase(view, hand), round, rounds, activity, next, chests.Count(c => c != 0), rewards, rerolls, slots, monsters,
            offered, ReadCards(manager, CardRows(data, Offsets.LuaVoraxRelics), true), chests)
        { Protocols = protocols, State = _lua.TryGetString(view, Offsets.LuaVoraxState, out var stateName) ? stateName : "", RewardBonusPercent = bonus, SelectedCard = ReadSelectedCard(view, offered), SelectedMonsters = selectedMonsters };
    }

    private int NextReward(LuaTable view, LuaTable data)
    {
        if (TryGetInt(data, Offsets.LuaVoraxRewardTier, out var tier) && _lua.TryGetTable(data, Offsets.LuaVoraxRewardList, out var list))
            return _lua.TryGetTableAt(list, (long)tier + 1, out var row) && TryGetInt(row, Offsets.LuaVoraxRewardScore, out var score) ? score : 0;
        return TryGetInt(view, Offsets.LuaVoraxNextScore, out var next) ? next : 0;
    }

    private int ReadSelectedCard(LuaTable view, VoraxCard[] offered) =>
        TryGetInt(view, Offsets.LuaVoraxCardId, out var id) && id != -1 && TryGetInt(view, Offsets.LuaVoraxCardIndex, out var index) && offered.Any(c => c.Slot == index) ? index : -1;

    // The start instrument rows are numbered from 1, potion rows from 0, the widget list is always in hand order
    private VoraxCard WithRect(VoraxCard card, LuaTable view, Vector2? origin, int position) =>
        TryItemWidget(view, Offsets.LuaVoraxCardItems, position, out var widget) && TryWidgetRect(widget, origin, out var rect) ? card with { Min = rect.Min, Max = rect.Max } : card;

    private (VoraxMonster[] Monsters, int[] Selected) ReadMonsterWidgets(LuaTable view, VoraxMonster[] monsters, Vector2? origin)
    {
        var selected = new List<int>();
        for (var i = 0; i < monsters.Length; i++)
        {
            if (!TryItemWidget(view, Offsets.LuaVoraxMonsterItems, monsters[i].Slot, out var widget)) continue;
            if (TryJarRect(view, monsters[i].Slot, origin, out var rect) || TryWidgetRect(widget, origin, out rect)) monsters[i] = monsters[i] with { Min = rect.Min, Max = rect.Max };
            if (IsSelected(widget)) selected.Add(monsters[i].Slot);
        }

        return (monsters, [.. selected]);
    }

    private bool TryItemWidget(LuaTable view, string listKey, int slot, out nint widget)
    {
        widget = 0;
        return TryItem(view, listKey, slot, out var item) && TryUserdataWidget(item, Offsets.LuaVoraxRootView, out widget);
    }

    private bool TryItem(LuaTable view, string listKey, int slot, out LuaTable item)
    {
        item = default;
        return _lua.TryGetTable(view, listKey, out var items) && _lua.TryGetTableAt(items, slot + 1, out item);
    }

    private bool TryUserdataWidget(LuaTable table, string key, out nint widget)
    {
        widget = 0;
        return _lua.TryGetUserdata(table, key, out var userdata) && memory.TryReadPtr(userdata + Offsets.VoraxRootViewWidgetOffset, out widget) && widget != 0;
    }

    private bool TryJarRect(LuaTable view, int slot, Vector2? origin, out (Vector2 Min, Vector2 Max) rect)
    {
        rect = default;
        return TryItem(view, Offsets.LuaVoraxMonsterItems, slot, out var item) && _lua.TryGetTable(item, Offsets.LuaVoraxItemWidgets, out var widgets) &&
            TryUserdataWidget(widgets, Offsets.LuaVoraxMonsterRectWidget, out var jar) && TryWidgetRect(jar, origin, out rect);
    }

    private bool TryWidgetRect(nint widget, Vector2? origin, out (Vector2 Min, Vector2 Max) rect)
    {
        rect = default;
        return origin is { } at && _slate.TryWeak(widget + Offsets.UWidgetSlate, out var slate) && _slate.TryRect(slate, at, out rect);
    }

    private bool IsSelected(nint widget) =>
        TrySelectedOffset(widget, out var offset) && memory.TryReadPtr(widget + offset, out var image) && image != 0 &&
        memory.TryRead(image + Offsets.UWidgetVisibility, out byte visibility) && visibility == Offsets.VoraxSelectedVisible;

    private bool TrySelectedOffset(nint widget, out int offset)
    {
        offset = 0;
        if (!memory.TryReadPtr(widget + Offsets.UObjectClass, out var widgetClass)) return false;
        if (_selectedOffsets.TryGetValue(widgetClass, out offset)) return offset > 0;
        var cls = widgetClass;
        for (var depth = 0; cls != 0 && depth < MaxClassDepth; depth++)
        {
            memory.TryReadPtr(cls + Offsets.UStructChildProperties, out var field);
            for (var guard = 0; field != 0 && guard < MaxProperties; guard++)
            {
                if (IsNamed(field, Offsets.VoraxMonsterSelectedWidget) && memory.TryRead(field + Offsets.FPropertyOffset, out offset) && offset > 0)
                {
                    _selectedOffsets[widgetClass] = offset;
                    return true;
                }

                if (!memory.TryReadPtr(field + Offsets.FFieldNext, out field)) break;
            }

            if (!memory.TryReadPtr(cls + Offsets.UStructSuper, out cls)) break;
        }

        _selectedOffsets[widgetClass] = offset = 0;
        return false;
    }

    private bool IsNamed(nint field, string wanted) =>
        memory.TryRead(field + Offsets.FFieldName, out uint index) && memory.TryRead(field + Offsets.FFieldName + Offsets.FNameNumber, out uint number) &&
        number == 0 && _names.TryRead(index, out var name) && name == wanted;

    private (VoraxCard[] Cards, int Bonus) ReadProtocols(LuaTable manager, LuaTable data)
    {
        if (!_lua.TryGetTable(data, Offsets.LuaVoraxProtocols, out var list)) return ([], 0);
        var cards = new List<VoraxCard>();
        long bonus = 0;
        for (var i = 1; i <= Offsets.MaxVoraxCards && _lua.TryGetIntAt(list, i, out var raw); i++)
        {
            if (raw < int.MinValue || raw > int.MaxValue) continue;
            var id = (int)raw;
            var text = Resolve(manager, VoraxCardKind.Modifier, id);
            cards.Add(new VoraxCard(cards.Count, id, VoraxCardKind.Modifier, text.Rarity) { Name = text.Name, Description = text.Description, TypeName = text.TypeName });
            if (TryFindInfo(manager, ConfigTypeOf(VoraxCardKind.Modifier), id, out var info, out _) && TryGetInt(info, Offsets.LuaVoraxInfoScoreIncrease, out var inc)) bonus += inc;
        }

        return ([.. cards], (int)Math.Clamp(bonus, int.MinValue, int.MaxValue));
    }

    private bool TryOpenManager(out LuaTable manager)
    {
        manager = default;
        return _lua.TryOpen(out var globals) && _lua.TryGetTable(globals, Offsets.LuaVoraxManager, out manager);
    }

    private VoraxPhase ReadPhase(LuaTable view, LuaTable[] hand) =>
        hand.Any(IsInitRelic) ? VoraxPhase.Instrument :
        !_lua.TryGetString(view, Offsets.LuaVoraxState, out var state) ? VoraxPhase.Other :
        state == Offsets.VoraxStateRound ? VoraxPhase.Round :
        state == Offsets.VoraxStateRewardRelic ? VoraxPhase.Instrument : VoraxPhase.Other;

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
        [.. rows.Select((row, position) => (ok: TryReadCard(manager, row, position, owned, out var card), card)).Where(r => r.ok).Select(r => r.card).OrderBy(c => c.Slot)];

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
        var found = TryFindInfo(manager, ConfigTypeOf(kind), id, out var info, out var tableIndex);
        var keys = Offsets.VoraxTypeNameKeys.Split(',');
        if (!found)
        {
            if (kind != VoraxCardKind.Monster || tableIndex < 0) return CardText.Empty;
            var monster = new CardText(0, MonsterName(id), "", TypeName(keys, tableIndex));
            if (monster.Name.Length > 0) _texts[(kind, id)] = monster;
            return monster;
        }

        var parameters = new List<int>();
        if (_lua.TryGetTable(info, Offsets.LuaVoraxInfoParams, out var list))
            while (parameters.Count < MaxDescParams && _lua.TryGetIntAt(list, parameters.Count + 1, out var value) && value is >= int.MinValue and <= int.MaxValue)
                parameters.Add((int)value);

        var text = new CardText(
            TryGetInt(info, Offsets.LuaVoraxInfoRarity, out var rarity) ? rarity : 0,
            _lua.TryGetString(info, Offsets.LuaVoraxInfoName, out var nameKey) ? Text(nameKey) : "",
            _lua.TryGetString(info, Offsets.LuaVoraxInfoDesc, out var descKey) ? VoraxText.StripRichText(VoraxText.FillParams(Text(descKey), parameters)) : "",
            TypeName(keys, tableIndex));
        if (text.Name.Length > 0 && text.Description.Length > 0) _texts[(kind, id)] = text;
        return text;
    }

    private string TypeName(string[] keys, int tableIndex) => tableIndex >= 0 && tableIndex < keys.Length ? Text(Offsets.VoraxTypeNamePrefix + keys[tableIndex]) : "";

    private string MonsterName(int id) => Text(Offsets.VoraxMonsterNameKey + id.ToString(CultureInfo.InvariantCulture));

    private bool TryFindInfo(LuaTable manager, int type, int id, out LuaTable info, out int tableIndex)
    {
        info = default;
        tableIndex = -1;
        if (!_lua.TryGetTable(manager, Offsets.LuaVoraxConfigType, out var config) || !_lua.TryGetIntAt(config, type, out var index) || index < 0 || index > int.MaxValue) return false;
        tableIndex = (int)index;
        return _lua.TryGetTable(manager, Offsets.LuaVoraxTables, out var tables) && _lua.TryGetTableAt(tables, index, out var table) &&
            _lua.TryGetTable(table, Offsets.LuaVoraxInfos, out var infos) && _lua.TryGetTableAt(infos, id, out info);
    }

    private VoraxMonster[] ReadMonsters(LuaTable manager, LuaTable data)
    {
        if (!_lua.TryGetTable(data, Offsets.LuaVoraxMonsters, out var bag)) return [];
        var rows = _lua.IntKeyedTables(bag).OrderBy(r => r.Key).Take(Offsets.MaxVoraxMonsters).ToArray();
        return [.. rows.Select((r, position) => (ok: TryReadMonster(manager, r.Value, position, out var monster), monster)).Where(r => r.ok).Select(r => r.monster).OrderBy(m => m.Slot)];
    }

    private bool TryReadMonster(LuaTable manager, LuaTable row, int position, out VoraxMonster monster)
    {
        monster = default;
        if (!TryGetInt(row, Offsets.LuaVoraxId, out var id) || !TryGetInt(row, Offsets.LuaVoraxSpecies, out var species) ||
            !TryGetInt(row, Offsets.LuaVoraxCount, out var count) || !TryGetInt(row, Offsets.LuaVoraxActivity, out var activity) ||
            !TryGetInt(row, Offsets.LuaVoraxRarity, out var rarity) || count < 0 || activity < 0 || (long)count * activity > int.MaxValue)
            return false;

        var slot = TryGetInt(row, Offsets.LuaVoraxIndex, out var index) ? index : position;
        monster = new VoraxMonster(slot, id, species, rarity, count, activity)
        {
            Name = MonsterName(id),
            RarityName = RarityName(manager, rarity),
        };
        return true;
    }

    private string RarityName(LuaTable manager, int rarity) =>
        _lua.TryGetTable(manager, Offsets.LuaVoraxRarityNames, out var names) && _lua.TryGetBytesAt(names, rarity, out var key) ? Text(Offsets.VoraxTypeNamePrefix + Encoding.UTF8.GetString(key)) : "";

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
