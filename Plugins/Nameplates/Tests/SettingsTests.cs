using System;
using System.Drawing;
using EmberHUD.Ui;
using EmberHUD;
using EmberHUD.Testing;

namespace NameplatesPlugin;

public sealed class SettingsTests
{
    static readonly Type SettingsJson = typeof(PluginSettings).Assembly.GetType("EmberHUD.SettingsJson")!;

    static string Serialize(Settings s) => (string)SettingsJson.GetMethod("Serialize")!.Invoke(null, new object[] { s })!;

    static Settings Load(string json) => (Settings)SettingsJson.GetMethod("Load")!.Invoke(null, new object?[] { typeof(Settings), null, json, new Action<string>(m => throw new Exception(m)) })!;

    [Test]
    public void SettingsRoundTripThroughJson()
    {
        var s = new Settings();
        var boss = s.Buckets[(int)Bucket.Boss].Style;
        boss.Bar.Width = 321;
        boss.Bar.Fill = Color.FromArgb(0x12, 0x34, 0x56, 0x78);
        boss.Above.Style.Scale = 2;
        boss.Above.Style.BorderEnabled = true;
        boss.OnBar.Template = "{hp}/{hpmax}";
        boss.Below.Template = "{dist}m";
        s.Overrides.Add(new StyleOverride
        {
            Kind = MatchKind.CfgId,
            Name = "Some Boss",
            CfgId = 1170000,
            Style = new PlateStyle { OffsetY = 7, Bar = new BarStyle { Width = 55, Fill = Color.FromArgb(1, 2, 3, 4) }, Above = new TextLine { Enabled = true, Template = "!{name}", Style = new TextStyle { Scale = 1.5f } } },
        });

        var back = Load(Serialize(s));

        var backBoss = back.Buckets[(int)Bucket.Boss].Style;
        Check.Equal(321f, backBoss.Bar.Width);
        Check.Equal(boss.Bar.Fill, backBoss.Bar.Fill);
        Check.Equal(2f, backBoss.Above.Style.Scale);
        Check.That(backBoss.Above.Style.BorderEnabled);
        Check.Equal("{hp}/{hpmax}", backBoss.OnBar.Template);
        Check.Equal("{dist}m", backBoss.Below.Template);
        Check.Equal(1, back.Overrides.Count);
        var ov = back.Overrides[0];
        Check.Equal(MatchKind.CfgId, ov.Kind);
        Check.Equal("Some Boss", ov.Name);
        Check.Equal(1170000, ov.CfgId);
        Check.Equal(7f, ov.Style.OffsetY);
        Check.Equal(55f, ov.Style.Bar.Width);
        Check.Equal(Color.FromArgb(1, 2, 3, 4), ov.Style.Bar.Fill);
        Check.Equal("!{name}", ov.Style.Above.Template);
        Check.Equal(1.5f, ov.Style.Above.Style.Scale);
    }

    [Test]
    public void EveryBucketHasAStyle()
    {
        var s = new Settings();
        Check.Equal(Enum.GetValues<Bucket>().Length, s.Buckets.Length);
        foreach (var bucket in s.Buckets) Check.That(bucket?.Style?.Bar != null);
        s.Buckets[0].Style.Bar.Width = 777;
        s.Buckets[0].Style.Above.Style.Scale = 9;
        for (var i = 1; i < s.Buckets.Length; i++)
        {
            Check.That(s.Buckets[i].Style.Bar.Width != 777);
            Check.Equal(1f, s.Buckets[i].Style.Above.Style.Scale);
        }
    }

    [Test]
    public void PlateStyleCloneIsIndependent()
    {
        var source = new PlateStyle();
        var clone = source.Clone();
        clone.Bar.Width = 999;
        clone.Above.Template = "x";
        clone.Above.Style.Scale = 3;
        Check.That(source.Bar.Width != 999);
        Check.Equal("{name}", source.Above.Template);
        Check.Equal(1f, source.Above.Style.Scale);
    }
}
