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

    [Fact]
    public void ScreenRectSizeIsMaxMinusMin()
    {
        var rect = new ScreenRect(new Vector2(10, 20), new Vector2(40, 26));

        Assert.Equal((30f, 6f), (rect.Width, rect.Height));
    }

    [Fact]
    public void EntityHasNoGameBarByDefault()
    {
        var entity = new Entity(1, 1000, Vector3.Zero, EntityKind.Monster, Rarity.Normal);

        Assert.Null(entity.GameBar);
        Assert.False(entity.HasGameBar);
    }

    [Fact]
    public void WithGameBarKeepsTheOtherFields()
    {
        var entity = new Entity(1, 1000, new Vector3(3, 4, 5), EntityKind.Monster, Rarity.Rare) { Name = "Mob", Life = 50 };
        var bar = new ScreenRect(new Vector2(600, 300), new Vector2(680, 306));

        var barred = entity with { GameBar = bar, HasGameBar = true };

        Assert.Equal((bar, true), (barred.GameBar, barred.HasGameBar));
        Assert.Equal((entity.Id, entity.CfgId, entity.Position, entity.Kind, entity.Rarity, entity.Name, entity.Life), (barred.Id, barred.CfgId, barred.Position, barred.Kind, barred.Rarity, barred.Name, barred.Life));
    }
}
