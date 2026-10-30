using System.Diagnostics;
using System.Linq;
using System.Threading;
using EmberHUD;
using EmberHUD.Testing;

namespace Tarot;

public sealed class SolverTests
{
    static SimState Board(int length = 20, int position = 0, int hp = 3, params int[] hand)
    {
        var state = new SimState { Position = position, Hp = hp, TileCfg = [.. Enumerable.Repeat(10000, length)], TileChest = new int[length] };
        state.Hand.AddRange(hand);
        return state;
    }

    static void Chest(SimState s, int tile, int rarity) { s.TileCfg[tile] = 2010; s.TileChest[tile] = rarity; }

    static readonly SolverOptions Opts = SolverOptions.Default with { Rollouts = 30 };

    [Test]
    public void ChestBeatsTrap()
    {
        var s = Board(hand: [1, 2]);
        s.TileCfg[1] = 1000;
        Chest(s, 2, 7);
        var r = Solver.Evaluate(s, Opts, CancellationToken.None);
        Check.That(r[0].Move.HandIndex == 1 && !r[0].Move.End, "chest card first");
        Check.That(r[0].Mean > r.First(x => x.Move.HandIndex == 0).Mean, "higher mean");
    }

    [Test]
    public void EndRunOnLastTile()
    {
        var s = Board(10, 9, hand: [1]);
        s.Taken.AddRange([7, 7]);
        var r = Solver.Evaluate(s, Opts, CancellationToken.None);
        Check.Equal(1, r.Length);
        Check.That(r[0].Move.End, "end run");
        Check.That(r[0].Mean == 2 * Opts.Weights.Values[7], "taken value");
    }

    [Test]
    public void EmptyHandOnlyEndRun()
    {
        var r = Solver.Evaluate(Board(), Opts, CancellationToken.None);
        Check.Equal(1, r.Length);
        Check.That(r[0].Move.End, "end run");
    }

    [Test]
    public void DeadStateOnlyEndRun()
    {
        var s = Board(hand: [1]);
        s.Hp = 0;
        s.Taken.Add(7);
        var r = Solver.Evaluate(s, Opts, CancellationToken.None);
        Check.Equal(1, r.Length);
        Check.That(r[0].Move.End, "end run");
    }

    [Test]
    public void DeterministicForSeed()
    {
        var s = Board(40, hand: [1, 2, 3, 4, 5]);
        Chest(s, 3, 5);
        s.TileCfg[2] = 1000;
        var a = Solver.Evaluate(s, Opts, CancellationToken.None);
        var b = Solver.Evaluate(s, Opts, CancellationToken.None);
        Check.Equal(a.Length, b.Length);
        for (var i = 0; i < a.Length; i++) Check.That(a[i] == b[i], $"result {i}");
    }

    [Test]
    public void DeathLosesLootSetting()
    {
        var s = Board(hp: 1, hand: [1, 1]);
        Chest(s, 1, 7);
        s.TileCfg[2] = 1000;
        var loses = Solver.Evaluate(s, Opts, CancellationToken.None).First(x => !x.Move.End);
        var keeps = Solver.Evaluate(s, Opts with { DeathLosesLoot = false }, CancellationToken.None).First(x => !x.Move.End);
        Check.That(loses.Mean == 0, "loot lost");
        Check.That(loses.DeadRate == 1, "always dies");
        Check.That(keeps.Mean > 0, "loot kept");
    }

    [Test]
    public void PlayCapEndsLoops()
    {
        var s = Board(40, hand: [1, 1, 1, 1, 1]);
        var r = Solver.Evaluate(s, Opts with { PlayCap = 2 }, CancellationToken.None);
        Check.That(r.All(x => x.Samples == (x.Move.End ? 1 : 30)), "all rollouts returned");
    }

    [Test]
    public void CancelStopsEarly()
    {
        var s = Board(40, hand: [1, 2, 3]);
        using var cts = new CancellationTokenSource();
        cts.Cancel();
        var r = Solver.Evaluate(s, Opts, cts.Token);
        Check.That(r.Where(x => !x.Move.End).All(x => x.Samples < 30), "fewer samples");
    }

    [Test]
    public void PendingPickRestrictsMoves()
    {
        var s = Board(40, hand: [1, 2, 3]);
        var only = new[] { new Move(0, 1, false), new Move(2, 3, false) };
        var r = Solver.Evaluate(s, only, Opts, CancellationToken.None);
        Check.Equal(2, r.Length);
        Check.That(r.All(x => only.Contains(x.Move)), "only given moves");
    }

    [Test]
    public void EightyTileBoardIsFast()
    {
        var s = Board(80, hand: [1, 2, 3, 4, 5, 1, 2, 3, 4, 5, 6]);
        for (var i = 3; i < 80; i += 4) Chest(s, i, 2 + i % 6);
        for (var i = 2; i < 80; i += 7) s.TileCfg[i] = 1000;
        var watch = Stopwatch.StartNew();
        var r = Solver.Evaluate(s, SolverOptions.Default, CancellationToken.None);
        System.Console.WriteLine($"Tarot Evaluate 80 tiles: {watch.ElapsedMilliseconds} ms");
        Check.That(watch.ElapsedMilliseconds < 20000, "under 20 s");
        Check.That(r.Length > 1, "ranked");
    }
}
