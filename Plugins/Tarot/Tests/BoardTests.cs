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

    static TarotState Hand(int position, int hp, params TarotCard[] hand)
    {
        var tiles = new TarotTile[10];
        for (var i = 0; i < tiles.Length; i++) tiles[i] = new TarotTile(i, 10000, 0);
        tiles[3] = new TarotTile(3, 1000, 0);
        tiles[5] = new TarotTile(5, 2010, 2);
        tiles[6] = new TarotTile(6, 2010, 4);
        tiles[8] = new TarotTile(8, 9205, 0);
        return new TarotState(TarotPhase.Board, 1, 0, hp, position, tiles, hand, [], 0);
    }

    [Test]
    public void PickCardsOfferTheirOptions()
    {
        Check.Equal(3, Board.Options(21).Length);
        Check.Equal(6, Board.Options(22)[2]);
        Check.Equal(-3, Board.Options(23)[2]);
        Check.Equal(6, Board.Options(24).Length);
        Check.Equal(2, Board.Options(2)[0]);
        Check.Equal(0, Board.Options(33).Length);
    }

    [Test]
    public void PickCardTakesTheBestOption()
    {
        var best = Board.BestMove(Hand(2, 7, new TarotCard(1, 24)), new TarotCard(1, 24))!.Value;
        Check.Equal(4, best.Steps);
        Check.Equal(6, best.Landing);
    }

    [Test]
    public void PickCardAvoidsTheTrapWhenItCan()
    {
        var best = Board.BestMove(Hand(2, 7, new TarotCard(1, 21)), new TarotCard(1, 21))!.Value;
        Check.Equal(3, best.Steps);
    }

    [Test]
    public void PickCardTieTakesTheFarthestAdvance()
    {
        var best = Board.BestMove(Hand(6, 7, new TarotCard(1, 21)), new TarotCard(1, 21))!.Value;
        Check.Equal(5, best.Steps);
        Check.Equal(0, best.Score);
    }

    [Test]
    public void BestCardsKeepsEveryTiedCardWhenTheLandingIsGood()
    {
        var state = Hand(2, 7, new TarotCard(1, 4), new TarotCard(2, 1), new TarotCard(3, 4));
        Check.Equal(2, Board.BestCards(state).Length);
        Check.Equal(2, Board.BestCards(state)[1]);
    }

    [Test]
    public void BestCardsIsOneCardWhenNothingIsWorthPlaying()
    {
        var state = Hand(0, 7, new TarotCard(1, 1), new TarotCard(2, 1));
        Check.Equal(1, Board.BestCards(state).Length);
        Check.Equal(0, Board.BestCards(Hand(2, 7)).Length);
    }

    [Test]
    public void ShownCardsSkipTheCardSetAside()
    {
        TarotCard[] hand = [new(1, 203), new(5, 3), new(21, 105), new(22, 201)];
        Check.Equal(3, Board.ShownCards(hand, 3)!.Length);
        Check.Equal(3, Board.ShownCards(hand, 3)![2]);
        Check.Equal(0, Board.ShownCards(hand, 4)![0]);
        Check.That(Board.ShownCards(hand, 2) is null, "two off is ambiguous");
        Check.That(Board.ShownCards([new(1, 203), new(2, 3)], 1) is null, "no card set aside");
    }

    [Test]
    public void BestCardIsTheHighestLanding()
    {
        var state = Hand(2, 7, new TarotCard(1, 1), new TarotCard(2, 4), new TarotCard(3, 2));
        Check.Equal(1, Board.BestCard(state));
    }

    [Test]
    public void HealWinsWhenHpIsLow()
    {
        var state = Hand(2, 2, new TarotCard(1, 3), new TarotCard(2, 6));
        Check.Equal(1, Board.BestCard(state));
        Check.Equal(0, Board.BestCard(Hand(2, 7, new TarotCard(1, 3), new TarotCard(2, 6))));
    }

    [Test]
    public void NoHandNoBestCard() => Check.Equal(-1, Board.BestCard(Hand(2, 7)));

    [Test]
    public void LabelsReadLikeTheGame()
    {
        Check.Equal("Pick 1/3/5 ahead", Board.CardLabel(21));
        Check.Equal("Pick 1/2/3 back", Board.CardLabel(23));
        Check.Equal("Retreat 6", Board.CardLabel(16));
        Check.Equal("Life +2", Board.CardLabel(32));
        Check.Equal("Card 201", Board.CardLabel(201));
    }
}
