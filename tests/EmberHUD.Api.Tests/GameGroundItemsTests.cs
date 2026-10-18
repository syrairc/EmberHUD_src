using System.Diagnostics;

namespace EmberHUD.Api.Tests;

public class GameGroundItemsTests
{
    [Fact]
    public void GroundItemsIsNullByDefault()
    {
        Assert.Null(GameState.Empty.GroundItems);
    }

    [Fact]
    public void GroundItemsIsWantedForAWhileAfterAsking()
    {
        Game.WantGroundItems();
        var now = Stopwatch.GetTimestamp();
        Assert.True(Game.GroundItemsWanted(now, 3));
        Assert.False(Game.GroundItemsWanted(now + 4 * Stopwatch.Frequency, 3));
    }

    [Fact]
    public void TierIsTheLastTwoDigits()
    {
        Assert.Equal(8, GroundItem.Tier(140060408));
        Assert.Equal(7, GroundItem.Tier(106060407));
    }
}
