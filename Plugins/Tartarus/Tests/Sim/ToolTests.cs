using System;
using System.Collections.Generic;
using System.Linq;
using EmberHUD.Testing;

namespace Tartarus.Sim;

public sealed class ToolTests
{
    const int Warden = 5;

    static readonly Dictionary<string, string> SkippedScenarios = new();

    static readonly Dictionary<string, string> SkippedFrequencies = new();

    static readonly OracleScenario[] Scenarios = OracleData.Load("tools").Scenarios;

    static string Id(ToolId tool) => tool.ToString().ToLowerInvariant();

    static string? Matches(OracleScenario scenario)
    {
        if (!scenario.Random)
        {
            for (ulong seed = 1; seed <= 4; seed++)
                if (OracleData.Replay(scenario, seed) is { } wrong) return wrong;
            return null;
        }
        string? last = null;
        for (ulong seed = 1; seed <= 400; seed++)
        {
            last = OracleData.Replay(scenario, seed);
            if (last is null) return null;
        }
        return $"no seed in 400 gave this branch: {last}";
    }

    static Run Board(ToolId[] tools, double[]? beasts, params (int Cell, Beast Beast, int Tier)[] inmates)
    {
        var run = new Run(Boards.Quiet, new Rng(1)) { Position = Warden, Phase = Phase.Manage, Chance = new Chance(beasts) };
        run.Held.AddRange(tools);
        foreach (var (cell, beast, tier) in inmates) run.Cells[cell] = new Cell(true, beast, tier, false);
        return run;
    }

    [Test]
    public void EachToolDoesWhatTheRulesSay()
    {
        var names = Enum.GetValues<ToolId>().Select(Id).ToHashSet();
        Check.Equal(31, names.Count);
        foreach (var tool in Enum.GetValues<ToolId>())
        {
            if (tool == ToolId.Hook) continue;
            var own = Scenarios.Where(scenario => scenario.Before.Tools.Contains(Id(tool))).ToArray();
            Check.That(own.Length > 0, $"{tool} has no oracle scenario");
            foreach (var scenario in own.Where(scenario => !SkippedScenarios.ContainsKey(scenario.Name)))
            {
                var difference = Matches(scenario);
                Check.That(difference is null, $"{tool}: {scenario.Name}: {difference}");
            }
        }
    }

    [Test]
    public void SortingHookNeverOpensANormalDoor()
    {
        for (ulong seed = 1; seed <= 200; seed++)
        {
            var run = new Run(Boards.Quiet with { StartTools = 0 }, new Rng(seed)) { Position = 0, Phase = Phase.Patrol, Doors = [3] };
            run.Held.Add(ToolId.Hook);
            run.Apply(new Move(MoveKind.Door, 3));
            Check.That(run.Cells[3].Tier >= 1, "a Normal inmate came out of a door");
        }
    }

    [Test]
    public void MuzzleBonusWhenFourOrFewerInmatesBeforeTheMealGoesMealIncluded()
    {
        var four = Board([ToolId.Muzzle], [0, 0, 0, 1], (Warden, Beast.Wolf, 1), (6, Beast.Rat, 1), (8, Beast.Mammoth, 0), (9, Beast.Mammoth, 0));
        four.Apply(new Move(MoveKind.Consume, 6));
        Check.Equal(4, four.Cells[Warden].Tier);
        var five = Board([ToolId.Muzzle], [0, 0, 0, 1], (Warden, Beast.Wolf, 1), (6, Beast.Rat, 1), (8, Beast.Mammoth, 0), (9, Beast.Mammoth, 0), (10, Beast.Mammoth, 0));
        five.Apply(new Move(MoveKind.Consume, 6));
        Check.Equal(2, five.Cells[Warden].Tier);
    }

    [Test]
    public void ToolsNeverEnterTwiceAndAPickGivesOne()
    {
        var run = Board([], null, (Warden, Beast.Wolf, 1));
        run.Queue.Add(new Offer(MoveKind.Instrument, [(int)ToolId.Saw, (int)ToolId.Keys]));
        Check.That(run.Legal().SequenceEqual([new Move(MoveKind.Instrument, Param: (int)ToolId.Saw), new Move(MoveKind.Instrument, Param: (int)ToolId.Keys)]));
        run.Apply(new Move(MoveKind.Instrument, Param: (int)ToolId.Saw));
        Check.That(run.Tools.SequenceEqual([ToolId.Saw]));
        Check.That(run.Legal().Any(move => move.Kind == MoveKind.End));
        run.Queue.Add(new Offer(MoveKind.Instrument, [(int)ToolId.Saw]));
        run.Apply(new Move(MoveKind.Instrument, Param: (int)ToolId.Saw));
        Check.Equal(1, run.Tools.Count);
    }

    [Test]
    public void RunStartsWithAPickOfThreeTools()
    {
        for (ulong seed = 1; seed <= 20; seed++)
        {
            var run = Run.Start(new Rules(), seed);
            Check.Equal(1, run.Offers.Count);
            Check.Equal(3, run.Offers[0].Options.Length);
            Check.Equal(3, run.Offers[0].Options.Distinct().Count());
            Check.Equal(3, run.Legal().Count);
            Check.That(run.Legal().All(move => move.Kind == MoveKind.Instrument));
            Check.Equal(0, run.Tools.Count);
            Check.That(run.Doors.Length == 0, "the doors were rolled before the pick");
            run.Apply(run.Legal()[0]);
            Check.Equal(1, run.Tools.Count);
            Check.That(run.Legal().All(move => move.Kind == MoveKind.Door), "after the pick only doors are legal");
        }
        Check.Equal(0, Run.Start(new Rules { StartTools = 0 }, 1).Offers.Count);
        Check.Equal(2, Run.Start(new Rules { StartTools = 2 }, 1).Offers.Count);
    }

    [Test]
    public void PickedDoorToolsShapeTheFirstMorning()
    {
        for (ulong seed = 1; seed <= 30; seed++)
        {
            var run = Run.Start(new Rules { StartTools = 1 }, seed);
            run.Apply(new Move(MoveKind.Instrument, Param: run.Offers[0].Options[0]));
            var fewer = run.Tools[0] is ToolId.Forged or ToolId.Whip or ToolId.Stake;
            Check.Equal(fewer ? 2 : 3, run.Doors.Length);
        }
    }

    [Test]
    public void CloneKeepsEventStateToolsAndPicksAndTheyDivergeIndependently()
    {
        var run = Boards.Event(EventKind.ChainReaction, 4, (4, Beast.Wolf, 1), (5, Beast.Rat, 1));
        run.Prey.Add(new PreyMark(4, 5, 3, 2));
        run.Pool = [EventKind.Execution];
        run.ChainArmed = true;
        run.Held.Add(ToolId.Saw);
        run.Queue.Add(new Offer(MoveKind.Reward, [0, 1, 2, 3]));
        var copy = run.Clone();
        Check.Equal(EventKind.ChainReaction, copy.PendingEvent);
        Check.Equal(4, copy.EventCell);
        Check.That(copy.ChainArmed && copy.EventPool.SequenceEqual([EventKind.Execution]) && copy.PreyMarks.Count == 1 && copy.Tools.Count == 1 && copy.Offers.Count == 1);
        copy.Pool.Add(EventKind.CrowWhispers);
        copy.Prey.Clear();
        copy.Held.Clear();
        copy.Queue.Clear();
        copy.ChainArmed = false;
        copy.PendingEvent = null;
        Check.That(run.ChainArmed && run.EventPool.Count == 1 && run.PreyMarks.Count == 1 && run.Tools.Count == 1 && run.Offers.Count == 1 && run.PendingEvent == EventKind.ChainReaction);
    }

    [Test]
    public void EventsWithoutACellDoNotThrow()
    {
        foreach (var kind in new[] { EventKind.Execution, EventKind.BehindClosedDoors, EventKind.CrowWhispers })
        {
            var run = Boards.Event(kind, 4, (4, Beast.Wolf, 1), (5, Beast.Rat, 0));
            run.EventCell = -1;
            run.Apply(new Move(MoveKind.Confirm));
            Check.Equal(0, run.Cells[5].Tier);
        }
    }

    [Test]
    public void PreyMorningSkipsAMarkWhoseLionLeft()
    {
        var run = Boards.At(Phase.Manage, 3, (3, Beast.Lion, 4), (4, Beast.Wolf, 4), (8, Beast.Lion, 2), (9, Beast.Wolf, 2));
        run.Prey.Add(new PreyMark(3, 4, 3, 2));
        run.Prey.Add(new PreyMark(8, 9, 3, 2));
        run.Day = 1;
        run.Apply(new Move(MoveKind.End));
        Check.That(!run.Cells[3].Open, "the Lion did not reach Rainbow");
        Check.Equal(4, run.Cells[4].Tier);
        Check.Equal(3, run.Cells[8].Tier);
        Check.Equal(3, run.Cells[9].Tier);
        Check.Equal(1, run.PreyMarks.Count);
    }

    [Test]
    public void ClawsScratchOneFreeLockedCellEachMorningAndShowItsRarity()
    {
        var run = Board([ToolId.Claws], null, (Warden, Beast.Wolf, 2));
        for (var cell = 1; cell < run.Cells.Length; cell++)
            if (cell is not (Warden or 9)) run.Cells[cell] = run.Cells[cell] with { Markers = cell % 2 == 0 ? Marker.Scratch : Marker.Nest, NestDay = 99 };
        run.Apply(new Move(MoveKind.End));
        Check.That(run.Cells[9].Markers.HasFlag(Marker.Scratch) && run.Cells[9].Known && run.Cells[9].Tier >= 0, "the free cell was not scratched and shown");
        Check.Equal(Marker.Nest, run.Cells[1].Markers);
        run.Phase = Phase.Manage;
        run.Apply(new Move(MoveKind.End));
        Check.Equal(Marker.Scratch, run.Cells[2].Markers);
    }

    static Run Hunter(params (int Cell, Beast Beast, int Tier)[] inmates) => Board([ToolId.Trap], null, [(Warden, Beast.Lion, 2), .. inmates]);

    static int[] Hunts(Run run) => [.. run.Legal().Where(move => move.Kind == MoveKind.Hunt).Select(move => move.Cell)];

    [Test]
    public void TrapHuntIsOfferedOnlyToALionForInmatesItCanConsume()
    {
        var run = Hunter((6, Beast.Wolf, 2), (7, Beast.Rat, 2), (8, Beast.Mammoth, 2), (9, Beast.Wolf, 3));
        Check.That(Hunts(run).SequenceEqual([6, 7]));
        run.Held.Clear();
        Check.Equal(0, Hunts(run).Length);
        var wolf = Board([ToolId.Trap], null, (Warden, Beast.Wolf, 2), (6, Beast.Rat, 2));
        Check.Equal(0, Hunts(wolf).Length);
    }

    [Test]
    public void TrapHuntMarksOnePreyIsTheDaysActionAndBothRiseEachMorning()
    {
        var run = Hunter((6, Beast.Wolf, 2));
        run.Apply(new Move(MoveKind.Hunt, 6));
        Check.That(run.PreyMarks.SequenceEqual([new PreyMark(Warden, 6, 3, 2)]));
        Check.That(run.Legal().SequenceEqual([new Move(MoveKind.End)]), "a Hunt leaves no Consume, Replace or second Hunt");
        run.Apply(new Move(MoveKind.End));
        Check.Equal(3, run.Cells[Warden].Tier);
        Check.Equal(3, run.Cells[6].Tier);
    }

    [Test]
    public void TrapHuntNeedsAFreshDayAndSkipsInmatesWearingAPreyMark()
    {
        var consumed = Hunter((6, Beast.Wolf, 2), (7, Beast.Rat, 2));
        consumed.Chance = new Chance([0, 1, 0, 0]);
        consumed.Apply(new Move(MoveKind.Consume, 6));
        Check.Equal(0, Hunts(consumed).Length);
        var replaced = Hunter((6, Beast.Wolf, 2));
        replaced.Chance = new Chance([0, 1, 0, 0]);
        replaced.Apply(new Move(MoveKind.Replace));
        Check.Equal(0, Hunts(replaced).Length);
        var worn = Hunter((6, Beast.Wolf, 2), (7, Beast.Rat, 2), (11, Beast.Lion, 2), (12, Beast.Wolf, 2));
        worn.Prey.Add(new PreyMark(11, 6, 3, 2));
        Check.That(Hunts(worn).SequenceEqual([7, 12]));
        worn.Prey.Add(new PreyMark(Warden, 7, 3, 2));
        Check.Equal(0, Hunts(worn).Length);
    }

    [Test]
    public void OracleToolScenariosMatch()
    {
        Check.That(SkippedScenarios.Keys.All(name => Scenarios.Any(scenario => scenario.Name == name)), "skip list names a scenario that is not in tools.json");
        var matched = 0;
        foreach (var scenario in Scenarios.Where(scenario => !SkippedScenarios.ContainsKey(scenario.Name)))
        {
            var difference = Matches(scenario);
            Check.That(difference is null, $"{scenario.Name}: {difference}");
            matched++;
        }
        Check.Equal(Scenarios.Length - SkippedScenarios.Count, matched);
    }

    [Test]
    public void OracleToolFrequenciesAreWithinTolerance()
    {
        var all = OracleData.Load("tools").Frequencies;
        Check.That(SkippedFrequencies.Keys.All(name => all.Any(frequency => frequency.Name == name)), "skip list names a frequency that is not in tools.json");
        var checkedCount = 0;
        for (var index = 0; index < all.Length; index++)
        {
            var frequency = all[index];
            if (SkippedFrequencies.ContainsKey(frequency.Name)) continue;
            if (frequency.Measure.StartsWith("rose:"))
                OracleData.CheckFrequency(frequency, index, run => Rose(frequency, run));
            else
            {
                var branches = Scenarios.Where(scenario => scenario.Name.StartsWith(frequency.Name[..^" - how often".Length] + " [")).ToArray();
                Check.That(branches.Length > 0, $"{frequency.Name} has no branch scenarios");
                OracleData.CheckFrequency(frequency, index, run => [branches.FirstOrDefault(scenario => OracleData.Differences(run, scenario.After) is null)?.Branch ?? "other"]);
            }
            checkedCount++;
        }
        Check.Equal(all.Length - SkippedFrequencies.Count, checkedCount);
    }

    static IEnumerable<string> Rose(OracleFrequency frequency, Run run)
    {
        var risen = Enumerable.Range(0, run.Cells.Length).Where(cell =>
        {
            var before = frequency.Before!.Cells[cell];
            var was = before.Open ? before.Tier : before.Hidden?.Tier ?? -1;
            return run.Cells[cell].Open && run.Cells[cell].Tier > was;
        }).ToArray();
        yield return $"count:{risen.Length}";
        foreach (var cell in risen) yield return $"rose:{cell}";
    }
}
