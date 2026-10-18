using System.Diagnostics;
using System.Threading;
using EmberHUD.Testing;
using Tartarus.Sim;

namespace Tartarus;

public sealed class SolverWorkerTests
{
    const int WaitMs = 30000;

    static Run Board(int tier = 2) => Boards.At(Phase.Manage, 1, (1, Beast.Lion, tier), (2, Beast.Wolf, tier), (4, Beast.Rat, 1));

    static bool Wait(SolverWorker worker, int generation) =>
        SpinWait.SpinUntil(() => worker.Latest is { Ranked: not null } latest && latest.Generation == generation, WaitMs);

    [Test]
    public void RanksASubmittedBoard()
    {
        using var worker = new SolverWorker { Playouts = 20 };
        worker.Submit(Board(), 1);
        Check.That(Wait(worker, 1), "no result");
        Check.That(worker.Latest.Ranked!.Length > 0);
    }

    [Test]
    public void StaleRankingIsNeverShownForANewBoard()
    {
        using var worker = new SolverWorker { Playouts = 20 };
        worker.Submit(Board(), 1);
        Check.That(Wait(worker, 1), "no result");
        worker.Playouts = int.MaxValue;
        worker.Submit(Board(3), 2);
        var latest = worker.Latest;
        Check.Equal(2, latest.Generation);
        Check.That(latest.Ranked is null, "generation 1 ranking served for generation 2");
    }

    [Test]
    public void WorkerRestartsWhenTheBoardChanges()
    {
        using var worker = new SolverWorker { Playouts = int.MaxValue };
        worker.Submit(Board(), 1);
        worker.Playouts = 20;
        worker.Submit(Board(3), 2);
        Check.That(Wait(worker, 2), "no result for the new board");
        Check.Equal(2, worker.Latest.Generation);
        worker.Submit(Board(4), 3);
        Check.That(Wait(worker, 3), "no result for the third board");
    }

    [Test]
    public void SubmittingTheSameGenerationTwiceKeepsTheFirst()
    {
        using var worker = new SolverWorker { Playouts = 20 };
        worker.Submit(Board(), 1);
        Check.That(Wait(worker, 1), "no result");
        var first = worker.Latest.Ranked;
        worker.Submit(Board(3), 1);
        Check.That(ReferenceEquals(first, worker.Latest.Ranked));
    }

    [Test]
    public void WorkerStopsOnDispose()
    {
        var worker = new SolverWorker { Playouts = int.MaxValue };
        worker.Submit(Board(), 1);
        var watch = Stopwatch.StartNew();
        worker.Dispose();
        Check.That(watch.ElapsedMilliseconds < 1500, $"dispose took {watch.ElapsedMilliseconds} ms");
        worker.Submit(Board(), 2);
        Check.Equal(1, worker.Latest.Generation);
        Check.That(worker.Latest.Ranked is null);
        worker.Dispose();
    }

    [Test]
    public void SubmitDoesNotTouchTheCallersRun()
    {
        using var worker = new SolverWorker { Playouts = 20 };
        var run = Board();
        var before = run.Day;
        worker.Submit(run, 1);
        Check.That(Wait(worker, 1), "no result");
        Check.Equal(before, run.Day);
        Check.That(run.Phase == Phase.Manage);
    }
}
