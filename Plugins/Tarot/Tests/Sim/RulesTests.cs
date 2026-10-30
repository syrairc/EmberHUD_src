using System.Linq;
using EmberHUD;
using EmberHUD.Testing;

namespace Tarot;

public sealed class RulesTests
{
    [Test]
    public void CardMoveEffects()
    {
        var three = Rules.OfCard(3);
        Check.Equal(1, three.Length);
        Check.Equal(EffectKind.Move, three[0].Kind);
        Check.Equal(1, three[0].Set.Length);
        Check.Equal(3, three[0].Set[0]);
        Check.Equal(-6, Rules.OfCard(16)[0].Set[0]);
        Check.Equal(6, Rules.OfCard(60)[0].Set.Length);
        Check.Equal(0, Rules.OfCard(21).Length);
    }

    [Test]
    public void FallbackGivesCardThenMoves()
    {
        var effects = Rules.OfCard(102);
        Check.Equal(2, effects.Length);
        Check.Equal(EffectKind.GiveCards, effects[0].Kind);
        Check.Equal(6, effects[0].Set.Length);
        Check.Equal(101, effects[0].Set[0]);
        Check.Equal(106, effects[0].Set[5]);
        Check.Equal(EffectKind.Move, effects[1].Kind);
        Check.Equal(2, effects[1].Set[0]);
        Check.That(Rules.IsFallback(106) && !Rules.IsFallback(107), "fallback range");
    }

    [Test]
    public void TileEffects()
    {
        Check.Equal(EffectKind.Life, Rules.OfTile(1000)[0].Kind);
        Check.Equal(-1, Rules.OfTile(1000)[0].Min);
        Check.Equal(2, Rules.OfTile(1052)[0].Min);
        var give = Rules.OfTile(1101)[0];
        Check.Equal(EffectKind.GiveCards, give.Kind);
        Check.Equal(3, give.Set.Length);
        Check.Equal(211, give.Set[1]);
        Check.Equal(EffectKind.RarityUpAhead, Rules.OfTile(9217)[0].Kind);
        var red = Rules.OfTile(9218)[0];
        Check.Equal(EffectKind.SetAheadRarity, red.Kind);
        Check.Equal(6, red.Target);
        var rainbow = Rules.OfTile(9219)[0];
        Check.Equal(EffectKind.SetAheadRarity, rainbow.Kind);
        Check.Equal(7, rainbow.Target);
        var clear = Rules.OfTile(9221)[0];
        Check.Equal(EffectKind.TileChange, clear.Kind);
        Check.Equal(10000, clear.Target);
        Check.Equal(1000, clear.Set[0]);
        Check.Equal(2010, Rules.OfTile(9222)[0].Target);
        Check.Equal(3, Rules.OfTile(9225)[0].Max);
        Check.Equal(1, Rules.OfTile(9201)[0].Min);
        Check.Equal(6, Rules.OfTile(9201)[0].Target);
        Check.Equal(99, Rules.OfTile(9204)[0].Max);
        Check.Equal(0, Rules.OfTile(2010).Length);
        Check.Equal(0, Rules.OfTile(10000).Length);
    }

    [Test]
    public void PlaceChestCardsChangeEmptyTiles()
    {
        var put = Rules.OfCard(43)[0];
        Check.Equal(EffectKind.TileChange, put.Kind);
        Check.Equal(2003, put.Target);
        Check.Equal(10000, put.Set[0]);
        Check.Equal(8, put.RangeMax);
    }

    [Test]
    public void UpgradeCard()
    {
        Check.Equal(1, Rules.Upgrade(201).Count);
        Check.Equal(3, Rules.Upgrade(201).MaxRarity);
        Check.Equal(3, Rules.Upgrade(224).Count);
        Check.Equal(6, Rules.Upgrade(224).MaxRarity);
        Check.Equal(2, Rules.Upgrade(212).Count);
        Check.That(Rules.IsUpgradeCard(203), "203 upgrades");
        Check.That(Rules.IsUpgradeCard(55), "55 is an old upgrade note");
        Check.That(!Rules.IsUpgradeCard(3), "3 does not");
        Check.That(!Rules.IsUpgradeCard(205), "205 is no card");
        Check.Equal(EffectKind.RarityUpCollected, Rules.OfCard(203)[0].Kind);
    }

    [Test]
    public void DrawPoolFollowsTagsAndWeights()
    {
        var moves = Rules.DrawPool([1]);
        Check.Equal(6, moves.Length);
        Check.Equal(50000, Rules.DrawWeight(moves[0]));
        Check.Equal(12, Rules.DrawPool([1, 2]).Length);
        Check.Equal(12, Rules.DrawPool([6]).Length);
        Check.Equal(35000, Rules.DrawWeight(201));
        Check.Equal(0, Rules.DrawWeight(205));
        Check.Equal(0, Rules.DrawPool([5]).Length);
    }

    [Test]
    public void GiveByTagResolvesThePool()
    {
        var give = Rules.OfTile(1021)[0];
        Check.Equal(EffectKind.GiveCards, give.Kind);
        Check.Equal(16, give.Set.Length);
    }

    [Test]
    public void UnknownIsNeutral()
    {
        foreach (var effect in Rules.OfCard(999).Concat(Rules.OfTile(777)).Concat(Rules.OfCard(301)).Concat(Rules.OfCard(51)))
            Check.That(effect.Kind == EffectKind.Unknown, "unknown stays unknown");
        Check.Equal(1, Rules.OfCard(999).Length);
        Check.Equal(1, Rules.OfTile(777).Length);
        Check.Equal(EffectKind.Unknown, Rules.OfCard(301)[0].Kind);
        Check.Equal(0, Rules.Upgrade(3).Count);
    }
}
