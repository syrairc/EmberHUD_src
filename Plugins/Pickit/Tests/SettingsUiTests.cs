using System.Linq;
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
        Check.That(!settings.GroundTierNumbers && !settings.InventoryTierNumbers);
        settings.DebugRects = true;
        settings.GroundTierNumbers = true;
        settings.InventoryTierNumbers = true;
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

    [Test]
    public void DefaultTiersFadeFromT1ToWorse()
    {
        var tiers = new Settings().Tiers;
        Check.Equal(10, tiers.Count);
        Check.That(tiers.All(t => t.Icon == "svg:gem"));
        float Strength(int i) => tiers[i].Color.GetSaturation() * tiers[i].Color.A;
        Check.That(Strength(1) > Strength(6));
        Check.That(Strength(6) < Strength(5));
    }

    [Test]
    public void InventoryDefaultsAndScaleClamps()
    {
        var settings = new Settings();
        Check.That(settings.InventoryEnabled);
        Check.Equal(1f, settings.GroundScale);
        Check.Equal(1f, settings.InventoryScale);
        settings.GroundScale = 0;
        settings.InventoryScale = 99;
        settings.Normalize();
        Check.Equal(0.25f, settings.GroundScale);
        Check.Equal(3f, settings.InventoryScale);
        settings.GroundScale = float.NaN;
        settings.InventoryScale = float.PositiveInfinity;
        settings.Normalize();
        Check.Equal(1f, settings.GroundScale);
        Check.Equal(1f, settings.InventoryScale);
    }

    [Test]
    public void NormalizeClampsNumberStyles()
    {
        var settings = new Settings { GroundNumbers = null!, InventoryNumbers = new NumberStyle { Scale = 50 } };
        settings.Normalize();
        Check.Equal(1f, settings.GroundNumbers.Scale);
        Check.Equal(3f, settings.InventoryNumbers.Scale);
        settings.GroundNumbers.Scale = 0.01f;
        settings.InventoryNumbers.Scale = float.NaN;
        settings.Normalize();
        Check.Equal(0.5f, settings.GroundNumbers.Scale);
        Check.Equal(1f, settings.InventoryNumbers.Scale);
    }

    [Test]
    public void NumberStyleControlsDrawWhenTheToggleIsOn()
    {
        var settings = new Settings { GroundTierNumbers = true, InventoryTierNumbers = true };
        TestFrame.Run(settings.Draw);
    }
}
