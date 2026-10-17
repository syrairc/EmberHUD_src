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
    public void InventoryIsWantedForAWhileAfterAsking()
    {
        Game.WantInventory();
        var now = Stopwatch.GetTimestamp();
        Assert.True(Game.InventoryWanted(now, 3));
        Assert.False(Game.InventoryWanted(now + 4 * Stopwatch.Frequency, 3));
    }
}
