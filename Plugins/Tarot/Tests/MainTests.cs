using System.Linq;
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
    public void RenderHiddenWhenPanelOff() => Check.Equal(0, Frame(Board(), new Settings { ShowPanel = false }));

    [Test]
    public void TheSettingsPageDraws() => Check.That(TestFrame.Vertices(new Settings().Draw, inWindow: true) > 0, "settings drew");
}
