using System.Diagnostics;
using EmberHUD.Testing;

namespace EmberHUD.Api.Tests;

public class GameVoraxTests
{
    [Fact]
    public void VoraxIsNullByDefault()
    {
        Assert.Null(GameState.Empty.Vorax);
    }

    [Fact]
    public void WantVoraxOpensTheGate()
    {
        Game.VoraxAsked = 0;
        var now = Stopwatch.GetTimestamp();
        Assert.False(Game.VoraxWanted(now, 3));
        Game.WantVorax();
        Assert.True(Game.VoraxWanted(Stopwatch.GetTimestamp(), 3));
        Assert.False(Game.VoraxWanted(Game.VoraxAsked + 4 * Stopwatch.Frequency, 3));
    }

    [Fact]
    public void VoraxAskedDuringSeesTheCall()
    {
        Assert.True(TestGame.VoraxAskedDuring(() => Game.WantVorax()));
        Assert.False(TestGame.VoraxAskedDuring(() => { }));
    }

    [Fact]
    public void TotalIsCountTimesActivity()
    {
        Assert.Equal(120, new VoraxMonster(0, 14, 202, 4, 24, 5).Total);
    }

    [Fact]
    public void CardRectIsUnknownByDefault()
    {
        Assert.False(new VoraxCard(0, 1, VoraxCardKind.Potion, 1).HasRect);
    }

    [Fact]
    public void CardRectKnownWhenSized()
    {
        Assert.True(new VoraxCard(0, 1, VoraxCardKind.Potion, 1) { Min = new(10, 20), Max = new(110, 220) }.HasRect);
    }

    [Fact]
    public void MonsterRectKnownWhenSized()
    {
        Assert.True(new VoraxMonster(0, 14, 202, 4, 24, 5) { Min = new(10, 20), Max = new(110, 220) }.HasRect);
    }

    [Fact]
    public void SelectionDefaultsMeanNothingSelected()
    {
        var state = new VoraxState(VoraxPhase.Round, 1, 5, 0, 0, 0, 0, 0, 0, [], [], [], []);
        Assert.Equal(-1, state.SelectedCard);
        Assert.Empty(state.SelectedMonsters);
    }
}
