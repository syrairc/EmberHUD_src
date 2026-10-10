namespace TorchlightSDK.Api.Tests;

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
    public void MapIconsStartEmptyAndSurviveWith()
    {
        Assert.Empty(GameState.Empty.MapIcons);
        var icon = new MapIcon(10, "UI_MiniMap_Portal", new Vector2(1, 2), 1f, 0f, 7);
        var state = GameState.Empty with { MapIcons = [icon] };
        Assert.Equal(icon, Assert.Single(state.MapIcons));
        Assert.Empty(GameState.Empty.MapIcons);
        Assert.Equal("starting", state.Status);
    }
}
