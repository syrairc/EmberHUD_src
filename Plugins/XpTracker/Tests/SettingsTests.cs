using System;
using EmberHUD;
using EmberHUD.Testing;
using EmberHUD.Ui;

namespace XpTrackerPlugin;

public sealed class SettingsTests
{
    static readonly Type SettingsJson = typeof(PluginSettings).Assembly.GetType("EmberHUD.SettingsJson")!;

    static string Serialize(Settings s) => (string)SettingsJson.GetMethod("Serialize")!.Invoke(null, new object[] { s })!;

    static Settings Load(string json) => (Settings)SettingsJson.GetMethod("Load")!.Invoke(null, new object?[] { typeof(Settings), null, json, new Action<string>(m => throw new Exception(m)) })!;

    [Test]
    public void NormalizeFillsNulls()
    {
        var s = new Settings { Left = null!, Center = new TextLine { Template = null!, Style = null! } };
        s.Normalize();
        Check.That(s.Left.Enabled, "left restored");
        Check.Equal("{xpmap} xp/map  {mapshr} maps/hr", s.Left.Template);
        Check.Equal("", s.Center.Template);
        Check.That(s.Center.Style is not null, "style filled");
        Check.Equal("Level in {etah} / {etamaps} maps", s.Right.Template);
    }

    [Test]
    public void DeathPanelBackgroundIsTransparentByDefault() => Check.Equal(0, new Settings().BackgroundOpacity);

    [Test]
    public void DefaultsRoundTripThroughJson()
    {
        var s = new Settings { OffsetY = 11, MapTimeOnly = false };
        s.Center.Template = "{xphr}/h";
        s.Right.Align = HAlign.Left;
        var back = Load(Serialize(s));
        Check.Equal(11, back.OffsetY);
        Check.That(!back.MapTimeOnly, "map time only off");
        Check.Equal("{xphr}/h", back.Center.Template);
        Check.Equal(HAlign.Left, back.Right.Align);
        Check.Equal("{xpmap} xp/map  {mapshr} maps/hr", back.Left.Template);
        Check.Equal(HAlign.Left, new Settings().Left.Align);
        Check.Equal(HAlign.Center, new Settings().Center.Align);
        Check.Equal(HAlign.Right, new Settings().Right.Align);
    }
}
