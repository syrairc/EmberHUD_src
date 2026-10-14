namespace EmberHUD.Api.Tests;

public sealed class GameUiTests
{
    static readonly GamePanel Bag = new("PCBag_C", PanelKind.Inventory, new(2400, 200), new(3300, 1200), false) { Address = 0x1000 };
    static readonly GamePanel Map = new("BigMap_C", PanelKind.WorldMap, new(0, 0), new(3440, 1440), true) { Address = 0x2000 };

    [Fact]
    public void EmptyHasNothingOpen()
    {
        Assert.Empty(GameUi.Empty.Panels);
        Assert.False(GameUi.Empty.AnyFullScreen);
        Assert.Same(GameUi.Empty, GameState.Empty.Ui);
    }

    [Fact]
    public void AnyFullScreenOnlyWhenAFullScreenPanelIsOpen()
    {
        Assert.False(new GameUi([Bag]).AnyFullScreen);
        Assert.True(new GameUi([Bag, Map]).AnyFullScreen);
    }
}
