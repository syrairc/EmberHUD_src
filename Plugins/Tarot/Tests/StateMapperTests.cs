using System.Linq;
using EmberHUD;
using EmberHUD.Testing;

namespace Tarot;

public sealed class StateMapperTests
{
    static TarotState Board(int tiles = 40, int position = 3, params int[] hand) => new(TarotPhase.Board, 1, 0, 5, position,
        [.. Enumerable.Range(0, tiles).Select(i => new TarotTile(i, i % 7 == 2 ? 1000 : i % 5 == 4 ? 2010 : 10000, i % 5 == 4 ? 2 + i % 3 : 0))],
        [.. hand.Select((cfg, i) => new TarotCard(i + 1, cfg))], [2, 4], 0);

    [Test]
    public void MapsFakeBoard()
    {
        var state = Board(40, 3, 1, 24, 3);
        var sim = StateMapper.ToSim(state)!;
        Check.Equal(3, sim.Position);
        Check.Equal(5, sim.Hp);
        Check.Equal(40, sim.TileCfg.Length);
        Check.Equal(40, sim.TileChest.Length);
        Check.That(sim.TileCfg[2] == 1000 && sim.TileCfg[4] == 2010 && sim.TileChest[4] == 3, "tiles copied");
        Check.That(sim.Hand.SequenceEqual([1, 24, 3]), "hand cfgs in order");
        Check.That(sim.Taken.SequenceEqual([2, 4]), "taken copied");
        sim.Taken.Add(7);
        Check.Equal(2, state.Taken.Length);
    }

    [Test]
    public void NonBoardPhaseIsNull()
    {
        Check.That(StateMapper.ToSim(Board() with { Phase = TarotPhase.Collect }) is null, "collect");
        Check.That(StateMapper.ToSim(Board() with { Phase = TarotPhase.Final }) is null, "final");
        Check.That(StateMapper.ToSim(Board(0)) is null, "no tiles");
    }

    [Test]
    public void MissingTileIndexBecomesEmpty()
    {
        var state = Board(4) with { Tiles = [new(0, 10000, 0), new(1, 1000, 0), new(3, 2010, 4), new(9, 1000, 0), new(-1, 1000, 0)] };
        var sim = StateMapper.ToSim(state)!;
        Check.Equal(5, sim.TileCfg.Length);
        Check.That(sim.TileCfg[2] == 10000 && sim.TileChest[2] == 0, "gap is empty");
        Check.That(sim.TileCfg[1] == 1000 && sim.TileChest[3] == 4, "keyed by index");
        Check.That(sim.TileCfg.Count(c => c == 1000) == 1, "out of range ignored");
    }

    [Test]
    public void PendingPickGivesOptionMoves()
    {
        var state = Board(40, 3, 1, 22) with { Playing = 22 };
        var sim = StateMapper.ToSim(state)!;
        var moves = StateMapper.Pending(state, sim)!;
        Check.That(moves.Select(m => m.Steps).OrderBy(x => x).SequenceEqual([2, 4, 6]), "options");
        Check.That(moves.All(m => m.HandIndex == 1 && !m.End), "points at the card in hand");
        Check.That(StateMapper.Pending(Board() , StateMapper.ToSim(Board())!) is null, "nothing playing");
    }

    [Test]
    public void PendingCardMissingFromHandIsAddedToTheSim()
    {
        var state = Board(40, 3, 1) with { Playing = 24 };
        var sim = StateMapper.ToSim(state)!;
        var moves = StateMapper.Pending(state, sim)!;
        Check.Equal(6, moves.Count);
        Check.That(moves.All(m => m.HandIndex >= 0 && sim.Hand[m.HandIndex] == 24), "card appended");
    }

    [Test]
    public void SignatureChangesOnPlay()
    {
        var state = Board(40, 3, 1, 2);
        var sig = StateMapper.Signature(state);
        Check.Equal(sig, StateMapper.Signature(Board(40, 3, 1, 2)));
        Check.That(sig != StateMapper.Signature(state with { Position = 4 }), "position");
        Check.That(sig != StateMapper.Signature(state with { Hp = 4 }), "hp");
        Check.That(sig != StateMapper.Signature(state with { Phase = TarotPhase.Final }), "phase");
        Check.That(sig != StateMapper.Signature(state with { Hand = state.Hand[..1] }), "hand");
        Check.That(sig != StateMapper.Signature(state with { Taken = [2] }), "taken");
        Check.That(sig != StateMapper.Signature(state with { Playing = 2 }), "playing");
        var tiles = (TarotTile[])state.Tiles.Clone();
        tiles[10] = tiles[10] with { Chest = 9 };
        Check.That(sig != StateMapper.Signature(state with { Tiles = tiles }), "tile chest");
        tiles[10] = tiles[10] with { CfgId = 1000 };
        Check.That(sig != StateMapper.Signature(state with { Tiles = tiles }), "tile cfg");
    }
}
