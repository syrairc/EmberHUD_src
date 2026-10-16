using EmberHUD.Testing;
using Hexa.NET.ImGui;

namespace NameplatesPlugin;

public sealed class SettingsUiTests
{
    static void Page(Settings s) => TestFrame.Run(() =>
    {
        ImGui.Begin("nameplates settings");
        s.Draw();
        ImGui.End();
    });

    [Test]
    public void TheSettingsPageDraws() => Page(new Settings());

    [Test]
    public void ThePageDrawsWithAnOverrideAndAnUnknownTexture()
    {
        var s = new Settings();
        var row = SettingsUI.NewOverride(s);
        row.Style.Bar.Texture = "NoSuchTexture";
        s.Overrides.Add(row);
        SettingsUI.OpenEditors.Add(row);
        s.Overrides.Add(new StyleOverride { Kind = MatchKind.CfgId, CfgId = 7 });
        s.Buckets[(int)Bucket.Boss].Style.Bar.Texture = "Missing";
        Page(s);
    }

    [Test]
    public void AddingAnOverrideCopiesTheBucketStyle()
    {
        var s = new Settings();
        var bucket = s.Buckets[(int)Bucket.Normal].Style;
        bucket.Bar.Width = 321;
        bucket.Above.Template = "{name} x";
        var row = SettingsUI.NewOverride(s);
        Check.That(!ReferenceEquals(bucket, row.Style));
        Check.That(!ReferenceEquals(bucket.Bar, row.Style.Bar));
        Check.That(!ReferenceEquals(bucket.Above, row.Style.Above));
        Check.Equal(321f, row.Style.Bar.Width);
        Check.Equal(bucket.Bar.Height, row.Style.Bar.Height);
        Check.Equal(bucket.Bar.Fill, row.Style.Bar.Fill);
        Check.Equal("{name} x", row.Style.Above.Template);
        row.Style.Bar.Width = 5;
        Check.Equal(321f, bucket.Bar.Width);
    }
}
