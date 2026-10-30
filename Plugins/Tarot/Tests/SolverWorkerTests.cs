using System;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using EmberHUD;
using EmberHUD.Testing;

namespace Tarot;

public sealed class SolverWorkerTests
{
    static readonly SolverOptions Quick = SolverOptions.Default with { Rollouts = 20 };

    static TarotState Board(int position = 0, int tiles = 30) => new(TarotPhase.Board, 1, 0, 5, position,
        [.. Enumerable.Range(0, tiles).Select(i => new TarotTile(i, i % 4 == 3 ? 2010 : 10000, i % 4 == 3 ? 3 : 0))],
        [new(1, 1), new(2, 2), new(3, 3)], [], 0);

    static bool WaitFor(Func<bool> done)
    {
        var watch = Stopwatch.StartNew();
        while (!done())
        {
            if (watch.ElapsedMilliseconds > 5000) return false;
            Thread.Sleep(5);
        }
        return true;
    }

    [Test]
    public void ProducesResultForBoard()
    {
        using var worker = new SolverWorker();
        var state = Board();
        worker.Request(state, Quick);
        Check.That(WaitFor(() => worker.Latest is not null), "result arrived");
        var (signature, results) = worker.Latest!.Value;
        Check.Equal(StateMapper.Signature(state), signature);
        Check.That(results.Length > 1 && results.All(r => r.Samples > 0), "ranked with samples");
    }

    [Test]
    public void UnchangedStateDoesNotRestart()
    {
        using var worker = new SolverWorker();
        worker.Request(Board(), Quick);
        Check.That(WaitFor(() => worker.Latest is not null), "first result");
        var jobs = worker.Jobs;
        var results = worker.Latest!.Value.Results;
        worker.Request(Board(), Quick);
        Thread.Sleep(50);
        Check.Equal(jobs, worker.Jobs);
        Check.That(ReferenceEquals(results, worker.Latest!.Value.Results), "result kept");
        worker.Request(Board(), Quick with { Rollouts = 21 });
        Check.Equal(jobs + 1, worker.Jobs);
        worker.Request(Board(), Quick with { Rollouts = 21, DeathLosesLoot = false });
        Check.Equal(jobs + 2, worker.Jobs);
    }

    [Test]
    public void NewBoardDropsStaleResult()
    {
        using var worker = new SolverWorker();
        var a = Board(0);
        var b = Board(5);
        var sigA = StateMapper.Signature(a);
        var sigB = StateMapper.Signature(b);
        var big = SolverOptions.Default with { Rollouts = 100000 };
        worker.Request(a, big);
        worker.Request(b, Quick);
        var sawA = false;
        Check.That(WaitFor(() =>
        {
            var latest = worker.Latest;
            if (latest?.Signature == sigA) sawA = true;
            return latest?.Signature == sigB;
        }), "B finished");
        Check.That(!sawA, "A never published");
        Thread.Sleep(100);
        Check.Equal(sigB, worker.Latest!.Value.Signature);
    }

    [Test]
    public void CancelledJobNeverPublishesPartialResults()
    {
        using var worker = new SolverWorker();
        worker.Request(Board(0), SolverOptions.Default with { Rollouts = 100000 });
        Thread.Sleep(30);
        worker.Request(Board(1), SolverOptions.Default with { Rollouts = 100000 });
        Thread.Sleep(30);
        Check.That(worker.Latest is null, "nothing yet");
    }

    [Test]
    public void NonBoardStateClearsLatest()
    {
        using var worker = new SolverWorker();
        worker.Request(Board(), Quick);
        Check.That(WaitFor(() => worker.Latest is not null), "result");
        worker.Request(Board() with { Phase = TarotPhase.Final }, Quick);
        Check.That(worker.Latest is null, "cleared");
        worker.Request(Board(0, 0), Quick);
        Check.That(worker.Latest is null, "still cleared");
    }

    [Test]
    public void PendingPickOnlyRanksTheOptions()
    {
        using var worker = new SolverWorker();
        var state = Board() with { Hand = [new(1, 22)], Playing = 22 };
        worker.Request(state, Quick);
        Check.That(WaitFor(() => worker.Latest is not null), "result");
        var moves = worker.Latest!.Value.Results.Select(r => r.Move).ToArray();
        Check.That(moves.Length == 3 && moves.All(m => !m.End && m.Steps is 2 or 4 or 6), "only the options");
    }

    [Test]
    public void DisposeStops()
    {
        var worker = new SolverWorker();
        worker.Request(Board(), SolverOptions.Default with { Rollouts = 100000 });
        Check.That(WaitFor(() => worker.Running), "thread up");
        var watch = Stopwatch.StartNew();
        worker.Dispose();
        worker.Dispose();
        Check.That(watch.ElapsedMilliseconds < 1500, "prompt");
        Check.That(!worker.Running, "thread stopped");
        worker.Request(Board(7), Quick);
        Thread.Sleep(30);
        Check.That(worker.Latest is null, "no work after dispose");
    }
}
