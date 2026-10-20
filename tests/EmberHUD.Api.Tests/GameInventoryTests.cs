using System.Diagnostics;

namespace EmberHUD.Api.Tests;

public class GameInventoryTests
{
    [Fact]
    public void InventoryIsNullByDefault()
    {
        Assert.Null(GameState.Empty.Inventory);
        Assert.Null(GameState.Empty.Stash);
        Assert.Null(GameState.Empty.StashItems);
    }

    [Fact]
    public void BagIsNullByDefault()
    {
        Assert.Null(GameState.Empty.Bag);
    }

    [Fact]
    public void InventoryIsWantedForAWhileAfterAsking()
    {
        Game.WantInventory();
        var now = Stopwatch.GetTimestamp();
        Assert.True(Game.InventoryWanted(now, 3));
        Assert.False(Game.InventoryWanted(now + 4 * Stopwatch.Frequency, 3));
    }
}

public class GameTradeGoodsTests
{
    [Fact]
    public void TradeGoodsAreWantedForAWhileAfterAsking()
    {
        Game.TradeGoodsAsked = 0;
        Assert.False(Game.TradeGoodsWanted(Stopwatch.GetTimestamp(), 3));
        Game.WantTradeGoods();
        var now = Stopwatch.GetTimestamp();
        Assert.True(Game.TradeGoodsWanted(now, 3));
        Assert.False(Game.TradeGoodsWanted(now + 4 * Stopwatch.Frequency, 3));
    }
}

public class GameCastsTests
{
    [Fact]
    public void CastsAreWantedForAWhileAfterAsking()
    {
        Game.WantCasts();
        var now = Stopwatch.GetTimestamp();
        Assert.True(Game.CastsWanted(now, 3));
        Assert.False(Game.CastsWanted(now + 4 * Stopwatch.Frequency, 3));
    }
}

public class GameProjectilesTests
{
    [Fact]
    public void DefaultStateHasNoProjectiles()
    {
        Assert.Empty(GameState.Empty.Projectiles);
    }

    [Fact]
    public void WantProjectilesMarksProjectilesWanted()
    {
        Game.WantProjectiles();
        var now = Stopwatch.GetTimestamp();
        Assert.True(Game.ProjectilesWanted(now, 3));
        Assert.False(Game.ProjectilesWanted(now + 4 * Stopwatch.Frequency, 3));
    }
}

public class GameGroundHazardsTests
{
    [Fact]
    public void DefaultStateHasNoGroundHazards()
    {
        Assert.Empty(GameState.Empty.GroundHazards);
    }

    [Fact]
    public void WantGroundHazardsMarksThemWanted()
    {
        Game.GroundHazardsAsked = 0;
        Assert.False(Game.GroundHazardsWanted(Stopwatch.GetTimestamp(), 3));
        Game.WantGroundHazards();
        var now = Stopwatch.GetTimestamp();
        Assert.True(Game.GroundHazardsWanted(now, 3));
        Assert.False(Game.GroundHazardsWanted(now + 4 * Stopwatch.Frequency, 3));
    }

    [Fact]
    public void InventoryItemAffixesDefaultToEmpty()
    {
        var item = new InventoryItem(InventoryBag.Equipment, 0, "a", 1, 1, 0, 0, 1, 1, 0);
        Assert.Empty(item.PrefixIds);
        Assert.Empty(item.SuffixIds);
    }

    [Fact]
    public void InventoryItemKeepsAffixesWithInit()
    {
        var item = new InventoryItem(InventoryBag.Equipment, 0, "a", 1, 1, 0, 0, 1, 1, 0) { PrefixIds = [1, 2], SuffixIds = [3] };
        Assert.Equal([1, 2], item.PrefixIds);
        Assert.Equal([3], item.SuffixIds);
    }
}
