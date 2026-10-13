namespace EmberHUD.Api.Tests;

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

    [Fact]
    public void CameraAndEntityAddressDefaultToNothingAndKeepTheirInitValues()
    {
        var entity = new Entity(1, 2, Vector3.Zero, EntityKind.Monster, Rarity.Normal);

        Assert.Null(GameState.Empty.Camera);
        Assert.Equal(0, entity.Address);
        Assert.Equal((nint)0x1000, (entity with { Address = 0x1000 }).Address);
        Assert.Equal(90, (GameState.Empty with { Camera = new Camera(Vector3.Zero, Vector3.Zero, 90) }).Camera?.Fov);
    }

    [Fact]
    public void MapStartsNullAndSurvivesWith()
    {
        Assert.Null(GameState.Empty.Map);
        var map = new MapInfo("Rainforest", 78, 272, "1%", false, [new MapContent("Vigil", "svg:quest", ContentState.Active)], ["+15% Drop Quantity"]);
        var state = GameState.Empty with { Map = map };
        Assert.Same(map, (state with { Status = "x" }).Map);
        Assert.Equal(ContentState.Active, state.Map!.Content[0].State);
    }
}
