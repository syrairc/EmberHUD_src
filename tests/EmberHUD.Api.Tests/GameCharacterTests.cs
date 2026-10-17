using System.Diagnostics;

namespace EmberHUD.Api.Tests;

public class GameCharacterTests
{
    [Fact]
    public void BuffsAreNullByDefault() => Assert.Null(GameState.Empty.Buffs);

    [Fact]
    public void CharacterIsWantedForAWhileAfterAsking()
    {
        Game.WantCharacter();
        var now = Stopwatch.GetTimestamp();
        Assert.True(Game.CharacterWanted(now, 3));
        Assert.False(Game.CharacterWanted(now + 4 * Stopwatch.Frequency, 3));
    }
}
