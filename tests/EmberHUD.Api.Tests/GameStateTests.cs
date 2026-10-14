namespace EmberHUD.Api.Tests;

public class GameStateTests
{
    [Fact]
    public void RescanMinimapIsTakenOnceHoweverManyTimesItIsRequested()
    {
        Game.TakeMinimapRescan();
        Assert.False(Game.TakeMinimapRescan());
        Game.RescanMinimap();
        Game.RescanMinimap();
        Assert.True(Game.TakeMinimapRescan());
        Assert.False(Game.TakeMinimapRescan());
    }

    [Fact]
    public void EveryMemberIsSetByInitAndAreaHoldsTheMapInfo()
    {
        var area = new MapInfo("Rainforest", 78, 272, null, false, [], []);
        var state = new GameState { Status = "ready", AreaId = 7, Area = area, MinimapRotation = -90 };

        Assert.Equal(("ready", 7, (float?)-90f), (state.Status, state.AreaId, state.MinimapRotation));
        Assert.Same(area, state.Area);
        Assert.Empty(state.Entities);
        Assert.Null(state.Player);
    }

    [Fact]
    public void FlatDistanceIgnoresHeight()
    {
        var player = new Player(new Vector3(0, 0, 500), Vector2.UnitX);

        Assert.Equal(5f, player.FlatDistance(new Vector3(3, 4, -20)));
        Assert.Equal(0f, player.FlatDistance(new Vector3(0, 0, 9)));
    }
}
