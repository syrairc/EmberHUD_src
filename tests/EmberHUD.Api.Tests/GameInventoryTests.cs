using System.Diagnostics;

namespace EmberHUD.Api.Tests;

public class GameInventoryTests
{
    [Fact]
    public void InventoryIsNullByDefault()
    {
        Assert.Null(GameState.Empty.Inventory);
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
