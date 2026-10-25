using System.Diagnostics;
using EmberHUD.Testing;

namespace EmberHUD.Api.Tests;

public class GameItemMenuTests
{
    [Fact]
    public void ItemMenuIsNullUntilAPluginAsks()
    {
        Assert.Null(GameState.Empty.ItemMenu);
        Game.ItemMenuAsked = 0;
        Assert.False(Game.ItemMenuWanted(Stopwatch.GetTimestamp(), 3));
        Game.WantItemMenu();
        Assert.True(Game.ItemMenuWanted(Stopwatch.GetTimestamp(), 3));
        Assert.False(Game.ItemMenuWanted(Game.ItemMenuAsked + 4 * Stopwatch.Frequency, 3));
        Assert.True(TestGame.ItemMenuAskedDuring(() => Game.WantItemMenu()));
        Assert.False(TestGame.ItemMenuAskedDuring(() => { }));
    }
}
