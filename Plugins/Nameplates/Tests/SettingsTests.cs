using System;
using System.Reflection;
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
        s.Buckets[(int)Bucket.Rare].Style.Bar.Width = 321;
        s.Buckets[(int)Bucket.Rare].Style.Below.Template = "{dist}m";
        s.Overrides.Add(new StyleOverride { Kind = MatchKind.CfgId, CfgId = 4242, Name = "Boss", Style = new PlateStyle { OffsetY = 7 } });
        var back = Load(Serialize(s));
        Check.Equal(321f, back.Buckets[(int)Bucket.Rare].Style.Bar.Width);
        Check.Equal("{dist}m", back.Buckets[(int)Bucket.Rare].Style.Below.Template);
        Check.Equal(1, back.Overrides.Count);
        Check.Equal(MatchKind.CfgId, back.Overrides[0].Kind);
        Check.Equal(4242, back.Overrides[0].CfgId);
        Check.Equal(7f, back.Overrides[0].Style.OffsetY);
        Check.Equal(s.Buckets[(int)Bucket.Boss].Style.Bar.Fill, back.Buckets[(int)Bucket.Boss].Style.Bar.Fill);
        Check.Equal(s.Buckets[(int)Bucket.Boss].Style.Above.Style.ShadowEnabled, back.Buckets[(int)Bucket.Boss].Style.Above.Style.ShadowEnabled);
    }

    [Test]
    public void EveryBucketHasAStyle()
    {
        var s = new Settings();
        Check.Equal(Enum.GetValues<Bucket>().Length, s.Buckets.Length);
        foreach (var bucket in s.Buckets) Check.That(bucket?.Style?.Bar != null);
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
