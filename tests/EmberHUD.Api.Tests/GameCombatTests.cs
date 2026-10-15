using System.Diagnostics;

namespace EmberHUD.Api.Tests;

public class GameCombatTests
{
    [Fact]
    public void CombatSecondsConvertsTheTimestamp()
    {
        var stats = new CombatStats(Stopwatch.Frequency * 3, 10, 12, 2);
        Assert.Equal(3.0, stats.Seconds);
    }

    [Fact]
    public void CombatIsNullByDefault()
    {
        Assert.Null(GameState.Empty.Combat);
    }

    [Fact]
    public void CombatIsWantedForAWhileAfterAsking()
    {
        Game.WantCombat();
        var now = Stopwatch.GetTimestamp();
        Assert.True(Game.CombatWanted(now, 3));
        Assert.False(Game.CombatWanted(now + 4 * Stopwatch.Frequency, 3));
    }
}
