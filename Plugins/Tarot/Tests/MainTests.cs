using System;
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

    static ActionResult Result(int hand, int steps, double mean, double dead = 0) => new(new Move(hand, steps, false), mean, dead, 0, 10);

    static readonly ActionResult EndFirst = new(Move.EndRun, 20, 0, 0, 1);

    [Test]
    public void RankPicksSolverBestCard()
    {
        var (best, endRun) = Advice.Rank(Board(), [Result(1, 3, 14), Result(0, 2, 10), EndFirst with { Mean = 5 }]);
        Check.That(best.SequenceEqual([1]) && !endRun, "card 1");
    }

    [Test]
    public void RankKeepsTiedCards()
    {
        var (best, _) = Advice.Rank(Board(), [Result(1, 3, 14), Result(1, 5, 14), Result(0, 2, 14), Result(0, 1, 3)]);
        Check.That(best.SequenceEqual([1, 0]), "both tied cards once");
    }

    [Test]
    public void EndRunBestFlag()
    {
        var (best, endRun) = Advice.Rank(Board(), [EndFirst, Result(0, 2, 10)]);
        Check.That(best.Length == 0 && endRun, "end run first");
    }

    [Test]
    public void RowTextFormat()
    {
        Check.Equal("14.2  dead 8%", Advice.RowText(Result(0, 2, 14.23, .08)));
        Check.Equal("0.0  dead 0%", Advice.RowText(Result(0, 2, 0)));
    }

    [Test]
    public void BestForTakesTheTopResultOfThatCard()
    {
        ActionResult[] results = [Result(1, 3, 14), Result(0, 2, 12), Result(0, 1, 9), EndFirst];
        Check.Equal(12.0, Advice.BestFor(results, 0)!.Value.Mean);
        Check.That(Advice.BestFor(results, 5) is null, "unknown card");
    }

    [Test]
    public void StaleResultIgnored()
    {
        var state = Board();
        ActionResult[] results = [Result(0, 2, 10)];
        var signature = StateMapper.Signature(state);
        Check.That(Advice.Usable(state, signature, (signature, results)) == results, "matching");
        Check.That(Advice.Usable(state, signature, ("old", results)) is null, "stale");
        Check.That(Advice.Usable(state, signature, null) is null, "none");
        Check.That(Advice.Usable(state, signature, (signature, [])) is null, "empty");
        Check.That(Advice.Usable(state with { Phase = TarotPhase.Final }, signature, (signature, results)) is null, "not board");
        Check.That(Advice.Arrows(state, null).Select(a => a.Index).SequenceEqual(Tarot.Board.BestCards(state)), "greedy fallback");
    }

    [Test]
    public void ArrowsFollowTheSolver()
    {
        var state = Board();
        var arrows = Advice.Arrows(state, [Result(1, 3, 14), Result(0, 2, 10)]);
        Check.That(arrows.Length == 1 && arrows[0].Index == 1 && arrows[0].Good, "solver card");
    }

    [Test]
    public void PendingPickResolvesThroughTheOptions()
    {
        var state = Board() with { Playing = 22 };
        var offscreen = state.Hand.Length;
        Check.Equal(1, Advice.Pick(state, [Result(offscreen, 4, 14), Result(offscreen, 2, 10)]).Index);
        Check.Equal(2, Advice.Pick(state, [EndFirst, Result(offscreen, 6, 9)]).Index);
        Check.Equal(Array.IndexOf(Tarot.Board.Options(22), Tarot.Board.BestMove(state, new TarotCard(0, 22))!.Value.Steps), Advice.Pick(state, null).Index);
    }

    [Test]
    public void TickRequestsOnlyWithTheSolverOn()
    {
        TestGame.Set(new GameState { Tarot = Board() });
        try
        {
            var off = new Main();
            var on = new Main();
            try
            {
                typeof(Plugin<Settings>).GetProperty("Settings")!.GetSetMethod(true)!.Invoke(off, [new Settings { UseSolver = false }]);
                off.Tick();
                Check.Equal(0, off.Worker.Jobs);
                on.Tick();
                on.Tick();
                Check.Equal(1, on.Worker.Jobs);
            }
            finally { off.Dispose(); on.Dispose(); }
        }
        finally { TestGame.Set(GameState.Empty); }
    }

    [Test]
    public void DisposeStopsTheWorker()
    {
        TestGame.Set(new GameState { Tarot = Board() });
        try
        {
            var plugin = new Main();
            plugin.Tick();
            plugin.Dispose();
            Check.That(!plugin.Worker.Running, "thread stopped");
        }
        finally { TestGame.Set(GameState.Empty); }
    }

    [Test]
    public void LastPlayedCardWithoutPopupSolvesTheWholeHand()
    {
        TestGame.Set(new GameState { Tarot = Board() with { Hand = [new(1, 101), new(2, 2), new(3, 24)], Playing = 101 } });
        var plugin = new Main();
        try
        {
            plugin.Tick();
            for (var i = 0; i < 100 && plugin.Worker.Latest is null; i++) System.Threading.Thread.Sleep(50);
            var results = plugin.Worker.Latest!.Value.Results;
            Check.That(results.Where(r => !r.Move.End).Select(r => r.Move.HandIndex).Distinct().Count() == 3, "every card ranked");
        }
        finally
        {
            plugin.Dispose();
            TestGame.Set(GameState.Empty);
        }
    }

    [Test]
    public void RowColourFollowsTheSolver()
    {
        Check.That(Advice.RowColour(false, false, GameColors.Negative) == GameColors.Negative, "greedy colour kept without solver text");
        Check.That(Advice.RowColour(true, false, GameColors.Negative) == GameColors.Text, "neutral");
        Check.That(Advice.RowColour(true, true, GameColors.Negative) == GameColors.Positive, "best");
    }
}
