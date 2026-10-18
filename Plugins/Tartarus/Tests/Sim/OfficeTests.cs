using System;
using System.Collections.Generic;
using System.Linq;
using EmberHUD.Testing;

namespace Tartarus.Sim;

public sealed class OfficeTests
{
    static readonly Dictionary<string, string> SkippedScenarios = new();

    [Test]
    public void TheOfficeOffersInstigateBestowAndEnd()
    {
        var run = Boards.At(Phase.Office, 0, (5, Beast.Wolf, 1));
        Check.That(run.Legal().SequenceEqual([new Move(MoveKind.Instigate), new Move(MoveKind.Bestow), new Move(MoveKind.End)]));
        run.Apply(new Move(MoveKind.Bestow));
        Check.Equal(Phase.Manage, run.Phase);
        Check.That(run.Legal().SequenceEqual([new Move(MoveKind.End)]), "a second office choice is offered");
        Check.Throws<ArgumentException>(() => Boards.At(Phase.Manage, 5, (5, Beast.Wolf, 1)).Apply(new Move(MoveKind.Bestow)));
    }

    [Test]
    public void BestowRaisesOneInmateOneTier()
    {
        for (ulong seed = 1; seed <= 20; seed++)
        {
            var run = Boards.At(seed, Phase.Office, 0, (3, Beast.Wolf, 0), (5, Beast.Rat, 1), (7, Beast.Lion, 2));
            var before = run.Cells.ToArray();
            run.Apply(new Move(MoveKind.Bestow));
            var changed = Enumerable.Range(0, 16).Where(cell => run.Cells[cell] != before[cell]).ToArray();
            Check.Equal(1, changed.Length);
            Check.Equal(before[changed[0]].Tier + 1, run.Cells[changed[0]].Tier);
            Check.Equal(before[changed[0]].Beast, run.Cells[changed[0]].Beast);
        }
    }

    [Test]
    public void BestowOnASupremeVaultsARainbowAndOnNobodyDoesNothing()
    {
        var run = Boards.At(Phase.Office, 0, (5, Beast.Wolf, 4));
        run.Apply(new Move(MoveKind.Bestow));
        Check.Equal(1, run.Chromatics);
        Check.That(!run.Cells[5].Open);
        var empty = Boards.At(Phase.Office, 0);
        empty.Apply(new Move(MoveKind.Bestow));
        Check.Equal(0, empty.Chromatics);
    }

    [Test]
    public void InstigateMakesTwoSeenEventsOnABoardWithNoneAndOneOnABoardWithOne()
    {
        var run = Boards.At(Phase.Office, 0, (5, Beast.Wolf, 1));
        run.Apply(new Move(MoveKind.Instigate));
        var marked = run.Where(cell => cell.Event is not null);
        Check.Equal(2, marked.Length);
        Check.That(marked.All(cell => run.Cells[cell].Seen && run.EventPool.Contains(run.Cells[cell].Event!.Value)), "event unseen or outside the pool");
        Check.Equal(Phase.Manage, run.Phase);
        for (ulong seed = 1; seed <= 20; seed++)
        {
            var one = Boards.At(seed, Phase.Office, 0, (5, Beast.Wolf, 1));
            one.Cells[9] = one.Cells[9] with { Event = EventKind.CrowWhispers };
            one.Apply(new Move(MoveKind.Instigate));
            Check.Equal(2, one.Where(cell => cell.Event is not null).Length);
        }
    }

    [Test]
    public void InstigateAlsoUsesOpenCellsWithAnInmateAndNeverTheOfficeOrEmptyOnes()
    {
        var run = Boards.At(Phase.Office, 0, (5, Beast.Wolf, 1));
        for (var cell = 1; cell < 16; cell++) if (cell != 5) run.Cells[cell] = run.Cells[cell] with { Event = EventKind.CrowWhispers };
        run.Apply(new Move(MoveKind.Instigate));
        Check.That(run.Cells[5].Open && run.Cells[5].Seen && run.Cells[5].Event is not null, "the inmate's open cell got no event");
        Check.That(run.Cells[0].Event is null);
        var lonely = Boards.At(Phase.Office, 0, (5, Beast.Wolf, 1));
        for (var cell = 1; cell < 16; cell++) if (cell != 5) lonely.Cells[cell] = lonely.Cells[cell] with { Markers = Marker.Nest };
        lonely.Apply(new Move(MoveKind.Instigate));
        Check.That(lonely.Where(cell => cell.Event is not null).SequenceEqual([5]), "only the inmate's cell was free");
    }

    [Test]
    public void InstigateSkipsCellsThatHoldAnEventOrANest()
    {
        var run = Boards.At(Phase.Office, 0);
        for (var cell = 1; cell < 16; cell++) run.Cells[cell] = run.Cells[cell] with { Event = EventKind.CrowWhispers };
        run.Cells[4] = new Cell(false, Beast.Mammoth, -1, false, Markers: Marker.Nest, NestDay: 5);
        run.Cells[6] = run.Cells[6] with { Event = null };
        run.Apply(new Move(MoveKind.Instigate));
        Check.That(run.Cells[6].Event is not null && run.Cells[4].Event is null, "one free cell should get the event, the nest none");
        Check.Equal(13, run.Where(cell => cell.Event == EventKind.CrowWhispers).Length);
    }

    [Test]
    public void OracleOfficeScenariosMatch()
    {
        var all = OracleData.Load("office").Scenarios;
        Check.That(SkippedScenarios.Keys.All(name => all.Any(scenario => scenario.Name == name)), "skip list names a scenario that is not in office.json");
        var matched = 0;
        foreach (var scenario in all.Where(scenario => !SkippedScenarios.ContainsKey(scenario.Name)))
        {
            for (ulong seed = 1; seed <= 4; seed++)
            {
                var run = OracleData.ToRun(scenario.Before, seed, scenario.Params);
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
    public void OracleOfficeFrequenciesAreWithinTolerance()
    {
        var frequencies = OracleData.Load("office").Frequencies;
        for (var index = 0; index < frequencies.Length; index++)
        {
            var frequency = frequencies[index];
            OracleData.CheckFrequency(frequency, index, run => frequency.Action.Id == "bestow" ? Rose(frequency, run) : Placed(frequency, run));
        }
    }

    static IEnumerable<string> Rose(OracleFrequency frequency, Run run) =>
        Enumerable.Range(0, run.Cells.Length).Where(cell => run.Cells[cell].Tier != frequency.Before!.Cells[cell].Tier && frequency.Before.Cells[cell].Open).Select(cell => $"rose:{cell}");

    static IEnumerable<string> Placed(OracleFrequency frequency, Run run)
    {
        var marked = Enumerable.Range(0, 16).Where(cell => frequency.Before!.Cells[cell].Markers?.Event is null && run.Cells[cell].Event is not null).ToArray();
        yield return $"count:{marked.Length}";
        foreach (var cell in marked)
        {
            yield return $"cell:{cell}";
            yield return $"kind:{OracleData.IdOf(run.Cells[cell].Event!.Value)}";
        }
    }
}
