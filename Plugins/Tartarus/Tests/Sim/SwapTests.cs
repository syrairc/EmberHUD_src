using System;
using System.Collections.Generic;
using System.Linq;
using EmberHUD.Testing;

namespace Tartarus.Sim;

public sealed class SwapTests
{
    static readonly Dictionary<string, string> SkippedScenarios = new();

    static readonly Dictionary<string, string> SkippedFrequencies = new();

    static IEnumerable<string> Outcomes(OracleFrequency frequency, Run run)
    {
        if (frequency.Measure.StartsWith("rose:"))
        {
            var pinned = Array.IndexOf(frequency.Params!.SwapWeights!, 1.0);
            for (var cell = 0; cell < run.Cells.Length; cell++)
                if (run.Cells[cell].Open && run.Cells[cell].Tier > (cell == 5 ? pinned : frequency.Before!.Cells[cell].Tier)) yield return $"rose:{cell}";
            yield break;
        }
        yield return $"tier:{run.Cells[5].Tier}";
        yield return $"beast:{run.Cells[5].Beast.ToString().ToLowerInvariant()}";
    }

    static Run Board(Chance? chance = null, ulong seed = 1, params ToolId[] tools)
    {
        var run = new Run(new Rules(), new Rng(seed)) { Position = 5, Phase = Phase.Manage, Chance = chance ?? new Chance() };
        run.Cells[5] = new Cell(true, Beast.Wolf, 0, false);
        run.Cells[6] = new Cell(true, Beast.Rat, 1, false);
        run.Held.AddRange(tools);
        return run;
    }

    [Test]
    public void ReplaceKeepsTheSlotAndRerollsBeastAndTier()
    {
        var run = Board(new Chance([0, 1, 0, 0], null, [0, 0, 1, 0, 0]));
        run.Apply(new Move(MoveKind.Replace));
        Check.Equal(new Cell(true, Beast.Lion, 2, false), run.Cells[5]);
        Check.Equal(new Cell(true, Beast.Rat, 1, false), run.Cells[6]);
        Check.Equal(5, run.Position);
        Check.That(run.Replaced && !run.Consumed);
    }

    [Test]
    public void ReplaceDrawsFromTheReplaceWeightsNotTheDoorWeights()
    {
        var run = Board(new Chance(null, [1, 0, 0, 0, 0], [0, 0, 0, 0, 1]));
        run.Apply(new Move(MoveKind.Replace));
        Check.Equal(4, run.Cells[5].Tier);
    }

    [Test]
    public void ReplaceIsRefusedOnEmptyOrOfficeCellsAndBeforeThePatrol()
    {
        var run = Board();
        run.Position = 0;
        Check.Throws<ArgumentException>(() => run.Apply(new Move(MoveKind.Replace)));
        run.Position = 11;
        Check.Throws<ArgumentException>(() => run.Apply(new Move(MoveKind.Replace)));
        run.Position = 5;
        run.Phase = Phase.Patrol;
        Check.Throws<ArgumentException>(() => run.Apply(new Move(MoveKind.Replace)));
    }

    [Test]
    public void TheNoosePreventsNormalAndMagic()
    {
        for (ulong seed = 1; seed <= 200; seed++)
        {
            var run = Board(null, seed, ToolId.Rope);
            run.Apply(new Move(MoveKind.Replace));
            Check.That(run.Cells[5].Tier >= 2, $"tier {run.Cells[5].Tier}");
        }
        var nothingLeft = Board(new Chance(null, null, [1, 1, 0, 0, 0]), 1, ToolId.Rope);
        Check.That(!nothingLeft.Legal().Any(move => move.Kind == MoveKind.Replace), "Replace offered with nothing to roll");
    }

    [Test]
    public void DayEndsAtRulesDays()
    {
        var run = Run.Start(new Rules { Days = 3, EventChance = 0, StartTools = 0 }, 4);
        for (var day = 1; day <= 3; day++)
        {
            Check.Equal(day, run.Day);
            Check.That(!run.Over);
            run.Apply(run.Legal()[0]);
            run.Apply(new Move(MoveKind.End));
        }
        Check.That(run.Over);
        Check.Equal(3, run.Day);
        Check.Equal(0, run.Legal().Count);
    }

    [Test]
    public void ANewDayResetsTheDailyFlags()
    {
        var run = Board(new Chance(null, null, [0, 0, 1, 0, 0]));
        run.Apply(new Move(MoveKind.Replace));
        run.Apply(new Move(MoveKind.End));
        Check.That(!run.Replaced && !run.Consumed && run.Chaining < 0);
        Check.Equal(2, run.Day);
        Check.Equal(Phase.Patrol, run.Phase);
        Check.Equal(3, run.Legal().Count);
    }

    [Test]
    public void OracleSwapScenariosMatch()
    {
        var all = OracleData.Load("swap").Scenarios;
        Check.That(SkippedScenarios.Keys.All(name => all.Any(scenario => scenario.Name == name)), "skip list names a scenario that is not in swap.json");
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
    public void OracleSwapFrequenciesAreWithinTolerance()
    {
        var all = OracleData.Load("swap").Frequencies;
        Check.That(SkippedFrequencies.Keys.All(name => all.Any(frequency => frequency.Name == name)), "skip list names a frequency that is not in swap.json");
        var frequencies = all.Where(frequency => !SkippedFrequencies.ContainsKey(frequency.Name)).ToArray();
        Check.Equal(all.Length - SkippedFrequencies.Count, frequencies.Length);
        for (var index = 0; index < frequencies.Length; index++)
            OracleData.CheckFrequency(frequencies[index], index, run => Outcomes(frequencies[index], run));
    }
}
