using System.Diagnostics;
using EmberHUD.Testing;

namespace EmberHUD.Api.Tests;

public class GameTarotTests
{
    [Fact]
    public void TarotIsNullByDefault()
    {
        Assert.Null(GameState.Empty.Tarot);
    }

    [Fact]
    public void WantTarotOpensTheGate()
    {
        Game.WantTarot();
        var now = Stopwatch.GetTimestamp();
        Assert.True(Game.TarotWanted(now, 3));
        Assert.False(Game.TarotWanted(now + 4 * Stopwatch.Frequency, 3));
    }

    [Fact]
    public void TarotAskedDuringSeesTheCall()
    {
        Assert.True(TestGame.TarotAskedDuring(() => Game.WantTarot()));
        Assert.False(TestGame.TarotAskedDuring(() => { }));
    }
}
