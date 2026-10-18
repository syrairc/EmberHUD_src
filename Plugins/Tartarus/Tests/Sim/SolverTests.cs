using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading;
using EmberHUD.Testing;

namespace Tartarus.Sim;

sealed record SolverOption(string Label, OracleAction Action, double Mean, double? GapSe);

sealed record SolverSeed(int Seed, SolverOption[] Options);

sealed record SolverBoard(string Name, OracleBoard Board, SolverSeed[] Runs, OracleAction BestAction, bool Decisive);

sealed record SolverStrategy(int Runs, double Mean, double Se);

sealed record SolverFile(int Rollouts, SolverStrategy Strategy, SolverBoard[] Boards);

public sealed class SolverTests
{
    const int Playouts = 300;

    const double WorstValueGap = 0.6;

    const double MeanValueGap = 0.1;

    // recorded reference strategy mean 3.226 +- 0.092 over 500 runs; tolerance 0.25 is about 2.7 standard errors
    const double MeanTolerance = 0.25;

    static readonly OracleParams RecordedParams = new(null, null, null, null, 72, 5, 9, 4);

    static readonly Lazy<SolverFile> Recorded = new(() =>
    {
        var path = Path.Combine(AppContext.BaseDirectory, "Plugins", "Tartarus", "Tests", "Oracle", "solver.json");
        return JsonSerializer.Deserialize<SolverFile>(File.ReadAllText(path), new JsonSerializerOptions { PropertyNameCaseInsensitive = true })!;
    });

    static Run Crowded() => Boards.At(Phase.Manage, 5, (5, Beast.Wolf, 2), (6, Beast.Rat, 2), (7, Beast.Lion, 1), (9, Beast.Mammoth, 3), (12, Beast.Rat, 0));

    [Test]
    public void RankReturnsOneEntryPerLegalMove()
    {
        var run = Crowded();
        var legal = run.Legal();
        var ranked = Solver.Rank(run, 20, new Rng(1));
        Check.That(legal.Count > 2, "the board offers too few moves");
        Check.That(ranked.Select(entry => entry.Move).Order(Comparer<Move>.Create((a, b) => (a.Kind, a.Cell, a.Param).CompareTo((b.Kind, b.Cell, b.Param))))
            .SequenceEqual(legal.Order(Comparer<Move>.Create((a, b) => (a.Kind, a.Cell, a.Param).CompareTo((b.Kind, b.Cell, b.Param))))), "ranking does not list each legal move once");
        for (var i = 1; i < ranked.Length; i++) Check.That(ranked[i - 1].Value >= ranked[i].Value, "ranking is not best first");
        Check.That(ranked.All(entry => entry.Spread >= 0 && entry.Value >= 0));
    }

    [Test]
    public void RankMarksTheStrategyPickAndGivesPairedMarginsAgainstTheLeader()
    {
        var run = Crowded();
        var ranked = Solver.Rank(run, 40, new Rng(2));
        Check.Equal(Strategy.Choose(run, new Rng(1)), ranked.Single(entry => entry.Strategy).Move);
        Check.Equal(0.0, ranked[0].GapSe);
        Check.That(ranked.Skip(1).All(entry => entry.GapSe > 0), "a trailing option has no paired margin");
        var only = Solver.Rank(Boards.Event(EventKind.TreasureHunt, 4, (4, Beast.Wolf, 1)), 40, new Rng(1));
        Check.That(only.Single().Strategy, "the only move is the strategy's pick");
    }

    [Test]
    public void SameSeedGivesTheSameRanking()
    {
        var first = Solver.Rank(Crowded(), 30, new Rng(7));
        var second = Solver.Rank(Crowded(), 30, new Rng(7));
        Check.That(first.SequenceEqual(second), "same seed ranked differently");
        Check.That(!first.SequenceEqual(Solver.Rank(Crowded(), 30, new Rng(8))) || first.All(entry => entry.Value == 0), "a different seed gave the same ranking");
    }

    [Test]
    public void SolverDoesNotChangeTheStartingRun()
    {
        static string Snapshot(Run run) => string.Join('|', run.Day, run.Position, run.Chromatics, run.Phase, run.Objective, string.Join(',', run.Held), string.Join(',', run.Pool),
            string.Join(',', run.Prey), string.Join(',', run.Queue.Select(offer => $"{offer.Kind}{string.Join('.', offer.Options)}")), string.Join(',', run.Doors),
            string.Join(',', run.Cells.Select(cell => $"{cell.Open}{cell.Beast}{cell.Tier}{cell.Event}")));
        var run = Crowded();
        run.Held.Add(ToolId.Saw);
        run.Prey.Add(new PreyMark(5, 6, 2, 3));
        run.Queue.Add(new Offer(MoveKind.Reward, [0, 1]));
        run.Objective = new Objective(ObjectiveKind.Walk, 4);
        run.Doors = [2, 3];
        var before = Snapshot(run);
        Solver.Rank(run, 10, new Rng(1));
        Check.Equal(before, Snapshot(run));
    }

    [Test]
    public void TwoStartToolsNeverOfferAHeldTool()
    {
        for (ulong seed = 1; seed <= 100; seed++)
        {
            var run = Run.Start(new Rules { StartTools = 2 }, seed);
            while (run.Offers.Count > 0)
            {
                var legal = run.Legal();
                Check.That(legal.All(move => !run.Holds((ToolId)move.Param)), $"seed {seed}: a held tool is on offer");
                run.Apply(legal[0]);
            }
            Check.Equal(2, run.Tools.Count);
        }
    }

    [Test]
    public void ZeroOrOneOptionReturnsAtOnce()
    {
        using var guard = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        var ended = Boards.At(Phase.Ended, 0);
        Check.Equal(0, Solver.Rank(ended, int.MaxValue, new Rng(1), guard.Token).Length);
        var hunt = Boards.Event(EventKind.TreasureHunt, 4, (4, Beast.Wolf, 1));
        var only = Solver.Rank(hunt, int.MaxValue, new Rng(1), guard.Token);
        Check.Equal(1, only.Length);
        Check.Equal(new Move(MoveKind.Confirm), only[0].Move);
        Check.That(!guard.IsCancellationRequested, "the solver ran its playouts for a single option");
    }

    [Test]
    public void CancelStopsEarly()
    {
        var run = Crowded();
        using var already = new CancellationTokenSource();
        already.Cancel();
        var none = Solver.Rank(run, int.MaxValue, new Rng(1), already.Token);
        Check.Equal(run.Legal().Count, none.Length);
        using var later = new CancellationTokenSource(TimeSpan.FromMilliseconds(150));
        var clock = System.Diagnostics.Stopwatch.StartNew();
        var partial = Solver.Rank(run, int.MaxValue, new Rng(1), later.Token);
        Check.That(clock.Elapsed < TimeSpan.FromSeconds(10), "cancel did not stop the solver");
        Check.Equal(run.Legal().Count, partial.Length);
        Check.That(partial.Any(entry => entry.Value > 0), "the partial result has no playouts");
    }

    [Test]
    public void ObviousConsumeBeatsEndOnAKnownBoard()
    {
        var run = Boards.At(Phase.Manage, 5, (5, Beast.Rat, 4), (6, Beast.Mammoth, 4));
        run.Day = 19;
        var ranked = Solver.Rank(run, 50, new Rng(3));
        Check.Equal(new Move(MoveKind.Consume, 6), ranked[0].Move);
        Check.That(ranked[0].Value >= 1, "the Chromatic was not counted");
        Check.That(ranked.First(entry => entry.Move.Kind == MoveKind.End).Value < 0.5, "ending the day should not reach a Chromatic here");
    }

    [Test]
    public void HiddenCellsAreRerolledPerPlayout()
    {
        var run = Boards.At(Phase.Patrol, 0);
        run.Doors = [2, 3];
        run.Cells[2] = new Cell(false, Beast.Mammoth, 4, false);
        var seen = new HashSet<int>();
        for (ulong seed = 1; seed <= 60; seed++)
        {
            var copy = run.Reroll(new Rng(seed));
            seen.Add(copy.Cells[2].Tier);
            Check.Equal(-1, copy.Cells[2].Tier);
        }
        run.Cells[3] = new Cell(false, Beast.Wolf, 3, true);
        Check.Equal(3, run.Reroll(new Rng(1)).Cells[3].Tier);
        run.Cells[4] = new Cell(false, Beast.Rat, -1, false, AtLeast: Tiers.Rare);
        Check.That(Enumerable.Range(1, 40).All(seed => run.Reroll(new Rng((ulong)seed)).Cells[4].AtLeast == Tiers.Rare));
    }

    [Test]
    public void StrategyOnlyPicksLegalMoves()
    {
        var rng = new Rng(11);
        for (ulong seed = 1; seed <= 60; seed++)
        {
            var run = Run.Start(new Rules(), seed);
            for (var step = 0; step < 2000 && !run.Over; step++)
            {
                var move = Strategy.Choose(run, rng);
                Check.That(run.Legal().Contains(move), $"seed {seed}: {move} is not legal in {run.Phase}");
                run.Apply(move);
            }
            Check.That(run.Over, $"seed {seed} never ended");
        }
    }

    [Test]
    public void AgreesWithTheReferenceOnRecordedBoards()
    {
        var boards = Recorded.Value.Boards;
        Check.That(boards.Length >= 30, "solver.json has too few boards");
        var disagreements = new List<string>();
        var gaps = new List<double>();
        for (var index = 0; index < boards.Length; index++)
        {
            var board = boards[index];
            var run = OracleData.ToRun(board.Board, 1, RecordedParams);
            var ranked = Solver.Rank(run, Playouts, new Rng((ulong)index + 1));
            var options = board.Runs.SelectMany(seed => seed.Options.Select(option => (Move: OracleData.MoveOf(option.Action), option.Mean, option.GapSe))).GroupBy(option => option.Move)
                .Select(group => (Move: group.Key, Mean: group.Average(option => option.Mean), GapSe: group.Max(option => option.GapSe ?? 0))).ToList();
            var best = options.MaxBy(option => option.Mean);
            var ours = options.FirstOrDefault(option => option.Move == ranked[0].Move);
            // decisive boards need the exact best option; others agree when our pick is among the recorded options and trails the reference best by no more than max(0.05, 3x paired gapSE)
            var agree = board.Decisive ? ranked[0].Move == OracleData.MoveOf(board.BestAction) : ours.Move == ranked[0].Move && best.Mean - ours.Mean <= Math.Max(0.05, 3 * ours.GapSe);
            if (!agree) disagreements.Add($"{board.Name}: ours {ranked[0].Move} ({ranked[0].Value:F2}), reference {best.Move} ({best.Mean:F2})");
            if (ours.Move == ranked[0].Move) gaps.Add(Math.Abs(ranked[0].Value - ours.Mean));
        }
        Check.That(disagreements.Count <= boards.Length / 10, $"{disagreements.Count} of {boards.Length} boards disagree: {string.Join(" | ", disagreements)}");
        Check.That(gaps.Max() <= WorstValueGap, $"best value differs from the reference by {gaps.Max():F2}");
        Check.That(gaps.Average() <= MeanValueGap, $"best values differ from the reference by {gaps.Average():F2} on average");
    }

    [Test]
    public void StrategyIsNearTheReferenceMean()
    {
        var reference = Recorded.Value.Strategy;
        Check.Equal(500, reference.Runs);
        var total = 0.0;
        for (var seed = 0; seed < reference.Runs; seed++)
        {
            var run = Run.Start(new Rules { StartTools = 0 }, (ulong)seed + 1);
            var rng = new Rng((ulong)seed + 1);
            for (var step = 0; step < 5000 && !run.Over; step++) run.Apply(Strategy.Choose(run, rng));
            total += run.Chromatics;
        }
        var mean = total / reference.Runs;
        Check.That(mean >= reference.Mean - MeanTolerance, $"strategy mean {mean:F3} Chromatics, reference {reference.Mean:F3} (tolerance {MeanTolerance})");
    }

    [Test]
    public void LockedTierFiveAndAtLeastFiveRankWithoutThrowing()
    {
        var run = Boards.At(Phase.Patrol, 0, (1, Beast.Lion, 3));
        run.Cells[3] = new Cell(false, Beast.Lion, 5, true, AtLeast: 5);
        run.Cells[4] = new Cell(false, Beast.Wolf, -1, false, AtLeast: 9);
        run.Doors = [2, 3, 4];
        Check.That(Solver.Rank(run, 10, new Rng(1)).Length == 3);
    }
}
