using System.Diagnostics;
using EmberHUD.Host.Memory;

namespace EmberHUD.Host.Reading;

internal sealed class TartarusReader(IReadBackend memory, nint moduleBase, Func<long>? clock = null)
{
    private readonly Func<long> _clock = clock ?? Stopwatch.GetTimestamp;
    private readonly LuaReader _lua = new(memory, moduleBase);
    private long _nextRefresh;
    private TartarusState? _state;

    public TartarusState? Read()
    {
        var now = _clock();
        if (now < _nextRefresh) return _state;
        _nextRefresh = now + Stopwatch.Frequency * Offsets.TartarusRefreshMilliseconds / 1000;
        return _state = Build();
    }

    private TartarusState? Build()
    {
        if (!_lua.TryOpen(out var globals) ||
            !_lua.TryGetTable(globals, Offsets.LuaTartarusManager, out var manager) ||
            !_lua.TryGetBool(manager, Offsets.LuaTartarusBoardPageOpen, out var pageOpen) || !pageOpen ||
            _lua.TryGetTable(manager, Offsets.LuaTartarusResult, out _) ||
            !_lua.TryGetTable(manager, Offsets.LuaTartarusBoard, out var board) ||
            !_lua.TryGetTable(board, Offsets.LuaTartarusCells, out var cells) ||
            !TryGetInt(board, Offsets.LuaTartarusPosition, out var position) ||
            !TryGetInt(board, Offsets.LuaTartarusSize, out var size) || size < 1 || size > Offsets.MaxTartarusCells || position < 0 || position >= size ||
            !TryGetInt(manager, Offsets.LuaTartarusMaxTurn, out var maxDay) || maxDay < 1)
            return null;

        var day = 1;
        if (_lua.TryGetTable(manager, Offsets.LuaTartarusAttrs, out var attrs) &&
            _lua.TryGetIntAt(attrs, Offsets.TartarusDayAttr, out var rawDay))
            day = (int)Math.Clamp(rawDay, 1, maxDay);

        var cellList = new List<TartarusCell>();
        for (var index = 0; index < size; index++)
            if (_lua.TryGetTableAt(cells, index, out var cell) && TryReadCell(index, cell, out var read)) cellList.Add(read);

        var instruments = _lua.TryGetTable(manager, Offsets.LuaTartarusRelics, out var relics)
            ? _lua.IntKeyedTables(relics).Take(Offsets.MaxTartarusRelics)
                .Select(r => (ok: TryGetInt(r.Value, Offsets.LuaTartarusRelicId, out var id) & TryGetInt(r.Value, Offsets.LuaTartarusObtainSequence, out var sequence), id, sequence))
                .Where(r => r.ok).OrderBy(r => r.sequence).Select(r => r.id).ToArray()
            : [];
        var chromatics = _lua.TryGetTable(manager, Offsets.LuaTartarusArchive, out var archive) ? _lua.IntKeyedTables(archive).Count() : 0;

        return new TartarusState(position, size, day, maxDay, [.. cellList], ReadChoice(manager, position, size), instruments, chromatics);
    }

    private TartarusChoice? ReadChoice(LuaTable manager, int position, int size)
    {
        if (!_lua.TryGetTable(manager, Offsets.LuaTartarusChoices, out var queue) ||
            !_lua.TryGetTableAt(queue, 1, out var front) ||
            !TryGetInt(front, Offsets.LuaTartarusChoiceType, out var type) ||
            !TryGetInt(front, Offsets.LuaTartarusChoiceId, out var id))
            return null;

        var kind = type switch
        {
            var t when t == Offsets.TartarusChoiceDice => TartarusChoiceKind.Doors,
            var t when t == Offsets.TartarusChoiceOperation => TartarusChoiceKind.Operation,
            var t when t == Offsets.TartarusChoiceOffice => TartarusChoiceKind.Office,
            var t when t == Offsets.TartarusChoiceInstrument => TartarusChoiceKind.Instrument,
            var t when t == Offsets.TartarusChoiceReward => TartarusChoiceKind.Reward,
            var t when t == Offsets.TartarusChoiceEvent => TartarusChoiceKind.Event,
            _ => TartarusChoiceKind.Other,
        };

        var complete = _lua.TryGetTable(front, Offsets.LuaTartarusOptions, out var optionTable);
        var options = new List<TartarusOption>();
        if (complete)
            foreach (var (_, table) in _lua.IntKeyedTables(optionTable).OrderBy(o => o.Key).Take(Offsets.MaxTartarusOptions))
            {
                var read = TryReadOption(kind, table, position, size, out var option);
                complete &= read;
                options.Add(option);
            }

        return new TartarusChoice(id, kind, [.. options], complete);
    }

    private bool TryReadOption(TartarusChoiceKind kind, LuaTable table, int position, int size, out TartarusOption option)
    {
        option = new TartarusOption(0, TartarusOptionKind.Other, -1, 0);
        if (!TryGetInt(table, Offsets.LuaTartarusOptionId, out var id)) return false;

        option = option with { Id = id };
        var optionKind = TartarusOptionKind.Other;
        var cell = -1;
        var param = 0;
        var ok = true;
        switch (kind)
        {
            case TartarusChoiceKind.Doors:
                ok = TryGetNested(table, Offsets.LuaTartarusDice, Offsets.LuaTartarusStep, out param);
                optionKind = TartarusOptionKind.Door;
                cell = ((position + param) % size + size) % size;
                break;
            case TartarusChoiceKind.Operation:
                ok = TryReadOperation(table, out optionKind, out cell);
                break;
            case TartarusChoiceKind.Office:
                ok = TryGetNested(table, Offsets.LuaTartarusInterrogation, Offsets.LuaTartarusInterrogationType, out var office);
                optionKind = office switch { var t when t == Offsets.TartarusInstigate => TartarusOptionKind.Instigate, var t when t == Offsets.TartarusBestow => TartarusOptionKind.Bestow, _ => TartarusOptionKind.Other };
                break;
            case TartarusChoiceKind.Instrument:
                ok = TryGetNested(table, Offsets.LuaTartarusRelic, Offsets.LuaTartarusRelicId, out param);
                optionKind = TartarusOptionKind.Instrument;
                break;
            case TartarusChoiceKind.Reward:
                ok = TryGetNested(table, Offsets.LuaTartarusColoredReward, Offsets.LuaTartarusRewardId, out param);
                optionKind = TartarusOptionKind.Reward;
                break;
            case TartarusChoiceKind.Event:
                ok = TryGetNested(table, Offsets.LuaTartarusEvent, Offsets.LuaTartarusEventId, out param);
                optionKind = TartarusOptionKind.Confirm;
                break;
        }

        if (ok) option = new TartarusOption(id, optionKind, cell, param);
        return ok;
    }

    private bool TryReadOperation(LuaTable table, out TartarusOptionKind kind, out int cell)
    {
        kind = TartarusOptionKind.Other;
        cell = -1;
        if (!_lua.TryGetTable(table, Offsets.LuaTartarusOperation, out var operation) ||
            !TryGetInt(operation, Offsets.LuaTartarusOperationType, out var type))
            return false;

        switch (type)
        {
            case var t when t == Offsets.TartarusOpDevour:
                if (!TryGetNested(operation, Offsets.LuaTartarusDevour, Offsets.LuaTartarusTarget, out cell)) return false;
                kind = TartarusOptionKind.Consume;
                break;
            case var t when t == Offsets.TartarusOpReplace: kind = TartarusOptionKind.Replace; break;
            case var t when t == Offsets.TartarusOpEnd: kind = TartarusOptionKind.End; break;
            case var t when t == Offsets.TartarusOpStopChain: kind = TartarusOptionKind.StopChain; break;
        }

        return true;
    }

    private bool TryGetNested(LuaTable table, string outer, string inner, out int value)
    {
        value = 0;
        return _lua.TryGetTable(table, outer, out var child) && TryGetInt(child, inner, out value);
    }

    private bool TryReadCell(int index, LuaTable cell, out TartarusCell read)
    {
        read = default;
        if (!_lua.TryGetTable(cell, Offsets.LuaTartarusPrisoner, out var prisoner)) return false;

        var open = _lua.TryGetString(prisoner, Offsets.LuaTartarusUid, out var uid) && uid.Length > 0;
        var ratingKnown = open || (TryGetInt(cell, Offsets.LuaTartarusPreviewType, out var preview) && preview == Offsets.TartarusPreviewRating);
        var tier = ratingKnown && TryGetInt(prisoner, Offsets.LuaTartarusRating, out var rating) ? rating - Offsets.TartarusRatingBase : -1;
        var beast = TryGetInt(prisoner, Offsets.LuaTartarusSpecies, out var species) ? species - Offsets.TartarusSpeciesBase + 1 : 0;
        _lua.TryGetBool(cell, Offsets.LuaTartarusDevourTarget, out var devour);
        _lua.TryGetBool(cell, Offsets.LuaTartarusEventMarker, out var marker);

        read = new TartarusCell(index, open, tier is >= 0 and <= 5 ? tier : -1, beast is >= 1 and <= 4 ? (TartarusBeast)beast : TartarusBeast.Unknown, devour, marker);
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
