using System;
using System.Collections.Generic;
using System.Linq;
using EmberHUD.Testing;

namespace Tartarus.Sim;

public sealed class ConsumeTests
{
    const int Warden = 5;

    static readonly double[] OnlyRat = [0, 0, 0, 1];

    static readonly double[] OnlyLion = [0, 1, 0, 0];

    static readonly Dictionary<string, string> SkippedScenarios = new()
    {
        ["chain with the stop option off goes on by itself"] = "chainStop off is not a Rules flag, the chain always waits for a pick",
        ["chain with the stop option off waits only when there is a choice"] = "chainStop off is not a Rules flag, the chain always waits for a pick",
    };

    static Run Board(Rules? rules = null, double[]? beasts = null, params (int Cell, Beast Beast, int Tier)[] inmates)
    {
        var run = new Run(rules ?? new Rules(), new Rng(1)) { Position = Warden, Phase = Phase.Manage, Chance = new Chance(beasts) };
        foreach (var (cell, beast, tier) in inmates) run.Cells[cell] = new Cell(true, beast, tier, false);
        return run;
    }

    static int[] Meals(Run run) => [.. run.Legal().Where(move => move.Kind == MoveKind.Consume).Select(move => move.Cell).Order()];

    static void Same(int[] expected, int[] actual) => Check.That(expected.SequenceEqual(actual), $"expected [{string.Join(',', expected)}], got [{string.Join(',', actual)}]");

    static Move Eat(int cell) => new(MoveKind.Consume, cell);

    [Test]
    public void ConsumeRaisesTheEaterOneTierAndLocksTheMeal()
    {
        var run = Board(null, OnlyRat, (Warden, Beast.Mammoth, 2), (6, Beast.Lion, 2), (9, Beast.Rat, 0));
        run.Apply(Eat(6));
        Check.Equal(new Cell(true, Beast.Rat, 3, false), run.Cells[Warden]);
        Check.That(!run.Cells[6].Open && run.Cells[6].Tier < 0, "the meal's cell is not locked and empty");
        Check.Equal(new Cell(true, Beast.Rat, 0, false), run.Cells[9]);
        Check.That(run.Consumed);
    }

    [Test]
    public void EaterKeepsItsBeastWhenTheRuleSaysSo()
    {
        var run = Board(new Rules { EaterKeepsBeast = true }, OnlyRat, (Warden, Beast.Wolf, 1), (6, Beast.Rat, 1));
        run.Apply(Eat(6));
        Check.Equal(new Cell(true, Beast.Wolf, 2, false), run.Cells[Warden]);
    }

    [Test]
    public void FoodChainIsMammothLionWolfRatMammoth()
    {
        foreach (var eater in Enum.GetValues<Beast>())
        {
            var run = Board(null, null, (Warden, eater, 1));
            foreach (var meal in Enum.GetValues<Beast>()) run.Cells[6 + (int)meal] = new Cell(true, meal, 1, false);
            Same([6 + ((int)eater + 1) % 4], Meals(run));
        }
    }

    [Test]
    public void SecondMealOnlyFromTheConfiguredTier()
    {
        Run Mammoth(Rules rules, int tier) => Board(rules, null, (Warden, Beast.Mammoth, tier), (6, Beast.Wolf, tier));
        Same([], Meals(Mammoth(new Rules(), 2)));
        Same([6], Meals(Mammoth(new Rules(), 3)));
        Same([6], Meals(Mammoth(new Rules { SecondMealFromTier = 0 }, 0)));
        Same([], Meals(Mammoth(new Rules { SecondMealFromTier = 4 }, 3)));
        Same([6], Meals(Board(null, null, (Warden, Beast.Lion, 3), (6, Beast.Rat, 3))));
        Same([], Meals(Board(null, null, (Warden, Beast.Wolf, 3), (6, Beast.Lion, 3))));
    }

    [Test]
    public void OnlyEqualTierOpenInmatesAreMeals()
    {
        var run = Board(null, null, (Warden, Beast.Wolf, 2), (6, Beast.Rat, 1), (7, Beast.Rat, 3), (8, Beast.Rat, 2));
        run.Cells[9] = new Cell(false, Beast.Rat, 2, true);
        Same([8], Meals(run));
    }

    [Test]
    public void ChainOffersTheNextMealAfterEachBite()
    {
        var run = Board(null, OnlyLion, (Warden, Beast.Wolf, 1), (6, Beast.Rat, 1), (7, Beast.Wolf, 2), (8, Beast.Wolf, 3));
        run.Apply(Eat(6));
        Check.Equal(Warden, run.Chaining);
        Same([7], Meals(run));
        Check.That(run.Legal().Contains(new Move(MoveKind.StopChain)));
        Check.That(!run.Legal().Contains(new Move(MoveKind.End)), "End is legal while a chain waits");
        run.Apply(Eat(7));
        run.Apply(Eat(8));
        Check.Equal(new Cell(true, Beast.Lion, 4, false), run.Cells[Warden]);
        Check.Equal(-1, run.Chaining);
        Check.That(run.Legal().SequenceEqual([new Move(MoveKind.End)]));
    }

    [Test]
    public void StopChainEndsTheChain()
    {
        var run = Board(null, OnlyLion, (Warden, Beast.Wolf, 1), (6, Beast.Rat, 1), (7, Beast.Wolf, 2));
        run.Apply(Eat(6));
        run.Apply(new Move(MoveKind.StopChain));
        Check.Equal(-1, run.Chaining);
        Check.Equal(new Cell(true, Beast.Lion, 2, false), run.Cells[Warden]);
        Check.That(run.Legal().SequenceEqual([new Move(MoveKind.End)]));
        Check.Throws<ArgumentException>(() => run.Apply(Eat(7)));
    }

    [Test]
    public void OneConsumeOrOneReplacePerDay()
    {
        var run = Board(null, OnlyLion, (Warden, Beast.Wolf, 1), (6, Beast.Rat, 1), (7, Beast.Wolf, 2));
        run.Apply(Eat(6));
        run.Apply(new Move(MoveKind.StopChain));
        Check.That(run.Meals(Warden).Contains(7), "cell 7 is not a legal-looking meal for the new Lion");
        Check.Throws<ArgumentException>(() => run.Apply(Eat(7)));
        Check.Throws<ArgumentException>(() => run.Apply(new Move(MoveKind.Replace)));
        var other = Board(null, null, (Warden, Beast.Wolf, 1), (6, Beast.Rat, 1));
        other.Apply(new Move(MoveKind.Replace));
        Check.Throws<ArgumentException>(() => other.Apply(new Move(MoveKind.Replace)));
        Check.That(!other.Legal().Any(move => move.Kind == MoveKind.Consume), "consume offered after a Replace");
    }

    [Test]
    public void NothingToManageBeforeThePatrolOrInTheOfficeOrOnALockedCell()
    {
        var run = Board(null, null, (Warden, Beast.Wolf, 1), (6, Beast.Rat, 1));
        run.Phase = Phase.Patrol;
        Check.Throws<ArgumentException>(() => run.Apply(Eat(6)));
        run.Phase = Phase.Manage;
        run.Position = 0;
        Check.That(run.Legal().SequenceEqual([new Move(MoveKind.End)]));
        run.Position = 11;
        Check.That(run.Legal().SequenceEqual([new Move(MoveKind.End)]));
    }

    [Test]
    public void ChromaticInmatesAreVaultedAndCounted()
    {
        var run = Board(null, null, (Warden, Beast.Wolf, 4), (6, Beast.Rat, 4));
        run.Apply(Eat(6));
        Check.Equal(1, run.Chromatics);
        Check.That(!run.Cells[Warden].Open && !run.Cells[6].Open, "the Chromatic stayed on the board");
        Check.Equal(-1, run.Chaining);
    }

    [Test]
    public void AtTheChromaticCapTheEaterStopsAtUltimate()
    {
        var run = Board(new Rules { ChromaticCap = 0 }, OnlyRat, (Warden, Beast.Wolf, 4), (6, Beast.Rat, 4));
        run.Apply(Eat(6));
        Check.Equal(0, run.Chromatics);
        Check.Equal(new Cell(true, Beast.Rat, 4, false), run.Cells[Warden]);
    }

    [Test]
    public void OracleConsumeScenariosMatch()
    {
        var all = OracleData.Load("consume").Scenarios;
        Check.That(SkippedScenarios.Keys.All(name => all.Any(scenario => scenario.Name == name)), "skip list names a scenario that is not in consume.json");
        var matched = 0;
        foreach (var scenario in all.Where(scenario => !SkippedScenarios.ContainsKey(scenario.Name)))
        {
            for (ulong seed = 1; seed <= 4; seed++)
            {
                var run = OracleData.ToRun(scenario.Before, seed, scenario.Params);
                var steps = new[] { scenario.Action }.Concat(scenario.Action.Then ?? []).Where(step => step.Kind != "listConsume").ToArray();
                for (var i = 0; i < steps.Length; i++)
                {
                    var move = OracleData.MoveOf(steps[i]);
                    if (i == steps.Length - 1 && scenario.Result?.Ok == false)
                        Check.Throws<ArgumentException>(() => run.Apply(move));
                    else
                        run.Apply(move);
                }
                var difference = OracleData.Differences(run, scenario.After);
                Check.That(difference is null, $"{scenario.Name}: {difference}");
                if (scenario.Result?.Targets is { } targets) Same(targets, Meals(run));
                if (scenario.Result?.ChainTargets is { } chainTargets) Same(chainTargets, Meals(run));
            }
            matched++;
        }
        Check.Equal(all.Length - SkippedScenarios.Count, matched);
    }

    [Test]
    public void OracleConsumeFrequenciesAreWithinTolerance()
    {
        var frequencies = OracleData.Load("consume").Frequencies;
        Check.Equal(1, frequencies.Length);
        for (var index = 0; index < frequencies.Length; index++)
            OracleData.CheckFrequency(frequencies[index], index, run => [$"beast:{run.Cells[Warden].Beast.ToString().ToLowerInvariant()}"]);
    }
}
