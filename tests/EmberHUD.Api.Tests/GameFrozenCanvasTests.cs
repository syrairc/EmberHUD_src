using System.Diagnostics;
using EmberHUD.Testing;

namespace EmberHUD.Api.Tests;

public class GameFrozenCanvasTests
{
    [Fact]
    public void FrozenCanvasIsNullByDefault()
    {
        Assert.Null(GameState.Empty.FrozenCanvas);
    }

    [Fact]
    public void WantFrozenCanvasOpensTheGate()
    {
        Game.FrozenCanvasAsked = 0;
        var now = Stopwatch.GetTimestamp();
        Assert.False(Game.FrozenCanvasWanted(now, 3));
        Game.WantFrozenCanvas();
        Assert.True(Game.FrozenCanvasWanted(Stopwatch.GetTimestamp(), 3));
        Assert.False(Game.FrozenCanvasWanted(Game.FrozenCanvasAsked + 4 * Stopwatch.Frequency, 3));
    }

    [Fact]
    public void FrozenCanvasAskedDuringSeesTheCall()
    {
        Assert.True(TestGame.FrozenCanvasAskedDuring(() => Game.WantFrozenCanvas()));
        Assert.False(TestGame.FrozenCanvasAskedDuring(() => { }));
    }
}
