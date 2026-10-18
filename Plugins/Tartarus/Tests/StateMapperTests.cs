using System.Linq;
using EmberHUD;
using EmberHUD.Testing;
using Tartarus.Sim;

namespace Tartarus;

public sealed class StateMapperTests
{
    static TartarusCell Open(int index, TartarusBeast beast, int tier, bool devour = false) => new(index, true, tier, beast, devour, false);

    static TartarusCell Locked(int index, int tier = -1) => new(index, false, tier, TartarusBeast.Unknown, false, false);

    static TartarusChoice Operation(bool complete = true) =>
        new(1, TartarusChoiceKind.Operation, [new TartarusOption(1, TartarusOptionKind.Consume, 2, 0), new TartarusOption(2, TartarusOptionKind.End, -1, 0)], complete);

    static TartarusCell[] Ring(params TartarusCell[] cells) =>
        [.. Enumerable.Range(0, 8).Select(index => cells.FirstOrDefault(cell => cell.Index == index, Locked(index)))];

    static TartarusState Board(TartarusChoice? choice, TartarusCell[]? cells = null, int maxDay = 20, int[]? tools = null) =>
        new(1, 8, 5, maxDay, cells ?? Ring(Open(1, TartarusBeast.Lion, 3, true), Open(2, TartarusBeast.Wolf, 3), Open(4, TartarusBeast.Rat, 1)), choice, tools ?? [], 2);

    static TrackedRun Tracked() => new() { Day = 5, Size = 8 };

    static MapResult Map(TartarusState state, TrackedRun? tracked = null) => StateMapper.Map(state, tracked ?? Tracked(), new Rules());

    [Test]
    public void ARecordedMidRunBoardMapsToARunWithTheSameCells()
    {
        var tracked = Tracked();
        tracked.SeenCells = [3];
        tracked.SwapUsedToday = true;
        var result = Map(Board(Operation(), Ring(Open(1, TartarusBeast.Lion, 3, true), Open(2, TartarusBeast.Wolf, 3), Locked(3, -1), Open(4, TartarusBeast.Rat, 1)), tools: [112, 131]), tracked);
        var run = result.Run!;
        Check.Equal("", result.Mismatch ?? "");
        Check.Equal(8, run.Cells.Length);
        Check.Equal(1, run.Position);
        Check.Equal(5, run.Day);
        Check.Equal(2, run.Chromatics);
        Check.That(run.Phase == Phase.Manage);
        Check.That(run.Cells[1] == new Cell(true, Beast.Lion, 3, false));
        Check.That(run.Cells[4] == new Cell(true, Beast.Rat, 1, false));
        Check.That(run.Cells[3].Seen && !run.Cells[3].Open && run.Cells[3].Tier == -1);
        Check.That(run.Tools.SequenceEqual([ToolId.Keys, ToolId.ShortChain]));
        Check.That(run.Legal().Any(move => move is { Kind: MoveKind.Consume, Cell: 2 }));
        Check.That(!run.Legal().Any(move => move.Kind == MoveKind.Replace));
    }

    [Test]
    public void DoorsChoiceGivesPatrolWithTheOfferedCells()
    {
        var doors = new TartarusChoice(1, TartarusChoiceKind.Doors, [new TartarusOption(1, TartarusOptionKind.Door, 3, 2), new TartarusOption(2, TartarusOptionKind.Door, 5, 4)], true);
        var run = Map(Board(doors, Ring(Open(1, TartarusBeast.Lion, 3), Locked(3, 2)))).Run!;
        Check.That(run.Phase == Phase.Patrol);
        Check.That(run.Legal().Select(move => move.Cell).SequenceEqual([3, 5]));
        Check.Equal(2, run.Cells[3].AtLeast);
    }

    [Test]
    public void MaxDayBecomesRulesDays()
    {
        var run = Map(Board(Operation(), maxDay: 12)).Run!;
        Check.Equal(12, run.Rules.Days);
        Check.Equal(12, run.MaxDay);
    }

    [Test]
    public void UnknownBeastGivesAMismatch()
    {
        var result = Map(Board(Operation(), Ring(Open(1, TartarusBeast.Lion, 3), Open(2, TartarusBeast.Unknown, 1))));
        Check.That(result.Run is null);
        Check.That(result.Mismatch!.Contains("cell 3"));
        var tierless = Map(Board(Operation(), Ring(Open(1, TartarusBeast.Lion, 3), Open(2, TartarusBeast.Rat, -1))));
        Check.That(tierless.Run is null && tierless.Mismatch is not null);
    }

    [Test]
    public void IncompleteChoiceGivesNoRun()
    {
        var result = Map(Board(Operation(false)));
        Check.That(result.Run is null && result.Mismatch is not null);
        Check.That(Map(Board(null)).Run is null);
    }

    [Test]
    public void EmptyCellsGiveNoRun()
    {
        var result = Map(Board(Operation(), []));
        Check.That(result.Run is null && result.Mismatch is not null);
    }

    [Test]
    public void MaxDayZeroGivesNoRun()
    {
        foreach (var days in new[] { 0, -3, 61, int.MaxValue })
            Check.That(Map(Board(Operation(), maxDay: days)).Run is null);
    }

    [Test]
    public void GarbageNeverThrows()
    {
        var state = Board(Operation());
        Check.That(Map(state with { Size = -1 }).Run is null);
        Check.That(Map(state with { Size = 9 }).Run is null);
        Check.That(Map(state with { Position = 99 }).Run is null);
        Check.That(Map(state with { Day = 0 }).Run is null);
        Check.That(Map(state with { Cells = null! }).Run is null);
        Check.That(Map(state with { Instruments = null! }).Run is null);
        Check.That(Map(state with { Choice = state.Choice! with { Options = null! } }).Run is null);
        Check.That(Map(state with { Cells = [.. state.Cells.Select(cell => cell with { Index = 3 })] }).Run is null);
        Check.That(Map(state with { Cells = [.. state.Cells.Select(cell => cell with { Index = -2 })] }).Run is null);
        Check.That(Map(state with { Cells = [.. state.Cells.Select(cell => cell with { Tier = 99, Open = true, Beast = TartarusBeast.Rat })] }).Run is null);
        Check.That(StateMapper.Map(null!, Tracked(), new Rules()).Run is null);
        Check.That(StateMapper.Map(state, null!, new Rules()).Run is null);
    }

    [Test]
    public void DevourTargetsMustMatchWhatTheEngineOffers()
    {
        var wrong = Map(Board(Operation(), Ring(Open(1, TartarusBeast.Lion, 3), Open(2, TartarusBeast.Wolf, 3, false), Open(4, TartarusBeast.Rat, 1, true))));
        Check.That(wrong.Run is null);
        Check.That(wrong.Mismatch!.Contains("cell 2"));
        Check.That(Map(Board(Operation())).Mismatch is null);
    }

    [Test]
    public void UntrackedPartsAreListedAsAssumed()
    {
        var tracked = Tracked();
        tracked.Assumed.Add("Joined late.");
        tracked.ToolsHeld = [143];
        var result = Map(Board(Operation(), tools: [143]), tracked);
        Check.That(result.Run is not null);
        Check.That(result.Assumed.Contains("Joined late."));
        Check.That(result.Assumed.Any(line => line.Contains("Start card and scratch")));
        Check.That(result.Assumed.Any(line => line.Contains("Tool uses left")));
        Check.That(result.Assumed.Any(line => line.Contains("Tool 143")));
        Check.Equal(0, result.Run!.Tools.Count);
    }

    static TartarusChoice Picks(TartarusChoiceKind kind, TartarusOptionKind option, params int[] ids) =>
        new(2, kind, [.. ids.Select((id, i) => new TartarusOption(i + 1, option, -1, id))], true);

    [Test]
    public void ASixteenCellBoardMapsCellForCell()
    {
        var cells = Enumerable.Range(0, 16).Select(index => index switch
        {
            1 => Open(1, TartarusBeast.Lion, 3, true),
            2 => Open(2, TartarusBeast.Wolf, 3),
            5 => Open(5, TartarusBeast.Mammoth, 1),
            9 => Open(9, TartarusBeast.Rat, 0),
            12 => Locked(12, 2),
            _ => Locked(index),
        }).ToArray();
        var state = new TartarusState(1, 16, 7, 20, cells, Operation(), [102], 1);
        var tracked = new TrackedRun { Day = 7, Size = 16, SeenCells = [4, 12] };
        var result = Map(state, tracked);
        var run = result.Run!;
        Check.Equal("", result.Mismatch ?? "");
        Check.Equal(16, run.Cells.Length);
        Check.That(run.Cells[2] == new Cell(true, Beast.Wolf, 3, false));
        Check.That(run.Cells[9] == new Cell(true, Beast.Rat, 0, false));
        Check.Equal(2, run.Cells[12].AtLeast);
        Check.That(run.Cells[4].Seen && run.Cells[12].Seen);
        Check.Equal(4, run.Cells.Count(cell => cell.Open));
        Check.That(run.Tools.SequenceEqual([ToolId.Ravenous]));
    }

    [Test]
    public void MaxDayThirtyMapsToThirtyDays()
    {
        var result = Map(Board(Operation(), maxDay: 30));
        Check.That(result.Run is not null && result.Mismatch is null);
        Check.Equal(30, result.Run!.MaxDay);
        Check.Equal(30, result.Run.Rules.Days);
    }

    [Test]
    public void MarkedCellsBecomeMarkedInTheSimulatorAndAreListed()
    {
        var marked = Ring(Open(1, TartarusBeast.Lion, 3, true), Open(2, TartarusBeast.Wolf, 3), new TartarusCell(5, false, -1, TartarusBeast.Unknown, false, true));
        var mapped = Map(Board(Operation(), marked));
        Check.That(mapped.Assumed.Any(line => line.Contains("Event markers")));
        Check.That(mapped.Run!.Cells[5].Marked);
        Check.That(!Map(Board(Operation())).Assumed.Any(line => line.Contains("Event markers")));
    }

    [Test]
    public void TheLiveObjectiveCarriesItsProgressAndPayState()
    {
        var walk = Map(Board(Operation()) with { Tasks = [new TartarusTask(5, 18, 7, false)] }).Run!.Objective;
        Check.Equal(new Objective(ObjectiveKind.Walk, 7, false), walk);
        var done = Map(Board(Operation()) with { Tasks = [new TartarusTask(4, 2, 2, true)] }).Run!.Objective;
        Check.Equal(new Objective(ObjectiveKind.Incident, 2, true), done);
        var doors = Map(Board(Operation()) with { Tasks = [new TartarusTask(3, 10, 4, false)] }).Run!.Objective;
        Check.Equal(new Objective(ObjectiveKind.Doors, 4, false), doors);
        Check.Equal(ObjectiveKind.None, Map(Board(Operation())).Run!.Objective.Kind);
    }

    [Test]
    public void AnUnknownObjectiveIsListedAndLeftOut()
    {
        var result = Map(Board(Operation()) with { Tasks = [new TartarusTask(9, 10, 0, false)] });
        Check.Equal(ObjectiveKind.None, result.Run!.Objective.Kind);
        Check.That(result.Assumed.Any(line => line.Contains("Objective 9")));
    }

    static TartarusChoice ReplaceOrEnd() => new(1, TartarusChoiceKind.Operation, [new(1, TartarusOptionKind.Replace, -1, 0), new(2, TartarusOptionKind.End, -1, 0)], true);

    [Test]
    public void TheMarkIsOnInmatesThatCanEatNotOnTheirMeals()
    {
        var cells = Ring(Open(1, TartarusBeast.Lion, 4), Open(2, TartarusBeast.Wolf, 1, true), Open(3, TartarusBeast.Rat, 1));
        var result = Map(Board(ReplaceOrEnd(), cells));
        Check.That(result.Run is not null && result.Mismatch is null, result.Mismatch);
        Check.That(result.Run!.Legal().All(move => move.Kind != MoveKind.Consume) && !result.Assumed.Any(line => line.Contains("already ate")));
    }

    [Test]
    public void AnEaterWithAMealButNoConsumeOptionMeansSomethingAlreadyAte()
    {
        var cells = Ring(Open(1, TartarusBeast.Lion, 2, true), Open(2, TartarusBeast.Wolf, 2));
        var result = Map(Board(ReplaceOrEnd(), cells));
        Check.That(result.Run is not null && result.Mismatch is null, result.Mismatch);
        Check.That(result.Run!.Legal().Any(move => move.Kind == MoveKind.Replace) && result.Run.Legal().All(move => move.Kind != MoveKind.Consume));
        Check.That(result.Assumed.Any(line => line.Contains("already ate")));
    }

    [Test]
    public void AnArmedChainReactionIsPassedToTheSimulator()
    {
        var tracked = Tracked();
        tracked.ChainArmedAt = 1;
        Check.That(Map(Board(Operation()), tracked).Run!.ChainArmed);
        Check.That(!Map(Board(Operation())).Run!.ChainArmed);
    }

    [Test]
    public void AllCellsAsDoorsMeansTheSewerRatway()
    {
        var all = new TartarusChoice(1, TartarusChoiceKind.Doors, [.. Enumerable.Range(1, 7).Select(cell => new TartarusOption(cell, TartarusOptionKind.Door, cell, cell))], true);
        Check.That(Map(Board(all)).Run!.Ratway);
        var three = new TartarusChoice(1, TartarusChoiceKind.Doors, [new TartarusOption(1, TartarusOptionKind.Door, 3, 2), new TartarusOption(2, TartarusOptionKind.Door, 5, 4)], true);
        Check.That(!Map(Board(three)).Run!.Ratway);
    }

    [Test]
    public void InstrumentChoiceOffersTheKnownToolsAndListsTheRest()
    {
        var state = Board(Picks(TartarusChoiceKind.Instrument, TartarusOptionKind.Instrument, 106, 143, 131), Ring()) with { Day = 1 };
        var result = Map(state, new TrackedRun { Day = 1, Size = 8 });
        var run = result.Run!;
        Check.That(run.Phase == Phase.Patrol);
        Check.That(run.Legal().SequenceEqual([new Move(MoveKind.Instrument, Param: (int)ToolId.Whip), new Move(MoveKind.Instrument, Param: (int)ToolId.ShortChain)]));
        Check.That(result.Assumed.Any(line => line.Contains("Tool 143")));
        Check.That(result.Mismatch is null && result.Unsupported is null);
    }

    [Test]
    public void InstrumentChoiceOfOnlyUnknownToolsIsNotSimulatedYet()
    {
        var result = Map(Board(Picks(TartarusChoiceKind.Instrument, TartarusOptionKind.Instrument, 143, 145)));
        Check.That(result.Run is null && result.Mismatch is null);
        Check.That(result.Unsupported!.Contains("known to the simulator"));
    }

    [Test]
    public void RewardChoiceOffersTheRewardsOfTheEngine()
    {
        var result = Map(Board(Picks(TartarusChoiceKind.Reward, TartarusOptionKind.Reward, 1, 2, 3, 4, 9)));
        var run = result.Run!;
        Check.That(run.Legal().SequenceEqual(new[] { 0, 1, 2, 3 }.Select(reward => new Move(MoveKind.Reward, Param: reward))));
        Check.That(result.Assumed.Any(line => line.Contains("Reward 9")));
        Check.That(result.Mismatch is null);
    }

    [Test]
    public void OtherChoicesAndUnknownEventsAreNotSimulatedYetAndNotAMismatch()
    {
        foreach (var board in new[] { Board(Picks(TartarusChoiceKind.Other, TartarusOptionKind.Confirm, 213)), Board(Picks(TartarusChoiceKind.Event, TartarusOptionKind.Confirm, 299)) })
        {
            var result = Map(board);
            Check.That(result.Run is null && result.Mismatch is null);
            Check.That(result.Unsupported!.Contains("not simulated yet"));
        }
    }

    [Test]
    public void AnEventScreenMapsToThePendingEventOnTheCurrentCell()
    {
        var state = Board(Picks(TartarusChoiceKind.Event, TartarusOptionKind.Confirm, 213));
        var result = Map(state);
        var run = result.Run!;
        Check.That(result.Mismatch is null && result.Unsupported is null);
        Check.Equal(Phase.Event, run.Phase);
        Check.Equal(EventKind.SharedFortune, run.PendingEvent);
        Check.Equal(state.Position, run.EventCell);
        Check.That(run.EventPool.Count == 14 && run.EventPool.Contains(EventKind.Archenemy) && run.EventPool.Contains(EventKind.TransferOfConvicts));
    }

    [Test]
    public void ChainStepBoardMapsToAChainInProgress()
    {
        var chain = new TartarusChoice(1, TartarusChoiceKind.Operation, [new(1, TartarusOptionKind.Consume, 2, 0), new(2, TartarusOptionKind.StopChain, -1, 0)], true);
        var result = Map(Board(chain));
        var run = result.Run!;
        Check.That(result.Mismatch is null, result.Mismatch);
        Check.Equal(1, run.Chaining);
        Check.That(run.Legal().Any(move => move is { Kind: MoveKind.Consume, Cell: 2 }) && run.Legal().Any(move => move.Kind == MoveKind.StopChain));
    }

    [Test]
    public void LockedTiersAreClampedToUltimateAndSpeciesOnlyIsListed()
    {
        var high = new TartarusCell(5, false, 5, TartarusBeast.Lion, false, false);
        var species = new TartarusCell(6, false, -1, TartarusBeast.Wolf, false, false);
        var rarity = new TartarusCell(7, false, 5, TartarusBeast.Unknown, false, false);
        var result = Map(Board(Operation(), Ring(Open(1, TartarusBeast.Lion, 3, true), Open(2, TartarusBeast.Wolf, 3), Open(4, TartarusBeast.Rat, 1), high, species, rarity)));
        var run = result.Run!;
        Check.Equal(Tiers.Ultimate, run.Cells[5].Tier);
        Check.Equal(Tiers.Ultimate, run.Cells[7].AtLeast);
        Check.That(result.Assumed.Any(line => line.Contains("only its species")));
    }

    [Test]
    public void LateInstrumentAndRewardPicksEndTheDay()
    {
        foreach (var choice in new[] { Picks(TartarusChoiceKind.Reward, TartarusOptionKind.Reward, 1, 2), Picks(TartarusChoiceKind.Instrument, TartarusOptionKind.Instrument, 106, 131) })
        {
            var run = Map(Board(choice)).Run!;
            run.Apply(run.Legal()[0]);
            Check.That(run.Legal().All(move => move.Kind == MoveKind.End), string.Join(",", run.Legal()));
        }
    }
}
