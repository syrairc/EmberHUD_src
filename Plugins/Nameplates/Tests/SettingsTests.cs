using System;
using System.Linq;
using System.Drawing;
using System.Numerics;
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
        boss.Below.Align = HAlign.Right;
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
        Check.Equal(HAlign.Right, backBoss.Below.Align);
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
    public void AnchorAndDrawBarRoundTripAndDefault()
    {
        Check.That(!new Settings().AnchorToGameBar);
        Check.That(new PlateStyle().DrawBar);
        var s = new Settings { AnchorToGameBar = true };
        s.Buckets[(int)Bucket.Boss].Style.DrawBar = false;
        var back = Load(Serialize(s));
        Check.That(back.AnchorToGameBar);
        Check.That(!back.Buckets[(int)Bucket.Boss].Style.DrawBar);
        Check.That(back.Buckets[(int)Bucket.Normal].Style.DrawBar);
        var old = Load("{\"DistanceUnit\": 100}");
        Check.That(!old.AnchorToGameBar);
        Check.That(old.Buckets[(int)Bucket.Normal].Style.DrawBar);
    }

    [Test]
    public void CloneCopiesDrawBar()
    {
        var source = new PlateStyle { DrawBar = false };
        Check.That(!source.Clone().DrawBar);
    }

    [Test]
    public void NormalizeFillsAShortOrNullSettingsObject()
    {
        var s = new Settings();
        s.Buckets = [null!, new BucketSettings { Style = null! }];
        s.Buckets[1].Show = false;
        s.Overrides.Add(null!);
        s.Overrides.Add(new StyleOverride { Name = null!, Style = null! });
        var line = new PlateStyle { Bar = null!, Above = new TextLine { Template = null!, Style = null! }, OnBar = null!, Below = null! };
        s.Buckets[1].Style = line;
        s.Normalize();
        Check.Equal(Enum.GetValues<Bucket>().Length, s.Buckets.Length);
        Check.That(!s.Buckets[1].Show);
        foreach (var bucket in s.Buckets)
        {
            Check.That(bucket.Style.Bar != null && bucket.Style.OnBar != null && bucket.Style.Below != null);
            Check.That(bucket.Style.Above.Template != null && bucket.Style.Above.Style != null);
        }
        Check.Equal(1, s.Overrides.Count);
        Check.Equal("", s.Overrides[0].Name);
        Check.That(s.Overrides[0].Style.Bar != null);
        Check.That(Resolver.Resolve(s, new Entity(1, 1, Vector3.Zero, EntityKind.Monster, Rarity.Boss) { Life = 1, MaxLife = 2 }, 0) is not null);
    }

    [Test]
    public void CastBarIsOffByDefaultInEveryBucket() => Check.That(new Settings().Buckets.All(b => !b.CastBar!.Enabled));

    [Test]
    public void NormalizeRepairsANullCastBar()
    {
        var s = new Settings();
        s.Buckets[(int)Bucket.Rare].CastBar = null;
        s.Normalize();
        var cast = s.Buckets[(int)Bucket.Rare].CastBar!;
        Check.That(cast.Bar != null && cast.Text != null && cast.Bar.Texture != null);
        s.Buckets[(int)Bucket.Rare].CastBar = new CastBarSettings { Bar = null!, Text = new TextLine { Template = null!, Style = null! } };
        s.Normalize();
        cast = s.Buckets[(int)Bucket.Rare].CastBar!;
        Check.That(cast.Bar != null && cast.Text.Template != null && cast.Text.Style != null);
    }

    [Test]
    public void CastBarSettingsRoundTripThroughJsonPerBucket()
    {
        var s = new Settings();
        var rare = s.Buckets[(int)Bucket.Rare].CastBar!;
        rare.Enabled = true;
        rare.OffsetY = 31;
        rare.Bar.Fill = Color.FromArgb(1, 2, 3, 4);
        var loaded = Load(Serialize(s));
        Check.That(loaded.Buckets[(int)Bucket.Rare].CastBar!.Enabled);
        Check.Equal(31f, loaded.Buckets[(int)Bucket.Rare].CastBar!.OffsetY);
        Check.Equal(Color.FromArgb(1, 2, 3, 4), loaded.Buckets[(int)Bucket.Rare].CastBar!.Bar.Fill);
        Check.That(!loaded.Buckets[(int)Bucket.Normal].CastBar!.Enabled);
    }

    [Test]
    public void TheOldGlobalCastBarMovesIntoEveryBucket()
    {
        var s = Load("""{"CastBar":{"Enabled":true,"OffsetY":31},"Buckets":[{},{},{},{}]}""");
        s.Normalize();
        Check.That(s.CastBar is null);
        var casts = s.Buckets.Take(4).Select(b => b.CastBar!).ToArray();
        Check.That(casts.All(c => c.Enabled && c.OffsetY == 31));
        Check.That(casts.Distinct().Count() == 4, "buckets share one cast bar instance");
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
