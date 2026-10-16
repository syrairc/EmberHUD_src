using System.Drawing;
using System.Numerics;
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

    static readonly PathState Path = new(1, "A", Color.Red, Vector2.Zero, null, 0);

    [Test]
    public void PathColourUnlessOverridden()
    {
        var style = new SurfaceStyle { Colour = Color.Blue };
        Check.Equal(Color.Red, style.ColourFor(Path));
        style.OverrideColour = true;
        Check.Equal(Color.Blue, style.ColourFor(Path));
    }
}
