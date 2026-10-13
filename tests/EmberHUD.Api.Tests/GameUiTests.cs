namespace EmberHUD.Api.Tests;

public sealed class GameUiTests : IDisposable
{
    static readonly GamePanel Bag = new("PCBag_C", PanelKind.Inventory, new(2400, 200), new(3300, 1200), false) { Address = 0x1000 };
    static readonly GamePanel Map = new("BigMap_C", PanelKind.WorldMap, new(0, 0), new(3440, 1440), true) { Address = 0x2000 };

    public void Dispose() => GameUi.Finder = null;

    [Fact]
    public void EmptyHasNothingOpen()
    {
        Assert.Empty(GameUi.Empty.Panels);
        Assert.False(GameUi.Empty.AnyFullScreen);
        Assert.Null(GameUi.Empty.Get(PanelKind.Inventory));
        Assert.False(GameUi.Empty.Covers(new Vector2(10, 10)));
        Assert.Same(GameUi.Empty, GameState.Empty.Ui);
    }

    [Fact]
    public void AnyFullScreenOnlyWhenAFullScreenPanelIsOpen()
    {
        Assert.False(new GameUi([Bag]).AnyFullScreen);
        Assert.True(new GameUi([Bag, Map]).AnyFullScreen);
    }

    [Fact]
    public void GetReturnsTheFrontMostPanelOfThatKind()
    {
        var back = Bag with { Address = 0x3000 };
        var ui = new GameUi([back, Map, Bag]);

        Assert.Equal(Bag, ui.Get(PanelKind.Inventory));
        Assert.Null(ui.Get(PanelKind.Stash));
    }

    [Fact]
    public void CoversUsesHalfOpenRects()
    {
        var ui = new GameUi([Bag]);

        Assert.True(ui.Covers(new Vector2(2400, 200)));
        Assert.True(ui.Covers(new Vector2(3299, 1199)));
        Assert.False(ui.Covers(new Vector2(3300, 600)));
        Assert.False(ui.Covers(new Vector2(100, 100)));
        Assert.False(ui.Covers(new Vector2(float.NaN, 300)));
    }

    [Fact]
    public void FindIsNullWithoutAFinderOrAddress()
    {
        Assert.Null(GameUi.Find(Bag, "Bg"));
        GameUi.Finder = (_, _) => (Vector2.One, Vector2.One * 2);
        Assert.Null(GameUi.Find(Bag with { Address = 0 }, "Bg"));
    }

    [Fact]
    public void FindAsksTheFinderWithThePageAddressAndName()
    {
        (nint, string)? asked = null;
        GameUi.Finder = (page, name) => { asked = (page, name); return (new Vector2(1, 2), new Vector2(3, 4)); };

        Assert.Equal((new Vector2(1, 2), new Vector2(3, 4)), GameUi.Find(Bag, "Bg"));
        Assert.Equal(((nint)0x1000, "Bg"), asked);
    }
}
