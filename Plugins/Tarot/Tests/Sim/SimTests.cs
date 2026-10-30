using System.Linq;
using EmberHUD;
using EmberHUD.Testing;

namespace Tarot;

public sealed class SimTests
{
    static SimState Board(int length = 20, int position = 0, int hp = 3, params int[] hand)
    {
        var state = new SimState { Position = position, Hp = hp, TileCfg = [.. Enumerable.Repeat(10000, length)], TileChest = new int[length] };
        state.Hand.AddRange(hand);
        return state;
    }

    static void Play(SimState state, int handIndex, int steps = 0) => Sim.Play(state, new Move(handIndex, steps, false), new Rng(1));

    [Test]
    public void RngIsDeterministic()
    {
        var a = new Rng(42);
        var b = new Rng(42);
        for (var i = 0; i < 20; i++)
        {
            var next = a.Next(10);
            Check.Equal(next, b.Next(10));
            Check.That(next is >= 0 and < 10, "in range");
        }
        var d = new Rng(7).NextDouble();
        Check.That(d is >= 0 and < 1, "double in range");
    }

    [Test]
    public void AdvanceLandsOnTile()
    {
        var state = Board(hand: 2);
        Play(state, 0);
        Check.Equal(2, state.Position);
        Check.Equal(0, state.Hand.Count);
    }

    [Test]
    public void ClampsAtBoardEnd()
    {
        var state = Board(10, 7, hand: 6);
        Play(state, 0);
        Check.Equal(9, state.Position);
        Check.That(state.Ended, "last tile ends");
    }

    [Test]
    public void TrapCostsLifeAndKills()
    {
        var state = Board(hp: 1, hand: 1);
        state.TileCfg[1] = 1000;
        Play(state, 0);
        Check.Equal(0, state.Hp);
        Check.That(state.Dead && state.Ended, "dead");
        Check.Equal(10000, state.TileCfg[1]);
    }

    [Test]
    public void HealClampsToMax()
    {
        var state = Board(hp: 5, hand: 1);
        state.TileCfg[1] = 1053;
        Play(state, 0);
        Check.Equal(6, state.Hp);
    }

    [Test]
    public void ChestTakenAndTileEmptied()
    {
        var state = Board(hand: 1);
        state.TileCfg[1] = 2010;
        state.TileChest[1] = 3;
        Play(state, 0);
        Check.Equal(1, state.Taken.Count);
        Check.Equal(3, state.Taken[0]);
        Check.Equal(10000, state.TileCfg[1]);
        Check.Equal(0, state.TileChest[1]);
    }

    [Test]
    public void ChestSlotsFull()
    {
        var state = Board(hand: 1);
        state.Taken.AddRange(Enumerable.Repeat(2, 7));
        state.TileCfg[1] = 2010;
        state.TileChest[1] = 3;
        Play(state, 0);
        Check.Equal(7, state.Taken.Count);
        Check.Equal(3, state.TileChest[1]);
        Check.Equal(2010, state.TileCfg[1]);
    }

    [Test]
    public void FallbackReplacesItself()
    {
        var state = Board(hand: 101);
        Play(state, 0);
        Check.Equal(1, state.Hand.Count);
        Check.That(Rules.IsFallback(state.Hand[0]), "fallback card");
        Check.Equal(1, state.Position);
    }

    [Test]
    public void NormalCardNotReplaced()
    {
        var state = Board(hand: [3, 4]);
        Play(state, 0);
        Check.Equal(1, state.Hand.Count);
        Check.Equal(4, state.Hand[0]);
    }

    [Test]
    public void HandCapDropsExtraCards()
    {
        var state = Board(hand: [.. Enumerable.Repeat(1, 11)]);
        state.TileCfg[1] = 1023;
        Play(state, 0);
        Check.Equal(11, state.Hand.Count);
    }

    [Test]
    public void UpgradeCardRaisesHighestAllowed()
    {
        var state = Board(hand: 201);
        state.Taken.AddRange([2, 3, 4]);
        Play(state, 0);
        Check.Equal(3, state.Taken.Count);
        Check.Equal(2, state.Taken[0]);
        Check.Equal(4, state.Taken[1]);
        Check.Equal(4, state.Taken[2]);
        Check.Equal(0, state.Position);
    }

    [Test]
    public void Chests9217BumpsAheadOnly()
    {
        var state = Board(hand: 3, position: 0);
        state.TileCfg[3] = 9217;
        state.TileChest[2] = 2;
        state.TileChest[5] = 2;
        state.TileChest[9] = 4;
        Play(state, 0);
        Check.Equal(3, state.Position);
        Check.Equal(2, state.TileChest[2]);
        Check.Equal(3, state.TileChest[5]);
        Check.Equal(5, state.TileChest[9]);
    }

    [Test]
    public void TrapsRemoved9221()
    {
        var state = Board(hand: 2);
        state.TileCfg[2] = 9221;
        state.TileCfg[5] = 1000;
        state.TileCfg[9] = 1000;
        Play(state, 0);
        Check.Equal(10000, state.TileCfg[5]);
        Check.Equal(10000, state.TileCfg[9]);
    }

    [Test]
    public void PlayDoesNotMutateClone()
    {
        var state = Board(hand: [2, 3]);
        state.Taken.Add(2);
        var copy = state.Clone();
        Play(copy, 0);
        Check.Equal(0, state.Position);
        Check.Equal(2, state.Hand.Count);
        Check.Equal(2, copy.Position);
        state.TileCfg[5] = 1000;
        Check.Equal(10000, copy.TileCfg[5]);
    }

    [Test]
    public void UnknownCardIsNoOp()
    {
        var state = Board(hand: 301);
        Play(state, 0);
        Check.Equal(0, state.Position);
        Check.Equal(3, state.Hp);
        Check.Equal(0, state.Hand.Count);
    }

    [Test]
    public void LegalListsPickOptions()
    {
        var state = Board(hand: 24);
        var moves = Sim.Legal(state).ToArray();
        Check.Equal(6, moves.Count(m => !m.End));
        Check.Equal(1, moves.Count(m => m.End));
        var empty = Sim.Legal(Board()).ToArray();
        Check.Equal(1, empty.Length);
        Check.That(empty[0] == Move.EndRun, "end run only");
    }

    [Test]
    public void PickMoveUsesChosenSteps()
    {
        var state = Board(hand: 24);
        Play(state, 0, 4);
        Check.Equal(4, state.Position);
    }

    [Test]
    public void ChestsLeftAheadCountsOnlyAhead()
    {
        var state = Board(position: 4);
        state.TileChest[2] = 1;
        state.TileChest[6] = 1;
        state.TileChest[9] = 1;
        Check.Equal(2, Sim.ChestsLeftAhead(state));
    }
}
