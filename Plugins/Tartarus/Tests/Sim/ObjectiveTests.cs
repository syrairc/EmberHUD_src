using System;
using System.Collections.Generic;
using System.Linq;
using EmberHUD.Testing;

namespace Tartarus.Sim;

public sealed class ObjectiveTests
{
    static readonly Dictionary<string, string> SkippedScenarios = new();

    static readonly OracleScenario[] Scenarios = OracleData.Load("objectives").Scenarios;

    static Run Walking(Objective objective, int position = 0, params int[] doors)
    {
        var run = Boards.At(Phase.Patrol, position);
        run.Objective = objective;
        run.Doors = doors;
        run.Hide(doors[0], Beast.Wolf, 1);
        return run;
    }

    [Test]
    public void KingOfTheCagedPaysATool()
    {
        var run = Boards.At(Phase.Manage, 5, (5, Beast.Wolf, 3), (6, Beast.Rat, 3));
        run.Objective = new Objective(ObjectiveKind.King);
        run.Apply(new Move(MoveKind.Consume, 6));
        Check.That(run.Objective.Paid);
        Check.Equal(1, run.Offers.Count);
        Check.Equal(3, run.Offers[0].Options.Length);
        Check.That(run.Legal().All(move => move.Kind == MoveKind.Instrument), "the pick did not come first");
        run.Apply(run.Legal()[0]);
        Check.Equal(1, run.Tools.Count);
        Check.That(run.Legal().Any(move => move.Kind == MoveKind.End));
    }

    [Test]
    public void KingOfTheCagedPaysOnlyOnceAndNotForOtherObjectives()
    {
        var other = Boards.At(Phase.Manage, 5, (5, Beast.Wolf, 3), (6, Beast.Rat, 3));
        other.Objective = new Objective(ObjectiveKind.Walk);
        other.Apply(new Move(MoveKind.Consume, 6));
        Check.Equal(0, other.Offers.Count);
        var paid = Boards.At(Phase.Manage, 5, (5, Beast.Wolf, 3), (6, Beast.Rat, 3));
        paid.Objective = new Objective(ObjectiveKind.King, 0, true);
        paid.Apply(new Move(MoveKind.Consume, 6));
        Check.Equal(0, paid.Offers.Count);
    }

    [Test]
    public void PrisonWalkCountsTilesMoved()
    {
        var run = Walking(new Objective(ObjectiveKind.Walk), 14, 3);
        run.Apply(new Move(MoveKind.Door, 3));
        Check.Equal(5, run.Objective.Progress);
        var ratway = Walking(new Objective(ObjectiveKind.Walk, 10), 4, 12);
        ratway.Ratway = true;
        ratway.Apply(new Move(MoveKind.Door, 12));
        Check.Equal(10, ratway.Objective.Progress);
        var finish = Walking(new Objective(ObjectiveKind.Walk, 16), 0, 5);
        finish.Apply(new Move(MoveKind.Door, 5));
        Check.Equal(18, finish.Objective.Progress);
        Check.That(finish.Objective.Paid && finish.Offers.Count == 1);
    }

    [Test]
    public void SurvivalCountsEveryBiteAndUndercurrentsEveryEvent()
    {
        var run = Boards.At(Phase.Manage, 5, (5, Beast.Wolf, 1), (6, Beast.Rat, 1), (7, Beast.Wolf, 2));
        run.Objective = new Objective(ObjectiveKind.Consume);
        run.Chance = new Chance([0, 1, 0, 0]);
        run.Apply(new Move(MoveKind.Consume, 6));
        run.Apply(new Move(MoveKind.Consume, 7));
        Check.Equal(2, run.Objective.Progress);
        var event2 = Boards.At(Phase.Patrol, 0);
        event2.Objective = new Objective(ObjectiveKind.Incident, 1);
        event2.Doors = [3];
        event2.Cells[3] = event2.Cells[3] with { Event = EventKind.ChainReaction };
        event2.Apply(new Move(MoveKind.Door, 3));
        Check.Equal(2, event2.Objective.Progress);
        Check.Equal(1, event2.Offers.Count);
    }

    [Test]
    public void ChromaticOpensARewardPick()
    {
        var run = Boards.At(Phase.Manage, 5, (5, Beast.Wolf, 4), (6, Beast.Rat, 4));
        run.Apply(new Move(MoveKind.Consume, 6));
        Check.Equal(1, run.Chromatics);
        Check.Equal(1, run.Offers.Count);
        var legal = run.Legal();
        Check.Equal(4, legal.Count);
        Check.That(legal.All(move => move.Kind == MoveKind.Reward));
        Check.Throws<ArgumentException>(() => run.Apply(new Move(MoveKind.End)));
        Check.Throws<ArgumentException>(() => run.Apply(new Move(MoveKind.Reward, Param: 9)));
        run.Apply(new Move(MoveKind.Reward, Param: (int)RewardId.Ring));
        Check.Equal(0, run.Offers.Count);
        Check.Equal(3, run.Where(cell => cell.Open).Length);
    }

    [Test]
    public void BehindCellDoorsCountsEveryCellThatOpensAndPaysAtTen()
    {
        var run = Walking(new Objective(ObjectiveKind.Doors, 9), 0, 3);
        run.Apply(new Move(MoveKind.Door, 3));
        Check.That(run.Objective.Paid && run.Objective.Progress == 10 && run.Offers.Count == 1);
        var ring = Boards.At(Phase.Manage, 5, (5, Beast.Wolf, 4), (6, Beast.Rat, 4));
        ring.Objective = new Objective(ObjectiveKind.Doors);
        ring.Apply(new Move(MoveKind.Consume, 6));
        ring.Apply(new Move(MoveKind.Reward, Param: (int)RewardId.Ring));
        Check.Equal(3, ring.Objective.Progress);
        var nest = Boards.At(Phase.Manage, 5, (5, Beast.Wolf, 1));
        nest.Objective = new Objective(ObjectiveKind.Doors);
        nest.Cells[4] = nest.Cells[4] with { Markers = Marker.Nest, NestDay = 2 };
        nest.Apply(new Move(MoveKind.End));
        Check.Equal(1, nest.Objective.Progress);
        var other = Walking(new Objective(ObjectiveKind.Consume), 0, 3);
        other.Apply(new Move(MoveKind.Door, 3));
        Check.Equal(0, other.Objective.Progress);
    }

    [Test]
    public void OpulenceQueuesThreePicksPerRainbowAndAPickThatMakesOneBringsThreeMore()
    {
        var plain = Boards.At(Phase.Manage, 5, (5, Beast.Wolf, 4), (6, Beast.Rat, 4));
        plain.Apply(new Move(MoveKind.Consume, 6));
        Check.Equal(1, plain.Offers.Count);
        var run = new Run(Boards.Quiet with { Card = StartCard.Opulence }, new Rng(1)) { Phase = Phase.Manage, Position = 5 };
        run.Cells[5] = new Cell(true, Beast.Wolf, 4, false);
        run.Cells[6] = new Cell(true, Beast.Rat, 4, false);
        run.Apply(new Move(MoveKind.Consume, 6));
        Check.That(run.Offers.Count == 3 && run.Offers.All(offer => offer.Kind == MoveKind.Reward && offer.Options.Length == 4));
        var horn = new Run(Boards.Quiet with { Card = StartCard.Opulence }, new Rng(1)) { Phase = Phase.Manage, Position = 5 };
        horn.Cells[5] = new Cell(true, Beast.Wolf, 4, false);
        horn.Cells[6] = new Cell(true, Beast.Rat, 4, false);
        horn.Queue.Add(new Offer(MoveKind.Reward, [0, 1, 2, 3]));
        horn.Apply(new Move(MoveKind.Reward, Param: (int)RewardId.Horn));
        Check.Equal(1, horn.Chromatics);
        Check.Equal(3, horn.Offers.Count);
    }

    [Test]
    public void HornFeedsOnlyInmatesThatHadAMealAtTheStartNeverANewcomer()
    {
        var late = new Run(Boards.Quiet, new Rng(1)) { Phase = Phase.Manage, Position = 1, Chance = new Chance(BeastWeights: [0, 1, 0, 0]) };
        late.Cells[1] = new Cell(true, Beast.Wolf, 1, false);
        late.Cells[2] = new Cell(true, Beast.Rat, 1, false);
        late.Cells[4] = new Cell(true, Beast.Mammoth, 2, false);
        late.Queue.Add(new Offer(MoveKind.Reward, [0, 1, 2, 3]));
        late.Apply(new Move(MoveKind.Reward, Param: (int)RewardId.Horn));
        Check.Equal(new Cell(true, Beast.Lion, 2, false), late.Cells[1]);
        Check.Equal(new Cell(true, Beast.Mammoth, 2, false), late.Cells[4]);
        var reopened = new Run(Boards.Quiet, new Rng(1)) { Phase = Phase.Manage, Position = 1, Chance = new Chance([1, 0, 0, 0], [0, 1, 0, 0, 0], null, _ => 0) };
        reopened.Held.Add(ToolId.Cage);
        reopened.Cells[1] = new Cell(true, Beast.Wolf, 1, false);
        reopened.Cells[2] = new Cell(true, Beast.Rat, 1, false);
        reopened.Cells[3] = new Cell(true, Beast.Lion, 1, false);
        reopened.Queue.Add(new Offer(MoveKind.Reward, [0, 1, 2, 3]));
        reopened.Apply(new Move(MoveKind.Reward, Param: (int)RewardId.Horn));
        Check.Equal(new Cell(true, Beast.Mammoth, 1, false), reopened.Cells[2]);
        Check.Equal(new Cell(true, Beast.Lion, 1, false), reopened.Cells[3]);
    }

    [Test]
    public void ToolPickComesBeforeTheRewardPick()
    {
        var run = Boards.At(Phase.Office, 0, (5, Beast.Wolf, 4));
        run.Objective = new Objective(ObjectiveKind.King);
        run.Apply(new Move(MoveKind.Bestow));
        Check.That(run.Offers.Select(offer => offer.Kind).SequenceEqual([MoveKind.Instrument, MoveKind.Reward]));
    }

    [Test]
    public void ObjectiveToolJoinsTheEndOfAQueuedRewardPick()
    {
        var run = Boards.At(Phase.Manage, 5, (5, Beast.Wolf, 3), (6, Beast.Rat, 3));
        run.Objective = new Objective(ObjectiveKind.King);
        run.Queue.Add(new Offer(MoveKind.Reward, [0, 1, 2, 3]));
        run.Pay();
        Check.That(run.Offers.Select(offer => offer.Kind).SequenceEqual([MoveKind.Reward, MoveKind.Instrument]));
    }

    [Test]
    public void IronLawMakesThirtyDays()
    {
        Check.Equal(30, new Run(new Rules { Card = StartCard.IronLaw }, new Rng(1)).MaxDay);
        Check.Equal(20, new Run(new Rules(), new Rng(1)).MaxDay);
    }

    [Test]
    public void CageFightEatsLowerTiersKeepsItsBeastAndTyrannyBestowsAndInstigatesThreeTimes()
    {
        var cage = new Run(new Rules { Card = StartCard.CageFight }, new Rng(1)) { Position = 5, Phase = Phase.Manage, Chance = new Chance([0, 0, 0, 1]) };
        cage.Cells[5] = new Cell(true, Beast.Mammoth, 2, false);
        cage.Cells[6] = new Cell(true, Beast.Lion, 0, false);
        cage.Cells[7] = new Cell(true, Beast.Wolf, 1, false);
        cage.Cells[8] = new Cell(true, Beast.Lion, 3, false);
        Check.That(cage.Legal().Where(move => move.Kind == MoveKind.Consume).Select(move => move.Cell).SequenceEqual([6, 7]));
        cage.Apply(new Move(MoveKind.Consume, 6));
        Check.Equal(new Cell(true, Beast.Mammoth, 3, false), cage.Cells[5]);
        var tyranny = new Run(Boards.Quiet with { Card = StartCard.Tyranny }, new Rng(1)) { Position = 14, Phase = Phase.Patrol, Doors = [0] };
        tyranny.Cells[5] = new Cell(true, Beast.Wolf, 0, false);
        tyranny.Apply(new Move(MoveKind.Door, 0));
        Check.Equal(Phase.Office, tyranny.Phase);
        Check.Equal(3, tyranny.Cells[5].Tier);
        Check.Equal(4, tyranny.Where(cell => cell.Event is not null).Length);
    }

    [Test]
    public void OracleObjectiveScenariosMatch()
    {
        Check.That(SkippedScenarios.Keys.All(name => Scenarios.Any(scenario => scenario.Name == name)), "skip list names a scenario that is not in objectives.json");
        var matched = 0;
        foreach (var scenario in Scenarios.Where(scenario => !SkippedScenarios.ContainsKey(scenario.Name)))
        {
            for (ulong seed = 1; seed <= 4; seed++)
            {
                var difference = OracleData.Replay(scenario, seed);
                Check.That(difference is null, $"{scenario.Name}: {difference}");
            }
            matched++;
        }
        Check.Equal(Scenarios.Length - SkippedScenarios.Count, matched);
    }

    [Test]
    public void ObjectiveAndStartingPickFrequenciesMatchTheOracle()
    {
        var all = OracleData.Load("objectives").Frequencies;
        var doors = OracleData.Load("doors").Frequencies.Single(frequency => frequency.Name == "run objective drawn at the start of a run");
        OracleData.CheckCounts(doors, trial => [$"objective:{Run.Start(new Rules(), (ulong)trial + 1).Objective.Kind.ToString().ToLowerInvariant()}"]);
        foreach (var frequency in all)
        {
            if (frequency.Action.Kind == "newRun")
                OracleData.CheckCounts(frequency, trial => Offered(Run.Start(new Rules(), (ulong)trial + 1), frequency));
            else
                OracleData.CheckCounts(frequency, trial =>
                {
                    var run = OracleData.ToRun(frequency.Before!, (ulong)trial * 2654435761UL + 7, frequency.Params);
                    run.Apply(OracleData.MoveOf(frequency.Action));
                    return Offered(run, frequency);
                });
        }
    }

    static IEnumerable<string> Offered(Run run, OracleFrequency frequency)
    {
        var options = run.Offers[0].Options;
        if (frequency.Outcomes.ContainsKey("count:3")) yield return $"count:{options.Length}";
        foreach (var option in options) yield return $"offer:{((ToolId)option).ToString().ToLowerInvariant()}";
    }
}
