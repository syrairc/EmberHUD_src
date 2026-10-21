using System.Diagnostics;
using System.Globalization;
using EmberHUD.Host.Memory;

namespace EmberHUD.Host.Reading;

internal sealed class FrozenCanvasReader(IReadBackend memory, nint moduleBase, Func<string, string?>? loc = null, Func<long>? clock = null)
{
    private const int MaxRewards = 16;

    private readonly Func<long> _clock = clock ?? Stopwatch.GetTimestamp;
    private readonly LuaReader _lua = new(memory, moduleBase);
    private long _nextRefresh;
    private FrozenCanvasState? _state;

    public FrozenCanvasState? Read()
    {
        var now = _clock();
        if (now < _nextRefresh) return _state;
        _nextRefresh = now + Stopwatch.Frequency * Offsets.FrozenRefreshMilliseconds / 1000;
        return _state = Build();
    }

    private static readonly (string Name, int Number, string Member)[] PlotTypeRows = ParseRows(Offsets.FrozenPlotTypes);
    private static readonly (string Name, int Number, string Member)[] ScreenRows = ParseRows(Offsets.FrozenScreens);

    internal static int SlotOf(int no) => no >= 1 && no <= Offsets.MaxFrozenPlots ? Offsets.MaxFrozenPlots - no : -1;

    internal static FrozenCanvasPlotKind PlotKind(string? name, int tag) =>
        MemberOf<FrozenCanvasPlotKind>(PlotTypeRows, r => r.Name == name) is var byName && byName != FrozenCanvasPlotKind.Unknown ? byName : MemberOf<FrozenCanvasPlotKind>(PlotTypeRows, r => r.Number == tag);

    internal static FrozenCanvasPhase PhaseOf(int mainState) =>
        mainState == Offsets.FrozenStateDraw ? FrozenCanvasPhase.Draw :
        mainState == Offsets.FrozenStateBattle ? FrozenCanvasPhase.Battle :
        FrozenCanvasPhase.Idle;

    internal static FrozenCanvasScreen ScreenOf(int viewState) => MemberOf<FrozenCanvasScreen>(ScreenRows, r => r.Number == viewState);

    private static (string Name, int Number, string Member)[] ParseRows(string text) =>
        [.. text.Split(',').Select(e => e.Split(':')).Select(f => (f[0], int.Parse(f[1], CultureInfo.InvariantCulture), f[2]))];

    private static T MemberOf<T>((string Name, int Number, string Member)[] rows, Func<(string Name, int Number, string Member), bool> match) where T : struct, Enum
    {
        var row = rows.FirstOrDefault(match);
        return row.Member is not null && Enum.TryParse<T>(row.Member, out var member) ? member : default;
    }

    private FrozenCanvasState? Build()
    {
        if (!_lua.TryOpen(out var globals) || !_lua.TryGetTable(globals, Offsets.LuaFrozenManager, out var manager) || !TryGetInt(manager, Offsets.LuaFrozenMainState, out var mainState))
            return null;

        var phase = PhaseOf(mainState);
        TryGetInt(manager, Offsets.LuaFrozenDifficulty, out var difficulty);
        if (phase != FrozenCanvasPhase.Battle) return new FrozenCanvasState(phase, difficulty, [], 0, 0, 0, 0, 0, []);

        if (!_lua.TryGetTable(manager, Offsets.LuaFrozenInfo, out var info) || !_lua.TryGetTableAt(info, Offsets.FrozenStateBattle, out var battle) ||
            !_lua.TryGetTable(battle, Offsets.LuaFrozenLevelPlots, out var levelPlots))
            return null;

        var result = ReadBattle(manager, difficulty, battle, levelPlots);
        return _lua.TryGetTable(battle, Offsets.LuaFrozenLevelPlots, out var again) && again == levelPlots ? result : _state;
    }

    private FrozenCanvasState ReadBattle(LuaTable manager, int difficulty, LuaTable battle, LuaTable levelPlots)
    {
        TryGetInt(manager, Offsets.LuaFrozenPos, out var pos);
        TryGetInt(battle, Offsets.LuaFrozenNowNum, out var done);
        TryGetInt(battle, Offsets.LuaFrozenFreeNum, out var total);
        TryGetInt(battle, Offsets.LuaFrozenTargetNum, out var target);
        TryGetInt(battle, Offsets.LuaFrozenChallengesLeft, out var left);
        TryGetInt(battle, Offsets.LuaFrozenChallengesAll, out var all);
        return new FrozenCanvasState(FrozenCanvasPhase.Battle, difficulty, ReadPlots(battle, levelPlots, pos), done, total, target, left, all, ReadRewards(battle)) { Objective = ReadObjective() };
    }

    private FrozenCanvasPlot[] ReadPlots(LuaTable battle, LuaTable levelPlots, int pos)
    {
        var servants = ServantSlots(battle);
        var playerSlot = SlotOf(pos);
        var plots = Enumerable.Range(0, Offsets.MaxFrozenPlots)
            .Select(slot => new FrozenCanvasPlot(slot, slot % Offsets.FrozenGridSize, slot / Offsets.FrozenGridSize, FrozenCanvasPlotKind.Unknown, 0, 0, servants.Contains(slot), slot == playerSlot))
            .ToArray();
        var seen = new HashSet<int>();
        foreach (var (_, row) in _lua.IntKeyedTables(levelPlots).OrderBy(r => r.Key).Take(Offsets.MaxFrozenPlots))
        {
            var slot = TryGetInt(row, Offsets.LuaFrozenPlotNo, out var no) ? SlotOf(no) : -1;
            if (slot < 0 || !seen.Add(slot)) continue;
            TryGetInt(row, Offsets.LuaFrozenPlotItem, out var paint);
            var tag = _lua.TryGetTable(row, Offsets.LuaFrozenPlotTags, out var tags) && _lua.TryGetIntAt(tags, 1, out var raw) && raw is >= int.MinValue and <= int.MaxValue ? (int)raw : 0;
            plots[slot] = plots[slot] with { Kind = PlotKind(null, tag), PaintId = paint, Level = paint > 0 ? paint % 100 : 0, PaintName = paint > 0 ? Text(Offsets.FrozenPaintNameKey + paint.ToString(CultureInfo.InvariantCulture)) : "" };
        }

        return plots;
    }

    private HashSet<int> ServantSlots(LuaTable battle)
    {
        var slots = new HashSet<int>();
        if (!_lua.TryGetTable(battle, Offsets.LuaFrozenServantPlots, out var list)) return slots;
        for (var i = 1; i <= Offsets.MaxFrozenPlots && _lua.TryGetIntAt(list, i, out var no); i++)
            if (no is >= int.MinValue and <= int.MaxValue && SlotOf((int)no) is var slot and >= 0) slots.Add(slot);
        return slots;
    }

    private FrozenCanvasReward[] ReadRewards(LuaTable battle)
    {
        if (!_lua.TryGetTable(battle, Offsets.LuaFrozenRewardList, out var list)) return [];
        return [.. _lua.IntKeyedTables(list).OrderBy(r => r.Key).Take(MaxRewards)
            .Select(r => (ok: TryGetInt(r.Value, Offsets.LuaFrozenRewardItem, out var item) & TryGetInt(r.Value, Offsets.LuaFrozenRewardCount, out var count), item, count))
            .Where(r => r.ok).Select(r => new FrozenCanvasReward(r.item, r.count))];
    }

    private FrozenCanvasObjective? ReadObjective()
    {
        if (!_lua.TryOpen(out var globals) ||
            !_lua.TryGetTable(globals, Offsets.LuaFrozenHudManager, out var hud) ||
            !_lua.TryGetTable(hud, Offsets.LuaFrozenHudList, out var list) ||
            !_lua.TryGetTable(list, Offsets.LuaFrozenHudKey, out var entry) ||
            !_lua.TryGetTable(entry, Offsets.LuaFrozenHudWidget, out var widget))
            return null;

        _lua.TryGetString(widget, Offsets.LuaFrozenHudText, out var text);
        TryGetInt(widget, Offsets.LuaFrozenHudCurrent, out var current);
        _lua.TryGetNumber(widget, Offsets.LuaFrozenHudRemaining, out var seconds);
        return text.Length > 0 || seconds > 1 ? new FrozenCanvasObjective(text, current, (float)seconds) : null;
    }

    private string Text(string key) => loc?.Invoke(key) ?? "";

    private bool TryGetInt(LuaTable table, string key, out int value)
    {
        value = 0;
        if (!_lua.TryGetInt(table, key, out var raw) || raw < int.MinValue || raw > int.MaxValue) return false;
        value = (int)raw;
        return true;
    }
}
