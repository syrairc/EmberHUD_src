using System.Numerics;
using EmberHUD;
using EmberHUD.Testing;

namespace Pickit;

public sealed class MainTests
{
    [Test]
    public void AsksForGroundItemsOnlyWhenNeeded()
    {
        TestSend.Record();
        TestGame.Set(new GameState());
        var plugin = new Main();
        plugin.Initialize();
        Check.That(TestGame.GroundItemsAskedDuring(plugin.Tick), "near on");
        plugin.Settings.NearEnabled = false;
        plugin.Settings.GemsEnabled = false;
        Check.That(!TestGame.GroundItemsAskedDuring(plugin.Tick), "both off");
        plugin.Settings.GemsEnabled = true;
        Check.That(TestGame.GroundItemsAskedDuring(plugin.Tick), "gems on");
    }

    [Test]
    public void DisableReleasesKeys()
    {
        var sent = TestSend.Record();
        TestGame.Set(new GameState { Status = "live", ChatOpen = false });
        TestInput.Hold(Key.Space);
        var plugin = new Main();
        plugin.Initialize();
        plugin.Tick();
        Check.That(sent.Count > 0 && sent[0].Down && sent[0].Key == Key.A, "pressed");
        plugin.OnDisable();
        Check.That(!sent[^1].Down && sent[^1].Key == Key.A, "released");
        TestInput.Hold();
    }

    static GameState WithItem(GameUi? ui = null) => new()
    {
        Ui = ui ?? GameUi.Empty,
        GroundItems = [new GroundItem(1, new Vector2(100, 200), new Vector2(300, 230), Vector3.Zero, true, [1], [2])]
    };

    static Main Plugin()
    {
        var plugin = new Main();
        plugin.Initialize();
        return plugin;
    }

    [Test]
    public void FullScreenPanelDrawsNothing()
    {
        TestGame.Set(WithItem(new GameUi([new GamePanel("inv", PanelKind.Inventory, Vector2.Zero, Vector2.One, true)])));
        Check.Equal(0, TestFrame.Vertices(Plugin().Render));
    }

    [Test]
    public void NoGroundItemsDrawsNothing()
    {
        TestGame.Set(new GameState());
        Check.Equal(0, TestFrame.Vertices(Plugin().Render));
    }

    [Test]
    public void DrawsGemsForAnItem()
    {
        TestGame.Set(WithItem());
        var plugin = Plugin();
        Check.That(TestFrame.Vertices(plugin.Render) > 0);
        plugin.Settings.GemsEnabled = false;
        Check.Equal(0, TestFrame.Vertices(plugin.Render));
    }
}
