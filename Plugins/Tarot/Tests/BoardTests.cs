using EmberHUD;
using EmberHUD.Testing;

namespace Tarot;

public sealed class BoardTests
{
    static TarotState State(int tiles, int position) =>
        new(TarotPhase.Board, 1, 0, 10, position, new TarotTile[tiles], [], [], 0);

    [Test]
    public void StepsCoverEveryMoveCard()
    {
        Check.Equal(2, Board.Steps(2));
        Check.Equal(-6, Board.Steps(16));
        Check.Equal(3, Board.Steps(103));
        Check.Equal(6, Board.Steps(1009));
        Check.That(Board.Steps(24) is null, "pick card has no steps");
    }

    [Test]
    public void KindOrder()
    {
        Check.Equal(TileKind.Chest, Board.Kind(new TarotTile(0, 2010, 3)));
        Check.Equal(TileKind.ChestUp, Board.Kind(new TarotTile(0, 9217, 0)));
        Check.Equal(TileKind.Trap, Board.Kind(new TarotTile(0, 1000, 0)));
        Check.Equal(TileKind.Heal, Board.Kind(new TarotTile(0, 9205, 0)));
        Check.Equal(TileKind.Other, Board.Kind(new TarotTile(0, 4001, 0)));
    }

    [Test]
    public void LandingClamps()
    {
        Check.Equal(4, Board.Landing(State(5, 3), new TarotCard(1, 6)));
        Check.Equal(0, Board.Landing(State(5, 1), new TarotCard(1, 16)));
        Check.That(Board.Landing(State(5, 1), new TarotCard(1, 24)) is null, "pick card has no landing");
    }

    [Test]
    public void LandingWithoutTilesIsNull() => Check.That(Board.Landing(State(0, 0), new TarotCard(1, 2)) is null, "no tiles no landing");

    [Test]
    public void LabelsReadLikeTheGame()
    {
        Check.Equal("Retreat 6", Board.CardLabel(16));
        Check.Equal("Life +2", Board.CardLabel(32));
        Check.Equal("Card 201", Board.CardLabel(201));
    }
}
