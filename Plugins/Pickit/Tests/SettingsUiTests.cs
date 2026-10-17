using EmberHUD;
using EmberHUD.Testing;

namespace Pickit;

public sealed class SettingsUiTests
{
    [Test]
    public void SettingsPageDrawsHeadless()
    {
        var settings = new Settings();
        Check.Equal(10, settings.Tiers.Count);
        TestFrame.Run(settings.Draw);
        settings.DebugRects = true;
        TestFrame.Run(settings.Draw);
    }

    [Test]
    public void NormalizePadsTiersAndClampsSliders()
    {
        var settings = new Settings { Tiers = null!, GemSize = 50, GemSpacing = float.NaN, AnchorY = -3 };
        settings.Normalize();
        Check.Equal(10, settings.Tiers.Count);
        settings.Tiers = [new TierStyle { Icon = "marker:circle", Scale = 3, Color = System.Drawing.Color.Red }];
        settings.Normalize();
        Check.Equal(10, settings.Tiers.Count);
        Check.Equal("marker:circle", settings.Tiers[0].Icon);
        Check.That(settings.GemSize <= 1 && settings.AnchorY >= 0 && float.IsFinite(settings.GemSpacing));
    }
}
