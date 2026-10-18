using System;
using System.Collections.Generic;
using System.Linq;
using EmberHUD.Testing;

namespace Tartarus.Sim;

public sealed class EventTests
{
    static readonly Move Confirm = new(MoveKind.Confirm);

    static readonly Dictionary<string, string> SkippedScenarios = new();

    static readonly Dictionary<string, string> SkippedFrequencies = new();

    static Func<int, int> Scripted(int hits)
    {
        var rolled = 0;
        return count => count == 100 ? (rolled++ < hits ? 0 : 99) : 0;
    }

    static Run Confirmed(EventKind kind, int cell, params (int Cell, Beast Beast, int Tier)[] inmates)
    {
        var run = Boards.Event(kind, cell, inmates);
        run.Apply(Confirm);
        return run;
    }

    [Test]
    public void MarkedCellRaisesAnEventOnArrivalAndTheMarkerIsSpent()
    {
        var run = Boards.At(Phase.Patrol, 0, (1, Beast.Rat, 1));
        run.Doors = [2];
        run.Cells[2] = run.Cells[2] with { Marked = true };
        run.Apply(new Move(MoveKind.Door, 2));
        Check.Equal(Phase.Event, run.Phase);
        Check.That(run.PendingEvent is not null);
        Check.That(!run.Cells[2].Marked);
    }

    [Test]
    public void ChainReactionDoesWhatTheRulesSay()
    {
        var run = Confirmed(EventKind.ChainReaction, 3, (3, Beast.Wolf, 1));
        Check.That(run.ChainArmed);
        Check.Equal(Phase.Manage, run.Phase);
        run.Doors = [2];
        run.Phase = Phase.Patrol;
        run.Position = 0;
        run.Apply(new Move(MoveKind.Door, 2));
        Check.That(run.Cells[1].Open && run.Cells[2].Open && !run.ChainArmed, "the walk did not open the cells on the path");
    }

    [Test]
    public void ExecutionDoesWhatTheRulesSay()
    {
        var run = Confirmed(EventKind.Execution, 4, (4, Beast.Wolf, 2), (5, Beast.Rat, 0), (6, Beast.Rat, 1), (7, Beast.Rat, 2), (8, Beast.Mammoth, 0));
        Check.Equal(4, run.Cells[4].Tier);
        Check.Equal(Beast.Wolf, run.Cells[4].Beast);
        Check.That(!run.Cells[5].Open && !run.Cells[6].Open && run.Cells[7].Open && run.Cells[8].Open);
    }

    [Test]
    public void BehindClosedDoorsDoesWhatTheRulesSay()
    {
        var run = Confirmed(EventKind.BehindClosedDoors, 4, (4, Beast.Lion, 0), (5, Beast.Mammoth, 1), (6, Beast.Mammoth, 0), (7, Beast.Wolf, 0));
        Check.Equal(new Cell(true, Beast.Rat, 4, false), run.Cells[4]);
    }

    [Test]
    public void RavenousAppetiteDoesWhatTheRulesSay()
    {
        var run = Boards.Event(EventKind.RavenousAppetite, 4, (4, Beast.Wolf, 0), (5, Beast.Rat, 3), (6, Beast.Lion, 2));
        run.Chance = new Chance(Index: Scripted(3));
        run.Apply(Confirm);
        Check.Equal(3, run.Cells[4].Tier);
        Check.Equal(3, run.Cells[5].Tier);
        Check.Equal(2, run.Cells[6].Tier);
    }

    [Test]
    public void HighRiskConvictDoesWhatTheRulesSay()
    {
        for (ulong seed = 1; seed <= 30; seed++)
        {
            var run = Boards.Event(seed, EventKind.HighRiskConvict, 4, (4, Beast.Wolf, 1));
            run.Apply(Confirm);
            var opened = run.Where(cell => cell.Open && cell.Tier >= 3);
            Check.Equal(1, opened.Length);
            Check.Equal(2, run.Where(cell => cell.Open).Length);
        }
        var full = Boards.Event(EventKind.HighRiskConvict, 4, (4, Beast.Wolf, 1));
        foreach (var cell in Enumerable.Range(1, 15).Where(cell => cell != 4)) full.Cells[cell] = new Cell(true, Beast.Wolf, 1, false);
        full.Apply(Confirm);
        Check.That(full.Where(cell => cell.Open && cell.Tier != 1).Length == 0, "nothing should change with no locked cell");
    }

    [Test]
    public void CrowWhispersDoesWhatTheRulesSay()
    {
        var run = Boards.Event(EventKind.CrowWhispers, 4, (4, Beast.Wolf, 1), (5, Beast.Rat, 0));
        run.Cells[9] = run.Cells[9] with { Markers = Marker.Nest, NestDay = 9 };
        run.Apply(Confirm);
        Check.That(run.Where(cell => cell.Known).SequenceEqual([6, 7, 8, 10]), "known cells " + string.Join(',', run.Where(cell => cell.Known)));
        Check.That(run.Cells[6].Tier >= 0, "a revealed occupant was not rolled");
        var wrapped = Confirmed(EventKind.CrowWhispers, 14, (14, Beast.Wolf, 1));
        Check.That(wrapped.Where(cell => cell.Known).SequenceEqual([1, 2, 3, 15]), "the office or the fifth cell was shown");
    }

    [Test]
    public void TasteOfFreedomDoesWhatTheRulesSay()
    {
        var run = Confirmed(EventKind.TasteOfFreedom, 4, (4, Beast.Wolf, 1));
        Check.Equal(4, run.Where(cell => cell.Open).Length);
        var few = Boards.Event(EventKind.TasteOfFreedom, 4, (4, Beast.Wolf, 1));
        foreach (var cell in Enumerable.Range(1, 15).Where(cell => cell is not (4 or 6 or 9))) few.Cells[cell] = new Cell(true, Beast.Wolf, 1, false);
        few.Apply(Confirm);
        Check.Equal(15, few.Where(cell => cell.Open).Length);
    }

    [Test]
    public void UsurpationIDoesWhatTheRulesSay()
    {
        var run = Confirmed(EventKind.UsurpationI, 4, (4, Beast.Wolf, 1));
        Check.Equal(2, run.Cells[4].Tier);
        Check.That(!run.EventPool.Contains(EventKind.UsurpationI) && run.EventPool[^1] == EventKind.UsurpationII);
        var placed = run.Where(cell => cell.Event == EventKind.UsurpationII);
        Check.Equal(1, placed.Length);
        Check.That(run.Cells[placed[0]].Seen);
    }

    [Test]
    public void UsurpationIIDoesWhatTheRulesSay()
    {
        var run = Boards.Event(EventKind.UsurpationII, 4, (4, Beast.Wolf, 0), (5, Beast.Rat, 0), (6, Beast.Lion, 0));
        run.Pool = [EventKind.UsurpationII];
        run.Apply(Confirm);
        Check.Equal(2, run.Tiers().Count(tier => tier == 1));
        Check.That(run.EventPool.SequenceEqual([EventKind.UsurpationIII]));
    }

    [Test]
    public void UsurpationIIIDoesWhatTheRulesSay()
    {
        var run = Boards.Event(EventKind.UsurpationIII, 4, (4, Beast.Wolf, 0), (5, Beast.Rat, 0), (6, Beast.Lion, 0));
        run.Pool = [EventKind.UsurpationIII];
        run.Apply(Confirm);
        Check.Equal(2, run.Tiers().Count(tier => tier == 2));
        Check.That(run.EventPool.SequenceEqual([EventKind.UsurpationIV]));
    }

    [Test]
    public void UsurpationIVDoesWhatTheRulesSay()
    {
        var run = Boards.Event(EventKind.UsurpationIV, 4, (4, Beast.Wolf, 0), (5, Beast.Rat, 3));
        run.Pool = [EventKind.UsurpationIV, EventKind.Execution];
        run.Apply(Confirm);
        Check.Equal(2, run.Cells[4].Tier);
        Check.That(!run.Cells[5].Open);
        Check.Equal(1, run.Chromatics);
        Check.That(run.EventPool.SequenceEqual([EventKind.Execution]));
    }

    [Test]
    public void SewerRatwayDoesWhatTheRulesSay()
    {
        var run = Confirmed(EventKind.SewerRatway, 4, (4, Beast.Wolf, 1), (9, Beast.Lion, 1));
        Check.Equal(Phase.Patrol, run.Phase);
        Check.That(run.Legal().Select(move => move.Cell).SequenceEqual(Enumerable.Range(1, 15)), "ratway destinations are every cell but the office");
        run.Apply(new Move(MoveKind.Door, 9));
        Check.Equal(9, run.Position);
        Check.Equal(Phase.Manage, run.Phase);
        Check.That(run.Cells.All(cell => !cell.Seen), "the ratway move marked cells seen");
    }

    [Test]
    public void TreasureHuntDoesWhatTheRulesSay()
    {
        var run = Boards.Event(EventKind.TreasureHunt, 4, (4, Beast.Wolf, 1));
        run.Hide(6, Beast.Rat, 0);
        run.Hide(7, Beast.Rat, 3);
        run.Cells[8] = new Cell(false, Beast.Rat, 0, false, Markers: Marker.Nest, NestDay: 9);
        run.Apply(Confirm);
        Check.That(run.Cells[6].Known && run.Cells[6].AtLeast == 0);
        Check.That(!run.Cells[7].Known && run.Cells[7].AtLeast == Tiers.Rare);
        Check.That(!run.Cells[8].Known && run.Cells[8].AtLeast == 0, "a nest was touched");
        Check.That(run.Cells[10].Tier >= 0, "unseen occupants are rolled");
    }

    [Test]
    public void EventsHideBehindAboutFivePercentOfLockedCells()
    {
        const int runs = 3000;
        var hidden = 0;
        for (ulong seed = 1; seed <= runs; seed++)
            hidden += Run.Start(new Rules(), seed).Where(cell => cell.Event is not null).Length;
        var share = hidden / (runs * 15.0);
        var tolerance = 4 * Math.Sqrt(0.05 * 0.95 / (runs * 15)) + 1.0 / (runs * 15);
        Check.That(Math.Abs(share - 0.05) <= tolerance, $"share {share:F4}");
        Check.Equal(0, Run.Start(new Rules { EventChance = 0 }, 1).Where(cell => cell.Event is not null).Length);
        Check.Equal(15, Run.Start(new Rules { EventChance = 100 }, 1).Where(cell => cell.Event is not null).Length);
    }

    [Test]
    public void EventsCanBeSkippedExceptTreasureHunt()
    {
        foreach (var kind in Enum.GetValues<EventKind>())
        {
            var run = Boards.Event(kind, 4, (4, Beast.Wolf, 1), (5, Beast.Rat, 1));
            var before = run.Cells.ToArray();
            var pool = run.EventPool.ToArray();
            var canSkip = run.Legal().Contains(new Move(MoveKind.Skip));
            Check.Equal(kind != EventKind.TreasureHunt, canSkip);
            if (!canSkip)
            {
                Check.Throws<ArgumentException>(() => run.Apply(new Move(MoveKind.Skip)));
                continue;
            }
            run.Apply(new Move(MoveKind.Skip));
            Check.That(run.Cells.SequenceEqual(before) && run.EventPool.SequenceEqual(pool) && !run.ChainArmed && run.Phase == Phase.Manage && run.PendingEvent is null, $"{kind} changed something when skipped");
        }
    }

    [Test]
    public void OracleEventScenariosMatch()
    {
        var all = OracleData.Load("events").Scenarios;
        Check.That(SkippedScenarios.Keys.All(name => all.Any(scenario => scenario.Name == name)), "skip list names a scenario that is not in events.json");
        var matched = 0;
        foreach (var scenario in all.Where(scenario => !SkippedScenarios.ContainsKey(scenario.Name)))
        {
            for (ulong seed = 1; seed <= 4; seed++)
            {
                var run = OracleData.ToRun(scenario.Before, seed, scenario.Params);
                if (scenario.Random && scenario.Branch is { } branch && branch.StartsWith("gain"))
                    run.Chance = run.Chance with { Index = Scripted(int.Parse(branch[4..])) };
                var move = OracleData.MoveOf(scenario.Action);
                if (scenario.Result?.Ok == false) Check.Throws<ArgumentException>(() => run.Apply(move));
                else run.Apply(move);
                var difference = OracleData.Differences(run, scenario.After);
                Check.That(difference is null, $"{scenario.Name}: {difference}");
            }
            matched++;
        }
        Check.Equal(all.Length - SkippedScenarios.Count, matched);
    }

    [Test]
    public void OracleEventFrequenciesAreWithinTolerance()
    {
        var all = OracleData.Load("events").Frequencies;
        Check.That(SkippedFrequencies.Keys.All(name => all.Any(frequency => frequency.Name == name)), "skip list names a frequency that is not in events.json");
        var frequencies = all.Where(frequency => !SkippedFrequencies.ContainsKey(frequency.Name)).ToArray();
        Check.Equal(all.Length - SkippedFrequencies.Count, frequencies.Length);
        for (var index = 0; index < frequencies.Length; index++)
        {
            var frequency = frequencies[index];
            OracleData.CheckFrequency(frequency, index, run => Outcomes(frequency, run));
        }
    }

    static string Name(Beast beast) => beast.ToString().ToLowerInvariant();

    static IEnumerable<string> Outcomes(OracleFrequency frequency, Run run)
    {
        var before = frequency.Before!;
        var wasOpen = Enumerable.Range(0, 16).Where(cell => before.Cells[cell].Open).ToArray();
        var opened = Enumerable.Range(1, 15).Where(cell => !before.Cells[cell].Open && run.Cells[cell].Open).ToArray();
        if (frequency.Measure.StartsWith("beast:")) yield return $"beast:{Name(run.Cells[4].Beast)}";
        else if (frequency.Measure.StartsWith("gain:")) yield return $"gain:{(run.Cells[4].Open ? run.Cells[4].Tier : 5)}";
        else if (frequency.Measure.StartsWith("who:"))
        {
            var who = wasOpen.Where(cell => !run.Cells[cell].Open || run.Cells[cell].Tier != before.Cells[cell].Tier).ToArray();
            yield return who.Length == 0 ? "who:none" : $"who:{who[0]}";
        }
        else if (frequency.Measure.StartsWith("cell:"))
        {
            foreach (var cell in opened)
            {
                yield return $"cell:{cell}";
                yield return $"tier:{run.Cells[cell].Tier}";
                yield return $"beast:{Name(run.Cells[cell].Beast)}";
            }
        }
        else if (frequency.Measure.StartsWith("open:"))
        {
            foreach (var cell in opened) yield return $"open:{cell}";
        }
        else if (frequency.Measure.StartsWith("rose:"))
        {
            foreach (var cell in wasOpen.Where(cell => run.Cells[cell].Tier > before.Cells[cell].Tier)) yield return $"rose:{cell}";
        }
        else
        {
            var low = Enumerable.Range(6, 5).Where(cell => run.Cells[cell].Known).ToArray();
            yield return $"revealed:{low.Length}";
            foreach (var cell in Enumerable.Range(6, 5)) yield return low.Contains(cell) ? $"low:{cell}" : $"dark:{cell}";
        }
    }
}
