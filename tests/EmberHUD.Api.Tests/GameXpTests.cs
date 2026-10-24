using System.Diagnostics;
using EmberHUD.Testing;

namespace EmberHUD.Api.Tests;

public class GameXpTests
{
    [Fact]
    public void XpIsNullUntilAPluginAsks()
    {
        Assert.Null(GameState.Empty.Xp);
        Game.XpAsked = 0;
        Assert.False(Game.XpWanted(Stopwatch.GetTimestamp(), 3));
        Game.WantXp();
        Assert.True(Game.XpWanted(Stopwatch.GetTimestamp(), 3));
        Assert.False(Game.XpWanted(Game.XpAsked + 4 * Stopwatch.Frequency, 3));
        Assert.True(TestGame.XpAskedDuring(() => Game.WantXp()));
        Assert.False(TestGame.XpAskedDuring(() => { }));
    }
}
