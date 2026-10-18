using System.Diagnostics;
using EmberHUD.Host;
using EmberHUD.Host.Reading;

namespace EmberHUD.Tests.Reading;

public class TartarusReaderTests
{
    private static readonly nint Module = unchecked((nint)0x1_4000_0000L);

    internal sealed class Fixture
    {
        public readonly FakeMemory Memory = new();
        public readonly FakeLua Lua;
        public readonly LuaTable Manager;
        public readonly LuaTable Board;
        public readonly LuaTable Cells;
        public readonly LuaTable Attrs;
        public readonly LuaTable Relics;
        public readonly LuaTable Archive;
        public long Now;

        public Fixture(bool withManager = true, int position = 3, int size = 16, int maxTurn = 20, long? day = 7, bool pageOpen = true, bool ended = false)
        {
            Lua = new FakeLua(Memory, Module);
            Manager = Lua.NewTable(0, 4);
            Board = Lua.NewTable(0, 2);
            Cells = Lua.NewTable(15, 1);
            Attrs = Lua.NewTable(0, 2);
            Relics = Lua.NewTable(4, 1);
            Archive = Lua.NewTable(4, 1);
            Lua.SetInt(Board, Offsets.LuaTartarusPosition, position);
            Lua.SetInt(Board, Offsets.LuaTartarusSize, size);
            Lua.SetTable(Board, Offsets.LuaTartarusCells, Cells);
            Lua.SetTable(Manager, Offsets.LuaTartarusBoard, Board);
            Lua.SetInt(Manager, Offsets.LuaTartarusMaxTurn, maxTurn);
            Lua.SetTable(Manager, Offsets.LuaTartarusAttrs, Attrs);
            Lua.SetTable(Manager, Offsets.LuaTartarusRelics, Relics);
            Lua.SetTable(Manager, Offsets.LuaTartarusArchive, Archive);
            if (ended) Lua.SetTable(Manager, Offsets.LuaTartarusResult, Lua.NewTable(0, 1));
            else Lua.SetInt(Manager, Offsets.LuaTartarusResult, -1);
            Lua.SetBool(Manager, Offsets.LuaTartarusBoardPageOpen, pageOpen);
            if (day is { } d) Lua.SetIntKey(Attrs, Offsets.TartarusDayAttr, d, Offsets.LuaTagInt);
            if (withManager) Lua.SetTable(Lua.Globals, Offsets.LuaTartarusManager, Manager);
        }

        public TartarusReader Reader() => new(Memory, Module, () => Now);

        public void Cell(int index, string? uid, int species, int rating, bool devour = false, bool marker = false, bool flags = false, int? preview = null)
        {
            var prisoner = Lua.NewTable(0, 2);
            if (uid is not null) Lua.SetString(prisoner, Offsets.LuaTartarusUid, uid);
            Lua.SetInt(prisoner, Offsets.LuaTartarusSpecies, species);
            Lua.SetInt(prisoner, Offsets.LuaTartarusRating, rating);
            var cell = Lua.NewTable(0, 3);
            Lua.SetTable(cell, Offsets.LuaTartarusPrisoner, prisoner);
            if (preview is { } p) Lua.SetInt(cell, Offsets.LuaTartarusPreviewType, p);
            if (flags)
            {
                Lua.SetBool(cell, Offsets.LuaTartarusDevourTarget, devour);
                Lua.SetBool(cell, Offsets.LuaTartarusEventMarker, marker);
            }

            Lua.SetIntKeyTable(Cells, index, cell);
        }

        public void Relic(int slot, int relicId, int sequence)
        {
            var relic = Lua.NewTable(0, 2);
            Lua.SetInt(relic, Offsets.LuaTartarusRelicId, relicId);
            Lua.SetInt(relic, Offsets.LuaTartarusObtainSequence, sequence);
            Lua.SetIntKeyTable(Relics, slot, relic);
        }
    }

    private static LuaTable Option(Fixture f, int? id, params (string Key, LuaTable Value)[] children)
    {
        var option = f.Lua.NewTable(0, 3);
        if (id is { } i) f.Lua.SetInt(option, Offsets.LuaTartarusOptionId, i);
        foreach (var (key, value) in children) f.Lua.SetTable(option, key, value);
        return option;
    }

    private static LuaTable Child(Fixture f, params (string Key, long Value)[] ints)
    {
        var table = f.Lua.NewTable(0, 2);
        foreach (var (key, value) in ints) f.Lua.SetInt(table, key, value);
        return table;
    }

    private static LuaTable Choice(Fixture f, int type, int id, params LuaTable[] options)
    {
        var choice = f.Lua.NewTable(0, 3);
        var list = f.Lua.NewTable(0, 5);
        for (var i = 0; i < options.Length; i++) f.Lua.SetIntKeyTable(list, i + 1, options[i]);
        f.Lua.SetInt(choice, Offsets.LuaTartarusChoiceType, type);
        f.Lua.SetInt(choice, Offsets.LuaTartarusChoiceId, id);
        f.Lua.SetTable(choice, Offsets.LuaTartarusOptions, list);
        return choice;
    }

    private static void Queue(Fixture f, params LuaTable[] choices)
    {
        var queue = f.Lua.NewTable(0, 2);
        for (var i = 0; i < choices.Length; i++) f.Lua.SetIntKeyTable(queue, i + 1, choices[i]);
        f.Lua.SetTable(f.Manager, Offsets.LuaTartarusChoices, queue);
    }

    private static TartarusChoice ReadChoice(Fixture f, int type, params LuaTable[] options)
    {
        Queue(f, Choice(f, type, 70, options));
        return f.Reader().Read()!.Choice!;
    }

    private static LuaTable Step(Fixture f, int id, int step) =>
        Option(f, id, (Offsets.LuaTartarusDice, Child(f, (Offsets.LuaTartarusStep, step))));

    [Fact]
    public void DoorsMapStepsToTargetCells()
    {
        var f = new Fixture(position: 3, size: 16);
        var choice = ReadChoice(f, Offsets.TartarusChoiceDice, Step(f, 11, 2), Step(f, 12, 3), Step(f, 13, 6));
        Assert.Equal(TartarusChoiceKind.Doors, choice.Kind);
        Assert.Equal(70, choice.Id);
        Assert.True(choice.Complete);
        Assert.Equal(
            [new TartarusOption(11, TartarusOptionKind.Door, 5, 2), new TartarusOption(12, TartarusOptionKind.Door, 6, 3), new TartarusOption(13, TartarusOptionKind.Door, 9, 6)],
            choice.Options);
    }

    [Fact]
    public void DoorTargetWraps()
    {
        var f = new Fixture(position: 14, size: 16);
        Assert.Equal(1, ReadChoice(f, Offsets.TartarusChoiceDice, Step(f, 11, 3)).Options.Single().Cell);
    }

    [Fact]
    public void OperationOptionsReadByType()
    {
        var f = new Fixture();
        var withTarget = Option(f, 21, (Offsets.LuaTartarusOperation, OperationWith(f, Offsets.TartarusOpDevour, 5)));
        var replace = Option(f, 22, (Offsets.LuaTartarusOperation, Child(f, (Offsets.LuaTartarusOperationType, Offsets.TartarusOpReplace))));
        var end = Option(f, 23, (Offsets.LuaTartarusOperation, Child(f, (Offsets.LuaTartarusOperationType, Offsets.TartarusOpEnd))));
        var choice = ReadChoice(f, Offsets.TartarusChoiceOperation, withTarget, replace, end);
        Assert.Equal(TartarusChoiceKind.Operation, choice.Kind);
        Assert.True(choice.Complete);
        Assert.Equal(
            [new TartarusOption(21, TartarusOptionKind.Consume, 5, 0), new TartarusOption(22, TartarusOptionKind.Replace, -1, 0), new TartarusOption(23, TartarusOptionKind.End, -1, 0)],
            choice.Options);
    }

    [Fact]
    public void StopChainReadsByType()
    {
        var f = new Fixture();
        var stop = Option(f, 24, (Offsets.LuaTartarusOperation, Child(f, (Offsets.LuaTartarusOperationType, Offsets.TartarusOpStopChain))));
        Assert.Equal(TartarusOptionKind.StopChain, ReadChoice(f, Offsets.TartarusChoiceOperation, stop).Options.Single().Kind);
    }

    private static LuaTable OperationWith(Fixture f, int type, int target)
    {
        var op = f.Lua.NewTable(0, 2);
        f.Lua.SetInt(op, Offsets.LuaTartarusOperationType, type);
        f.Lua.SetTable(op, Offsets.LuaTartarusDevour, Child(f, (Offsets.LuaTartarusTarget, target)));
        return op;
    }

    [Fact]
    public void OfficeReadsInstigateAndBestow()
    {
        var f = new Fixture();
        var choice = ReadChoice(f, Offsets.TartarusChoiceOffice,
            Option(f, 31, (Offsets.LuaTartarusInterrogation, Child(f, (Offsets.LuaTartarusInterrogationType, Offsets.TartarusInstigate)))),
            Option(f, 32, (Offsets.LuaTartarusInterrogation, Child(f, (Offsets.LuaTartarusInterrogationType, Offsets.TartarusBestow)))));
        Assert.Equal(TartarusChoiceKind.Office, choice.Kind);
        Assert.Equal(
            [new TartarusOption(31, TartarusOptionKind.Instigate, -1, 0), new TartarusOption(32, TartarusOptionKind.Bestow, -1, 0)],
            choice.Options);
    }

    [Theory]
    [InlineData(8)]
    [InlineData(2)]
    public void InstrumentReadsRelicIds(int type)
    {
        var f = new Fixture();
        var choice = ReadChoice(f, type,
            Option(f, 41, (Offsets.LuaTartarusRelic, Child(f, (Offsets.LuaTartarusRelicId, 106)))),
            Option(f, 42, (Offsets.LuaTartarusRelic, Child(f, (Offsets.LuaTartarusRelicId, 131)))),
            Option(f, 43, (Offsets.LuaTartarusRelic, Child(f, (Offsets.LuaTartarusRelicId, 123)))));
        Assert.Equal(TartarusChoiceKind.Instrument, choice.Kind);
        Assert.Equal([106, 131, 123], choice.Options.Select(o => o.Param));
        Assert.All(choice.Options, o => Assert.Equal(TartarusOptionKind.Instrument, o.Kind));
    }

    [Fact]
    public void RewardReadsRewardIds()
    {
        var f = new Fixture();
        var choice = ReadChoice(f, Offsets.TartarusChoiceReward,
            [.. Enumerable.Range(1, 4).Select(i => Option(f, 50 + i, (Offsets.LuaTartarusColoredReward, Child(f, (Offsets.LuaTartarusRewardId, i)))))]);
        Assert.Equal(TartarusChoiceKind.Reward, choice.Kind);
        Assert.Equal([1, 2, 3, 4], choice.Options.Select(o => o.Param));
        Assert.All(choice.Options, o => Assert.Equal(TartarusOptionKind.Reward, o.Kind));
    }

    [Fact]
    public void EventReadsEventId()
    {
        var f = new Fixture();
        var choice = ReadChoice(f, Offsets.TartarusChoiceEvent, Option(f, 61, (Offsets.LuaTartarusEvent, Child(f, (Offsets.LuaTartarusEventId, 213)))));
        Assert.Equal(TartarusChoiceKind.Event, choice.Kind);
        Assert.Equal(new TartarusOption(61, TartarusOptionKind.Confirm, -1, 213), choice.Options.Single());
    }

    [Fact]
    public void UnknownChoiceTypeIsOther()
    {
        var f = new Fixture();
        var choice = ReadChoice(f, 99, Option(f, 71));
        Assert.Equal(TartarusChoiceKind.Other, choice.Kind);
        Assert.Equal(TartarusOptionKind.Other, choice.Options.Single().Kind);
    }

    [Fact]
    public void LazyDevourReadsItsTargetFromTheBytes()
    {
        var f = new Fixture();
        var lazyDevour = f.Lua.NewTable(2, 0);
        f.Lua.SetIntKeyString(lazyDevour, 1, "rpc.S16WardenDevourOperationOption");
        f.Lua.SetIntKeyBytes(lazyDevour, 2, Convert.FromHexString("080612060A025034 1003".Replace(" ", "")));
        var op = f.Lua.NewTable(0, 2);
        f.Lua.SetInt(op, Offsets.LuaTartarusOperationType, Offsets.TartarusOpDevour);
        f.Lua.SetTable(op, Offsets.LuaTartarusDevour, lazyDevour);
        var choice = ReadChoice(f, Offsets.TartarusChoiceOperation, Option(f, 21, (Offsets.LuaTartarusOperation, op)));
        Assert.True(choice.Complete);
        Assert.Equal(new TartarusOption(21, TartarusOptionKind.Consume, 6, 0), choice.Options.Single());
    }

    [Fact]
    public void LazyDevourMakesTheChoiceIncomplete()
    {
        var f = new Fixture();
        var lazyDevour = f.Lua.NewTable(0, 2);
        f.Lua.SetString(lazyDevour, Offsets.LuaTartarusTarget, "blob");
        var op = f.Lua.NewTable(0, 2);
        f.Lua.SetInt(op, Offsets.LuaTartarusOperationType, Offsets.TartarusOpDevour);
        f.Lua.SetTable(op, Offsets.LuaTartarusDevour, lazyDevour);
        var choice = ReadChoice(f, Offsets.TartarusChoiceOperation, Option(f, 21, (Offsets.LuaTartarusOperation, op)));
        Assert.False(choice.Complete);
        Assert.Equal(new TartarusOption(21, TartarusOptionKind.Other, -1, 0), choice.Options.Single());
    }

    [Fact]
    public void MissingOptionIdIsIncomplete()
    {
        var f = new Fixture();
        var choice = ReadChoice(f, Offsets.TartarusChoiceEvent, Option(f, null, (Offsets.LuaTartarusEvent, Child(f, (Offsets.LuaTartarusEventId, 213)))));
        Assert.False(choice.Complete);
        Assert.Equal(TartarusOptionKind.Other, choice.Options.Single().Kind);
    }

    [Fact]
    public void InstrumentOptionWithoutARelicIsAnExtraOptionAndTheChoiceStaysComplete()
    {
        var f = new Fixture();
        var choice = ReadChoice(f, Offsets.TartarusChoiceInstrument, Option(f, 1, (Offsets.LuaTartarusRelic, Child(f, (Offsets.LuaTartarusRelicId, 111)))), Option(f, 4));
        Assert.True(choice.Complete);
        Assert.Equal(new TartarusOption(1, TartarusOptionKind.Instrument, -1, 111), choice.Options[0]);
        Assert.Equal(new TartarusOption(4, TartarusOptionKind.Other, -1, 0), choice.Options[1]);
    }

    [Fact]
    public void OnlyTheFrontChoiceIsRead()
    {
        var f = new Fixture();
        Queue(f, Choice(f, Offsets.TartarusChoiceEvent, 1, Option(f, 61, (Offsets.LuaTartarusEvent, Child(f, (Offsets.LuaTartarusEventId, 213))))), Choice(f, Offsets.TartarusChoiceReward, 2));
        var choice = f.Reader().Read()!.Choice!;
        Assert.Equal(1, choice.Id);
        Assert.Equal(TartarusChoiceKind.Event, choice.Kind);
    }

    [Fact]
    public void EmptyChoicesGiveANullChoiceButAState()
    {
        var f = new Fixture();
        Queue(f);
        var state = f.Reader().Read();
        Assert.NotNull(state);
        Assert.Null(state.Choice);
    }

    [Fact]
    public void OptionsAreCappedAtTheLimit()
    {
        var f = new Fixture();
        var many = Enumerable.Range(1, Offsets.MaxTartarusOptions + 4).Select(i => Option(f, i)).ToArray();
        Assert.Equal(Offsets.MaxTartarusOptions, ReadChoice(f, Offsets.TartarusChoiceEvent, many).Options.Length);
    }

    [Fact]
    public void ReadsPositionSizeDayAndMaxDay()
    {
        var s = new Fixture().Reader().Read()!;
        Assert.Equal(3, s.Position);
        Assert.Equal(16, s.Size);
        Assert.Equal(7, s.Day);
        Assert.Equal(20, s.MaxDay);
        Assert.Null(s.Choice);
    }

    [Fact]
    public void MaxDayFollowsMaxTurn()
    {
        var s = new Fixture(maxTurn: 30, day: 25).Reader().Read()!;
        Assert.Equal(30, s.MaxDay);
        Assert.Equal(25, s.Day);
    }

    [Fact]
    public void DayClampsToTheRange()
    {
        Assert.Equal(20, new Fixture(day: 99).Reader().Read()!.Day);
        Assert.Equal(1, new Fixture(day: null).Reader().Read()!.Day);
    }

    [Fact]
    public void OpenCellHasTierAndBeast()
    {
        var f = new Fixture();
        f.Cell(2, "P7", 101, 205);
        var cell = f.Reader().Read()!.Cells.Single();
        Assert.Equal(new TartarusCell(2, true, 4, TartarusBeast.Mammoth, false, false, 7), cell);
    }

    [Fact]
    public void OpenCellReadsItsUid()
    {
        var f = new Fixture();
        f.Cell(2, "P12", 101, 205);
        Assert.Equal(12, f.Reader().Read()!.Cells.Single().Uid);
    }

    [Fact]
    public void LockedCellHasUidZero()
    {
        var f = new Fixture();
        f.Cell(2, null, 0, 0);
        Assert.Equal(0, f.Reader().Read()!.Cells.Single().Uid);
    }

    [Theory]
    [InlineData("X7")]
    [InlineData("P")]
    [InlineData("Pabc")]
    public void OddUidTextGivesZeroButStaysOpen(string text)
    {
        var f = new Fixture();
        f.Cell(2, text, 101, 205);
        var cell = f.Reader().Read()!.Cells.Single();
        Assert.True(cell.Open);
        Assert.Equal(0, cell.Uid);
    }

    [Fact]
    public void LockedCellIsClosedAndUnknown()
    {
        var f = new Fixture();
        f.Cell(2, null, 0, 0);
        var cell = f.Reader().Read()!.Cells.Single();
        Assert.False(cell.Open);
        Assert.Equal(-1, cell.Tier);
        Assert.Equal(TartarusBeast.Unknown, cell.Beast);
    }

    [Fact]
    public void PreviewedLockedCellKnowsItsTier()
    {
        var f = new Fixture();
        f.Cell(5, null, 0, 202, preview: Offsets.TartarusPreviewRating);
        var cell = f.Reader().Read()!.Cells.Single();
        Assert.False(cell.Open);
        Assert.Equal(1, cell.Tier);
    }

    [Fact]
    public void LockedCellRatingWithoutRatingPreviewIsUnknown()
    {
        var f = new Fixture();
        f.Cell(5, null, 0, 202);
        f.Cell(6, null, 0, 202, preview: 0);
        Assert.All(f.Reader().Read()!.Cells, c => Assert.Equal(-1, c.Tier));
    }

    [Fact]
    public void OpenCellKeepsItsTierWhateverThePreviewType()
    {
        var f = new Fixture();
        f.Cell(2, "P7", 101, 205, preview: 0);
        Assert.Equal(4, f.Reader().Read()!.Cells.Single().Tier);
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(16)]
    public void PositionOutsideTheBoardGivesNoState(int position) =>
        Assert.Null(new Fixture(position: position, size: 16).Reader().Read());

    [Fact]
    public void FlagsReadAsBools()
    {
        var f = new Fixture();
        f.Cell(1, "a", 102, 201, devour: true, marker: true, flags: true);
        f.Cell(2, "b", 103, 201);
        var cells = f.Reader().Read()!.Cells;
        Assert.True(cells[0].DevourTarget);
        Assert.True(cells[0].EventMarker);
        Assert.False(cells[1].DevourTarget);
        Assert.False(cells[1].EventMarker);
    }

    [Fact]
    public void OfficeCellIsClosedAndIndexZero()
    {
        var f = new Fixture();
        f.Cell(0, null, 0, 0);
        f.Cell(1, "a", 104, 206);
        var cells = f.Reader().Read()!.Cells;
        Assert.Equal([0, 1], cells.Select(c => c.Index));
        Assert.False(cells[0].Open);
        Assert.Equal(-1, cells[0].Tier);
        Assert.Equal(5, cells[1].Tier);
        Assert.Equal(TartarusBeast.Rat, cells[1].Beast);
    }

    [Fact]
    public void InstrumentsSortByObtainSequence()
    {
        var f = new Fixture();
        f.Relic(1, 30, 3);
        f.Relic(2, 20, 2);
        f.Relic(3, 10, 1);
        Assert.Equal([10, 20, 30], f.Reader().Read()!.Instruments);
    }

    [Fact]
    public void ChromaticsCountsArchive()
    {
        var f = new Fixture();
        f.Lua.SetIntKeyTable(f.Archive, 1, f.Lua.NewTable(0, 1));
        f.Lua.SetIntKeyTable(f.Archive, 2, f.Lua.NewTable(0, 1));
        Assert.Equal(2, f.Reader().Read()!.Chromatics);
    }

    private static void Task(Fixture f, LuaTable tasks, long key, long? id, long? target, long? progress, string? state)
    {
        var task = f.Lua.NewTable(0, 4);
        if (id is { } i) f.Lua.SetInt(task, Offsets.LuaTartarusTaskId, i);
        if (target is { } t) f.Lua.SetInt(task, Offsets.LuaTartarusTaskTarget, t);
        if (progress is { } p) f.Lua.SetInt(task, Offsets.LuaTartarusTaskProgress, p);
        if (state is not null) f.Lua.SetString(task, Offsets.LuaTartarusTaskState, state);
        f.Lua.SetIntKeyTable(tasks, key, task);
    }

    [Fact]
    public void TasksReadProgressAndCompletion()
    {
        var f = new Fixture();
        var tasks = f.Lua.NewTable(4, 1);
        f.Lua.SetTable(f.Manager, Offsets.LuaTartarusTasks, tasks);
        Task(f, tasks, 11, 11, 3, null, "S16WardenTaskStateRunning");
        Task(f, tasks, 12, 12, 2, 2, "S16WardenTaskStateCompleted");
        Assert.Equal([new TartarusTask(11, 3, 0, false), new TartarusTask(12, 2, 2, true)], f.Reader().Read()!.Tasks.OrderBy(t => t.Id));
    }

    [Fact]
    public void TasksAreEmptyWithoutTheTable() => Assert.Empty(new Fixture().Reader().Read()!.Tasks);

    [Fact]
    public void BadTaskEntriesAreSkippedAndAMissingIdFallsBackToTheKey()
    {
        var f = new Fixture();
        var tasks = f.Lua.NewTable(4, 1);
        f.Lua.SetTable(f.Manager, Offsets.LuaTartarusTasks, tasks);
        Task(f, tasks, 1, 1, null, 1, "S16WardenTaskStateRunning");
        Task(f, tasks, 7, null, 4, 1, null);
        Assert.Equal([new TartarusTask(7, 4, 1, false)], f.Reader().Read()!.Tasks);
    }

    [Fact]
    public void ReturnsNullWhenBoardPageIsClosed() => Assert.Null(new Fixture(pageOpen: false).Reader().Read());

    [Fact]
    public void ReturnsNullWhenResultIsATable() => Assert.Null(new Fixture(ended: true).Reader().Read());

    [Fact]
    public void ReturnsNullWithoutTheManager() => Assert.Null(new Fixture(withManager: false).Reader().Read());

    [Theory]
    [InlineData(0, 20)]
    [InlineData(65, 20)]
    [InlineData(16, 0)]
    public void ReturnsNullOnABadSizeOrMaxTurn(int size, int maxTurn) => Assert.Null(new Fixture(size: size, maxTurn: maxTurn).Reader().Read());

    [Fact]
    public void ThrottlesRefreshes()
    {
        var f = new Fixture();
        var reader = f.Reader();
        var first = reader.Read();

        f.Cell(1, "a", 101, 201);
        f.Now = Stopwatch.Frequency * Offsets.TartarusRefreshMilliseconds / 1000 - 1;
        Assert.Same(first, reader.Read());

        f.Now += 1;
        Assert.Single(reader.Read()!.Cells);
    }
}
