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
}
