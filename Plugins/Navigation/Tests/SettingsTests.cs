using EmberHUD;
using EmberHUD.Testing;

namespace NavigationPlugin;

public sealed class SettingsTests
{
    [Test]
    public void SettingsPageDrawsAllTabs()
    {
        var settings = new Settings();
        Check.That(TestFrame.Vertices(settings.Draw, inWindow: true) > 0, "page");
        Check.That(TestFrame.Vertices(settings.DrawGeneral, inWindow: true) > 0, "general");
        Check.That(TestFrame.Vertices(settings.DrawGround, inWindow: true) > 0, "ground");
        Check.That(TestFrame.Vertices(settings.DrawMinimap, inWindow: true) > 0, "minimap");
        Check.That(TestFrame.Vertices(settings.DrawLegend, inWindow: true) > 0, "legend");
        Check.That(TestFrame.Vertices(settings.DrawLabels, inWindow: true) > 0, "labels");
    }
}
