using System;
using System.Linq;
using EmberHUD;
using EmberHUD.Testing;

namespace Tarot;

public sealed class PolicyTests
{
    static SimState Board(int hp = 4, int position = 0, params int[] hand)
    {
        var state = new SimState { Position = position, Hp = hp, TileCfg = [.. Enumerable.Repeat(10000, 100)], TileChest = new int[100] };
        state.Hand.AddRange(hand);
        return state;
    }

    static SimState Pressured(int rarity)
    {
        var state = Board();
        state.Taken.Add(2);
        for (var i = 1; i <= 9; i++) { state.TileCfg[i] = 2010; state.TileChest[i] = 2; }
        state.TileChest[1] = rarity;
        return state;
    }

    static void Near(double expected, double actual, string label) =>
        Check.That(Math.Abs(expected - actual) < 0.001, $"{label}: expected {expected} got {actual}");

    [Test]
    public void ChestGoldensUnderSlotPressure()
    {
        double[] golden = [-0.3511, 1.8334, 2.4594, 5.9762, 7.0609, 12.0431];
        for (var r = 2; r <= 7; r++) Near(golden[r - 2], Policy.TileScore(Pressured(r), 1, Weights.Rarity), $"rarity {r}");
    }

    [Test]
    public void NoPressureNoPenalty()
    {
        foreach (var (rarity, expected) in new[] { (2, 1.6969), (3, 3.8814) })
        {
            var state = Board();
            for (var i = 1; i <= 3; i++) { state.TileCfg[i] = 2010; state.TileChest[i] = rarity; }
            Near(expected, Policy.TileScore(state, 1, Weights.Rarity), $"rarity {rarity}");
        }
    }

    [Test]
    public void FreeMoveAddsRefillBias()
    {
        var state = Pressured(2);
        state.Hand.AddRange([1, 101]);
        var tile = Policy.TileScore(state, 1, Weights.Rarity);
        Near(tile, Policy.ActionScore(state, new Move(0, 1, false), Weights.Rarity), "hand move");
        Near(tile + 2.048, Policy.ActionScore(state, new Move(1, 1, false), Weights.Rarity), "fallback");
    }

    [Test]
    public void BackMovePaysBackCost()
    {
        var state = Board(position: 5, hand: 11);
        var empty = Policy.TileScore(state, 4, Weights.Rarity);
        Near(empty - 0.003, Policy.ActionScore(state, new Move(0, -1, false), Weights.Rarity), "back");
    }

    [Test]
    public void TrapScores()
    {
        var state = Board(hp: 4);
        state.TileCfg[1] = 1000;
        Near(-0.443, Policy.TileScore(state, 1, Weights.Rarity), "hp 4");
        state.Hp = 2;
        Near(-36.283, Policy.TileScore(state, 1, Weights.Rarity), "hp 2");
        state.Hp = 1;
        state.Taken.AddRange([2, 2, 2]);
        Near(-36.283 - 24, Policy.TileScore(state, 1, Weights.Rarity), "hp 1");
    }

    [Test]
    public void HealScores()
    {
        var state = Board(hp: 4);
        state.TileCfg[1] = 1052;
        Near(0.2216, Policy.TileScore(state, 1, Weights.Rarity), "hp 4");
        state.Hp = 2;
        Near(0.4803, Policy.TileScore(state, 1, Weights.Rarity), "hp 2");
    }

    [Test]
    public void FullSlotsChestIsEmpty()
    {
        var state = Pressured(5);
        state.Taken.AddRange(Enumerable.Repeat(2, 6));
        Near(Weights.Rarity.Empty, Policy.TileScore(state, 1, Weights.Rarity), "full");
    }

    [Test]
    public void UpgradeGating()
    {
        var w = Weights.Rarity;
        var nothing = Board(hand: 201);
        Check.Equal(-1, Policy.UpgradeToPlay(nothing, w));
        var blue = Board(hand: 201);
        blue.Taken.Add(2);
        Check.Equal(0, Policy.UpgradeToPlay(blue, w));
        var orangeWaits = Board(hand: 213);
        orangeWaits.Taken.AddRange([2, 3, 4]);
        Check.Equal(-1, Policy.UpgradeToPlay(orangeWaits, w));
        orangeWaits.Taken.Add(5);
        Check.Equal(0, Policy.UpgradeToPlay(orangeWaits, w));
        var red = Board(hp: 3, position: 20, hand: 224);
        red.Taken.AddRange([2, 6]);
        Check.Equal(-1, Policy.UpgradeToPlay(red, w));
        red.Hp = 1;
        Check.Equal(0, Policy.UpgradeToPlay(red, w));
        red.Hp = 3;
        red.Position = 78;
        Check.Equal(0, Policy.UpgradeToPlay(red, w));
    }

    [Test]
    public void ChooseAvoidsTrapWhenChestOnOtherCard()
    {
        var state = Board(hp: 3, hand: [1, 2]);
        state.TileCfg[1] = 1000;
        state.TileCfg[2] = 2010;
        state.TileChest[2] = 4;
        var move = Policy.Choose(state, Weights.Rarity, new Rng(1));
        Check.Equal(1, move.HandIndex);
    }

    [Test]
    public void ChoosePlaysReadyUpgrade()
    {
        var state = Board(hand: [1, 201]);
        state.Taken.Add(2);
        Check.Equal(1, Policy.Choose(state, Weights.Rarity, new Rng(1)).HandIndex);
    }

    [Test]
    public void ChooseIsDeterministicPerSeed()
    {
        SimState Tied() => Board(hand: 24);
        var a = Policy.Choose(Tied(), Weights.Rarity, new Rng(9));
        var b = Policy.Choose(Tied(), Weights.Rarity, new Rng(9));
        Check.That(a == b, "same seed same move");
        Check.That(!a.End, "plays a card");
    }

    [Test]
    public void ChooseEndsWhenNothingLegal()
    {
        Check.That(Policy.Choose(Board(), Weights.Rarity, new Rng(1)).End, "end run");
    }
}
