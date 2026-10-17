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
}
