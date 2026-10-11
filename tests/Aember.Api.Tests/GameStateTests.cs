namespace Aember.Api.Tests;

public class GameStateTests
{
    [Fact]
    public void EmptyHasNoPlayerEntitiesOrTerrain()
    {
        Assert.Equal("starting", GameState.Empty.Status);
        Assert.Equal(0, GameState.Empty.AreaId);
        Assert.Null(GameState.Empty.Player);
        Assert.Empty(GameState.Empty.Entities);
        Assert.Null(GameState.Empty.Terrain);
    }

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
    public void MapIconsStartEmptyAndSurviveWith()
    {
        Assert.Empty(GameState.Empty.MapIcons);
        var icon = new MapIcon(10, "UI_MiniMap_Portal", new Vector2(1, 2), 1f, 0f, 7);
        var state = GameState.Empty with { MapIcons = [icon] };
        Assert.Equal(icon, Assert.Single(state.MapIcons));
        Assert.Empty(GameState.Empty.MapIcons);
        Assert.Equal("starting", state.Status);
    }

    [Fact]
    public void ExploredStartsNullAndSurvivesWith()
    {
        Assert.Null(GameState.Empty.Explored);
        var trail = new ExploredTrail([new Vector2(1, 2)], 250);
        var state = GameState.Empty with { Explored = trail };
        Assert.Same(trail, state.Explored);
        Assert.Null(GameState.Empty.Explored);
        Assert.Equal("starting", state.Status);
    }
}
