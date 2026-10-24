using System.Numerics;
using EmberHUD;
using EmberHUD.Testing;

namespace XpTrackerPlugin;

public sealed class MainTests
{
    static readonly ScreenRect Bar = new(new Vector2(100, 1000), new Vector2(900, 1010));

    static readonly GameUi Covered = new([new GamePanel("Map", PanelKind.WorldMap, Vector2.Zero, new Vector2(100, 100), true)]);

    [Test]
    public void VisibleNeedsShowAndAnXpBar()
    {
        var state = new GameState { XpBar = Bar };
        Check.That(Main.Visible(state, new Settings()), "bar up and shown");
        Check.That(!Main.Visible(state, new Settings { Show = false }), "show off");
        Check.That(!Main.Visible(new GameState(), new Settings()), "no bar");
    }

    [Test]
    public void HiddenBehindFullScreenPanels()
    {
        var state = new GameState { XpBar = Bar, Ui = Covered };
        Check.That(!Main.Visible(state, new Settings()), "covered");
        Check.That(Main.Visible(state, new Settings { HideBehindPanels = false }), "panel hiding off");
    }

    static readonly MapInfo MapA = new("Map A", 80, null, null, false, [], []);

    [Test]
    public void SampleDropsXpWhileTheAreaIsUnreadOrUnnamed()
    {
        var xp = new XpState(92, 100, 1000);
        Check.That(Main.Sample(new GameState { Xp = xp, Area = MapA }, false, 1).Xp is not null, "named area keeps xp");
        Check.That(Main.Sample(new GameState { Xp = xp }, false, 1).Xp is null, "area null");
        Check.That(Main.Sample(new GameState { Xp = xp, Area = MapA with { Name = "" } }, false, 1).Xp is null, "empty name");
    }

    [Test]
    public void AreaLoadReadDoesNotMakeAFakeMap()
    {
        var xp = new XpState(92, 100, 1000);
        var town = new MapInfo("Ember's Rest", 1, null, null, true, [], []);
        var session = new XpSession();
        session.Add(Main.Sample(new GameState { Xp = xp, Area = MapA, AreaId = 5 }, false, 0));
        session.Add(Main.Sample(new GameState { Xp = xp, AreaId = 6 }, false, 10));
        session.Add(Main.Sample(new GameState { Xp = xp, Area = town, AreaId = 7 }, false, 20));
        Check.Equal(1, session.CompletedMaps);
    }

    [Test]
    public void ResetIsBoundOnEveryBind()
    {
        var main = new Main();
        main.Bind();
        Check.That(main.Settings.ResetSession is not null, "bound");
        main.Settings.ResetSession = null;
        main.Bind();
        Check.That(main.Settings.ResetSession is not null, "rebound after settings swap");
    }
}
