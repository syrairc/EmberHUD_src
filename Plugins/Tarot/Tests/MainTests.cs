using System.Linq;
using System.Numerics;
using EmberHUD;
using EmberHUD.Testing;

namespace Tarot;

public sealed class MainTests
{
    static TarotState Board(int tiles = 6) => new(TarotPhase.Board, 2, 0, 7, 1,
        [.. Enumerable.Range(0, tiles).Select(i => new TarotTile(i, i == 2 ? 1000 : i == 3 ? 2010 : 10000, i == 3 ? 3 : 0))],
        [new(1, 2), new(2, 24)], [3], 0);

    static int Frame(TarotState? state, Settings? settings = null)
    {
        TestGame.Set(new GameState { Tarot = state });
        try
        {
            var plugin = new Main();
            if (settings is not null) typeof(Plugin<Settings>).GetProperty("Settings")!.GetSetMethod(true)!.Invoke(plugin, [settings]);
            return TestFrame.Vertices(plugin.Render);
        }
        finally { TestGame.Set(GameState.Empty); }
    }

    [Test]
    public void TickAsksForTarot()
    {
        TestGame.Set(GameState.Empty);
        Check.That(TestGame.TarotAskedDuring(new Main().Tick));
    }

    [Test]
    public void RenderDrawsNothingWithoutState() => Check.Equal(0, Frame(null));

    [Test]
    public void RenderDrawsTheBoard() => Check.That(Frame(Board()) > 0, "board drew");

    [Test]
    public void RenderDrawsFromTheMiddleWithMoreAheadThanTilesLeft() =>
        Check.That(Frame(new TarotState(TarotPhase.Board, 2, 0, 7, 9, Board(12).Tiles, [], [], 0), new Settings { AheadTiles = 80 }) > 0, "middle drew");

    [Test]
    public void RenderDrawsAnEmptyBoardWithoutThrowing() => Check.That(Frame(Board(0)) > 0, "empty board drew");

    [Test]
    public void RenderDrawsCollectAndFinal()
    {
        Check.That(Frame(new TarotState(TarotPhase.Collect, 1, 3, 10, 0, [], [], [], 0)) > 0, "collect drew");
        Check.That(Frame(new TarotState(TarotPhase.Final, 1, 0, 10, 0, [], [], [], 4)) > 0, "final drew");
    }

    [Test]
    public void RenderArrowsTheBestHandCard()
    {
        var state = Board();
        var panel = new GamePanel("S9Taro_C", PanelKind.Other, new(0, 0), new(1000, 500), false)
        {
            Parts = [new("HandCardPanel[0]", new(100, 300), new(180, 450)), new("HandCardPanel[1]", new(200, 300), new(280, 450))],
        };
        TestGame.Set(new GameState { Tarot = state, Ui = new GameUi([panel]) });
        try
        {
            var arrowed = TestFrame.Vertices(new Main().Render);
            Settings off = new() { HighlightCard = false };
            var plain = new Main();
            typeof(Plugin<Settings>).GetProperty("Settings")!.GetSetMethod(true)!.Invoke(plain, [off]);
            Check.That(arrowed > TestFrame.Vertices(plain.Render), "arrow drew");
        }
        finally { TestGame.Set(GameState.Empty); }
    }

    [Test]
    public void RenderArrowsTheBestOfferedCard()
    {
        var panel = new GamePanel("S9Taro_C", PanelKind.Other, new(0, 0), new(1000, 500), false)
        {
            Parts = [new("PopPanel/ScrollBox[0]", new(100, 100), new(200, 300)), new("PopPanel/ScrollBox[1]", new(300, 100), new(400, 300)), new("PopPanel/ScrollBox[2]", new(500, 100), new(600, 300))],
        };
        TestGame.Set(new GameState { Tarot = Board() with { Playing = 22 }, Ui = new GameUi([panel]) });
        try
        {
            var arrowed = TestFrame.Vertices(new Main().Render);
            var plain = new Main();
            typeof(Plugin<Settings>).GetProperty("Settings")!.GetSetMethod(true)!.Invoke(plain, [new Settings { HighlightCard = false }]);
            Check.That(arrowed > TestFrame.Vertices(plain.Render), "offered card arrow drew");
        }
        finally { TestGame.Set(GameState.Empty); }
    }

    [Test]
    public void RenderHiddenWhenPanelOff() => Check.Equal(0, Frame(Board(), new Settings { ShowPanel = false }));

    [Test]
    public void TheSettingsPageDraws() => Check.That(TestFrame.Vertices(new Settings().Draw, inWindow: true) > 0, "settings drew");
}
