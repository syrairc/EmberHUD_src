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
}
