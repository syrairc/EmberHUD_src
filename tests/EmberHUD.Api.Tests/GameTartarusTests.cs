using System.Diagnostics;

namespace EmberHUD.Api.Tests;

public class GameTartarusTests
{
    [Fact]
    public void TartarusIsNullByDefault()
    {
        Assert.Null(GameState.Empty.Tartarus);
    }

    [Fact]
    public void TartarusIsWantedForAWhileAfterAsking()
    {
        Game.WantTartarus();
        var now = Stopwatch.GetTimestamp();
        Assert.True(Game.TartarusWanted(now, 3));
        Assert.False(Game.TartarusWanted(now + 4 * Stopwatch.Frequency, 3));
    }
}
