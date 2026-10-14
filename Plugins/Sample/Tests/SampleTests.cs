using System.Linq;
using EmberHUD;
using EmberHUD.Testing;

namespace Sample;

public sealed class SampleTests
{
    [Test]
    public void StatusTextShowsTheGameStatus()
    {
        Check.Equal("Status: starting", Main.StatusText(GameState.Empty));
        Check.Equal("Status: in map", Main.StatusText(GameState.Empty with { Status = "in map" }));
    }

    [Test]
    public void RegistersTheToggleHotkeyOnF8()
    {
        var plugin = new Main();
        plugin.Initialize();
        var binding = plugin.Hotkeys.Single();
        Check.Equal("Toggle", binding.Id);
        Check.Equal(Key.F8, binding.Default.Key);
    }

    [Test]
    public void TheHotkeyShowsAndHidesTheMarker()
    {
        var plugin = new Main();
        plugin.Initialize();
        try
        {
            TestInput.Hold(Key.F8);
            TestInput.Dispatch(plugin);
            Check.That(!plugin.Settings.Show, "press hides");
            TestInput.Hold();
            TestInput.Hold(Key.F8);
            TestInput.Dispatch(plugin);
            Check.That(plugin.Settings.Show, "second press shows");
        }
        finally
        {
            TestInput.Hold();
        }
    }

    [Test]
    public void RenderDrawsOnlyWhileShown()
    {
        var plugin = new Main();
        Check.That(TestFrame.Vertices(plugin.Render) > 0, "shown draws the marker and status");
        plugin.Settings.Show = false;
        Check.That(TestFrame.Vertices(plugin.Render) == 0, "hidden draws nothing");
    }

    [Test]
    public void TheSettingsPageDraws()
    {
        Check.That(TestFrame.Vertices(new Settings().Draw, inWindow: true) > 0, "settings page draws");
    }
}
