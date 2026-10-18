using System;
using System.Collections.Generic;
using System.Linq;
using EmberHUD.Testing;

namespace Tartarus.Sim;

public sealed class MarkerTests
{
    static readonly Dictionary<string, string> SkippedScenarios = new()
    {
        ["Scratch: gain without the base tier on top"] = "scratchStacksBase off is a reference-only knob, not a Rules flag",
        ["Rat nest: hatch tier is a setting (nestTier)"] = "nestTier is a reference-only knob, the hatch is always Rare",
    };

    static readonly double[] OnlyRat = [0, 0, 0, 1];

    static Move Eat(int cell) => new(MoveKind.Consume, cell);

    [Test]
    public void RatNestTimerCountsDown()
    {
        var run = Boards.At(Phase.Manage, 3, (3, Beast.Wolf, 1));
        run.Day = 1;
        run.Cells[4] = new Cell(false, Beast.Mammoth, -1, false, Markers: Marker.Nest, NestDay: 3);
        run.Apply(new Move(MoveKind.End));
        Check.That(!run.Cells[4].Open && run.Cells[4].Markers.HasFlag(Marker.Nest), "hatched a day early");
        run.Phase = Phase.Manage;
        run.Apply(new Move(MoveKind.End));
        Check.Equal(new Cell(true, Beast.Rat, Tiers.Rare, false), run.Cells[4]);
    }

    [Test]
    public void OpeningANestFirstDestroysIt()
    {
        var run = Boards.At(Phase.Patrol, 0);
        run.Cells[4] = new Cell(false, Beast.Rat, 0, false, Markers: Marker.Nest, NestDay: 3);
        run.Doors = [4];
        run.Apply(new Move(MoveKind.Door, 4));
        Check.Equal(new Cell(true, Beast.Rat, 0, false), run.Cells[4]);
    }

    [Test]
    public void PreyMarkRaisesBothForThreeMornings()
    {
        var run = Boards.At(Phase.Manage, 3, (3, Beast.Lion, 0), (4, Beast.Wolf, 0));
        run.Prey.Add(new PreyMark(3, 4, 3, 2));
        for (var day = 0; day < 4; day++)
        {
            run.Apply(new Move(MoveKind.End));
            run.Phase = Phase.Manage;
        }
        Check.Equal(3, run.Cells[3].Tier);
        Check.Equal(3, run.Cells[4].Tier);
        Check.Equal(0, run.PreyMarks.Count);
    }

    [Test]
    public void PreyAndTuskMarkersAffectConsume()
    {
        var prey = Boards.At(Phase.Manage, 3, (3, Beast.Lion, 2), (4, Beast.Wolf, 2), (7, Beast.Rat, 1));
        prey.Chance = new Chance(OnlyRat);
        prey.Prey.Add(new PreyMark(3, 4, 3, 2));
        prey.Apply(Eat(4));
        Check.Equal(0, prey.PreyMarks.Count);

        var tusk = Boards.At(Phase.Manage, 5, (5, Beast.Wolf, 1), (6, Beast.Rat, 1));
        tusk.Chance = new Chance(OnlyRat);
        tusk.Cells[5] = tusk.Cells[5] with { Markers = Marker.Tusk };
        tusk.Cells[6] = tusk.Cells[6] with { Markers = Marker.Tusk };
        tusk.Apply(Eat(6));
        Check.That(tusk.Cells[5].Markers.HasFlag(Marker.Tusk), "the eater's cell lost its Tusk");
        Check.That(tusk.Cells[6].Markers == Marker.None && !tusk.Cells[6].Open, "the eaten cell kept its Tusk");
    }

    [Test]
    public void TuskCellReplaceRaisesFourInmates()
    {
        var run = Boards.At(Phase.Manage, 5, (5, Beast.Wolf, 0), (6, Beast.Rat, 0), (7, Beast.Rat, 0), (8, Beast.Rat, 0), (9, Beast.Rat, 0), (10, Beast.Rat, 0));
        run.Chance = new Chance(null, null, [0, 1, 0, 0, 0]);
        run.Cells[5] = run.Cells[5] with { Markers = Marker.Tusk };
        run.Apply(new Move(MoveKind.Replace));
        Check.Equal(5, run.Tiers().Where(tier => tier >= 0).Sum());
        Check.That(run.Cells[5].Markers.HasFlag(Marker.Tusk), "a Replace removed the Tusk");
    }

    [Test]
    public void OracleMarkerScenariosMatch()
    {
        var all = OracleData.Load("markers").Scenarios;
        Check.That(SkippedScenarios.Keys.All(name => all.Any(scenario => scenario.Name == name)), "skip list names a scenario that is not in markers.json");
        var matched = 0;
        foreach (var scenario in all.Where(scenario => !SkippedScenarios.ContainsKey(scenario.Name)))
        {
            for (ulong seed = 1; seed <= 4; seed++)
            {
                var run = OracleData.ToRun(scenario.Before, seed, scenario.Params);
                var steps = new[] { scenario.Action }.Concat(scenario.Action.Then ?? []).Where(step => step.Kind != "listConsume").ToArray();
                foreach (var step in steps) run.Apply(OracleData.MoveOf(step));
                var difference = OracleData.Differences(run, scenario.After);
                Check.That(difference is null, $"{scenario.Name}: {difference}");
                if (scenario.Result?.Targets is { } targets)
                    Check.That(targets.SequenceEqual(run.Meals(run.Position).Order()), $"{scenario.Name}: meals [{string.Join(',', run.Meals(run.Position))}], expected [{string.Join(',', targets)}]");
            }
            matched++;
        }
        Check.Equal(all.Length - SkippedScenarios.Count, matched);
    }
}
