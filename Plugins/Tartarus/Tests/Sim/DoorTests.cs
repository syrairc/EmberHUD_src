using System;
using System.Collections.Generic;
using System.Linq;
using EmberHUD.Testing;

namespace Tartarus.Sim;

public sealed class DoorTests
{
    static readonly string[] BeastNames = ["mammoth", "lion", "wolf", "rat"];

    static Move OnlyEnd => new(MoveKind.End);

    static int[] Doors(Run run) => [.. run.Legal().Where(move => move.Kind == MoveKind.Door).Select(move => move.Cell)];

    static readonly Dictionary<string, string> SkippedScenarios = new();

    static readonly Dictionary<string, string> SkippedFrequencies = new()
    {
        ["run objective drawn at the start of a run"] = "drawn by Run.Start, checked in ObjectiveTests.ObjectiveAndStartingPickFrequenciesMatchTheOracle",
        ["event at the start of a run (72%)"] = "drawn by Run.Start, checked in EventTests.OracleStartEventFrequencyIsWithinTolerance",
    };

    static IEnumerable<string> Outcomes(Run run, OracleFrequency frequency)
    {
        if (frequency.Measure.StartsWith("events:"))
        {
            foreach (var key in OracleData.SpawnKeys(run, frequency.Before)) yield return key;
            yield break;
        }
        if (frequency.Action.Kind == "patrol")
        {
            var cell = run.Cells[frequency.Action.Cell];
            yield return $"tier:{cell.Tier}";
            if (frequency.Measure.Contains("beast")) yield return $"beast:{BeastNames[(int)cell.Beast]}";
            yield break;
        }
        if (frequency.Action.Kind == "consume")
        {
            var sealedCell = run.Cells[frequency.Action.Cell];
            yield return sealedCell.Event is null ? "event:none" : "event:any";
            if (sealedCell.Event is { } sealedKind) yield return $"kind:{OracleData.IdOf(sealedKind)}";
            yield break;
        }
        var doors = Doors(run);
        yield return $"count:{doors.Length}";
        if (frequency.Measure.Contains("dest:"))
            foreach (var door in doors) yield return $"dest:{door}";
    }

    [Test]
    public void StartPutsTheWardenInTheOfficeWithAllOtherCellsLocked()
    {
        var run = Run.Start(new Rules(), 1);
        Check.Equal(0, run.Position);
        Check.Equal(1, run.Day);
        Check.Equal(0, run.Chromatics);
        Check.That(!run.Over);
        Check.Equal(16, run.Cells.Length);
        Check.That(run.Cells.All(cell => !cell.Open));
        Check.Equal(Phase.Patrol, run.Phase);
    }

    [Test]
    public void EachMorningOffersThreeDoorsFromTheNextSixCells()
    {
        for (ulong seed = 1; seed <= 30; seed++)
        {
            var run = Run.Start(Boards.Quiet with { StartTools = 0 }, seed);
            while (!run.Over)
            {
                while (run.Offers.Count > 0) run.Apply(run.Legal()[0]);
                var doors = Doors(run);
                Check.Equal(Math.Max(1, 3 - run.Tools.Count(tool => tool is ToolId.Forged or ToolId.Whip or ToolId.Stake)), doors.Length);
                Check.Equal(doors.Length, doors.Distinct().Count());
                Check.That(doors.All(cell => (cell - run.Position + 16) % 16 is >= 1 and <= 6), "door outside the next six cells");
                Check.That(doors.SequenceEqual(doors.OrderBy(cell => (cell - run.Position + 16) % 16)), "doors not sorted clockwise");
                run.Apply(new Move(MoveKind.Door, doors[0]));
                while (run.Offers.Count > 0) run.Apply(run.Legal()[0]);
                run.Apply(OnlyEnd);
            }
        }
    }

    [Test]
    public void ADoorIsAnOpenCellOrALockedCellOrTheOffice()
    {
        var offered = new HashSet<int>();
        for (ulong seed = 1; seed <= 60; seed++)
        {
            var run = new Run(new Rules(), new Rng(seed)) { Position = 14 };
            run.Cells[15] = new Cell(true, Beast.Wolf, 1, false);
            run.RollDoors();
            offered.UnionWith(Doors(run));
        }
        Check.That(offered.Contains(15), "open cell never offered");
        Check.That(offered.Contains(0), "office never offered");
        Check.That(offered.Contains(1), "locked cell never offered");
    }

    [Test]
    public void ApplyingADoorMovesTheWardenAndOpensALockedCell()
    {
        var run = Run.Start(Boards.Quiet with { StartTools = 0 }, 5);
        var door = Doors(run)[0];
        Check.That(!run.Cells[door].Open);
        var result = run.Apply(new Move(MoveKind.Door, door));
        Check.That(result.Ok);
        Check.Equal(door, run.Position);
        Check.That(run.Cells[door].Open);
        Check.That(run.Cells[door].Tier is >= 0 and <= 4);
        Check.Equal(Phase.Manage, run.Phase);
        Check.That(run.Legal().Contains(OnlyEnd) && run.Legal().All(move => move.Kind != MoveKind.Door), "after the walk only manage moves and End are legal");
    }

    [Test]
    public void CloneIsIndependent()
    {
        var run = Run.Start(Boards.Quiet with { StartTools = 0 }, 9);
        var copy = run.Clone();
        run.Apply(new Move(MoveKind.Door, Doors(run)[0]));
        Check.Equal(0, copy.Position);
        Check.That(copy.Cells.All(cell => !cell.Open));
        copy.Apply(new Move(MoveKind.Door, Doors(copy)[0]));
        Check.That(copy.Position != 0);
    }

    [Test]
    public void OracleDoorScenariosMatch()
    {
        var all = OracleData.Load("doors").Scenarios;
        Check.That(SkippedScenarios.Keys.All(name => all.Any(scenario => scenario.Name == name)), "skip list names a scenario that is not in doors.json");
        var matched = 0;
        foreach (var scenario in all.Where(scenario => !SkippedScenarios.ContainsKey(scenario.Name)))
        {
            for (ulong seed = 1; seed <= 4; seed++)
            {
                var run = OracleData.ToRun(scenario.Before, seed, scenario.Params);
                var move = OracleData.MoveOf(scenario.Action);
                if (scenario.Result?.Ok == false)
                    Check.Throws<ArgumentException>(() => run.Apply(move));
                else
                    run.Apply(move);
                var difference = OracleData.Differences(run, scenario.After);
                Check.That(difference is null, $"{scenario.Name}: {difference}");
            }
            matched++;
        }
        Check.Equal(all.Length - SkippedScenarios.Count, matched);
    }

    [Test]
    public void OracleDoorFrequenciesAreWithinTolerance()
    {
        var all = OracleData.Load("doors").Frequencies;
        Check.That(SkippedFrequencies.Keys.All(name => all.Any(frequency => frequency.Name == name)), "skip list names a frequency that is not in doors.json");
        var frequencies = all.Where(frequency => !SkippedFrequencies.ContainsKey(frequency.Name)).ToArray();
        Check.Equal(all.Length - SkippedFrequencies.Count, frequencies.Length);
        for (var index = 0; index < frequencies.Length; index++)
            OracleData.CheckFrequency(frequencies[index], index, run => Outcomes(run, frequencies[index]));
    }

    [Test]
    public void CloneDrawsTheSameDoorsAndTiersAsTheRun()
    {
        var run = Run.Start(Boards.Quiet with { StartTools = 0 }, 11);
        var copy = run.Clone();
        for (var day = 0; day < 8; day++)
        {
            Check.That(Doors(run).SequenceEqual(Doors(copy)), $"doors differ on day {day}");
            var door = Doors(run)[0];
            run.Apply(new Move(MoveKind.Door, door));
            copy.Apply(new Move(MoveKind.Door, door));
            Check.Equal(run.Cells[door], copy.Cells[door]);
            run.Apply(OnlyEnd);
            copy.Apply(OnlyEnd);
        }
    }

    [Test]
    public void DaysOfZeroOrOneOrHugeAreClamped()
    {
        foreach (var days in new[] { -5, 0, 1, 100000 })
        {
            var run = Run.Start(new Rules { Days = days, StartTools = 0 }, 3);
            var moves = 0;
            while (!run.Over && moves++ < 10000)
                run.Apply(run.Legal()[0]);
            Check.That(run.Over, $"days {days} never ended");
            Check.That(run.Day is >= 1 and <= 60, $"days {days} ended on day {run.Day}");
            if (days <= 1) Check.Equal(1, run.Day);
        }
    }

    [Test]
    public void IllegalMoveThrowsArgumentException()
    {
        var run = Run.Start(new Rules { StartTools = 0 }, 2);
        Check.Throws<ArgumentException>(() => run.Apply(OnlyEnd));
        var notToday = Enumerable.Range(0, 16).First(cell => !Doors(run).Contains(cell));
        Check.Throws<ArgumentException>(() => run.Apply(new Move(MoveKind.Door, notToday)));
        Check.Throws<ArgumentException>(() => run.Apply(new Move(MoveKind.Consume, 3)));
        run.Apply(new Move(MoveKind.Door, Doors(run)[0]));
        Check.Throws<ArgumentException>(() => run.Apply(new Move(MoveKind.Door, 1)));
    }
}
