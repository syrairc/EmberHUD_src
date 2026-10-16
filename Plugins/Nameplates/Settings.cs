using System;
using System.Collections.Generic;
using System.Drawing;
using EmberHUD;
using EmberHUD.Ui;

namespace NameplatesPlugin;

public enum Bucket { Normal, Magic, Rare, Boss, Ally, Minion, Npc, Self }

public enum TextRow { Above, OnBar, Below }

public enum MatchKind { Name, CfgId }

public sealed class TextLine
{
    public bool Enabled;
    public string Template = "";
    public TextStyle Style = TextStyle.Hud();
    public Align Align = Align.Center;

    public TextLine Clone()
    {
        var copy = (TextLine)MemberwiseClone();
        copy.Style = Style.Clone();
        return copy;
    }
}

public sealed class BarStyle
{
    public float Width = 100;
    public float Height = 12;
    public float BorderWidth = 1;
    public Color Fill = Color.FromArgb(0xFF, 0xC8, 0x32, 0x32);
    public Color Back = Color.FromArgb(0xA0, 0, 0, 0);
    public Color Border = Color.FromArgb(0xFF, 0, 0, 0);
    public string Texture = "";

    public BarStyle Clone() => (BarStyle)MemberwiseClone();
}

public sealed class PlateStyle
{
    public BarStyle Bar = new();
    public TextLine Above = new() { Enabled = true, Template = "{name}" };
    public TextLine OnBar = new() { Enabled = true, Template = "{hp%}%" };
    public TextLine Below = new();
    public float OffsetY;

    public PlateStyle Clone() => new() { Bar = Bar.Clone(), Above = Above.Clone(), OnBar = OnBar.Clone(), Below = Below.Clone(), OffsetY = OffsetY };
}

public sealed class BucketSettings
{
    public bool Show = true;
    public bool DamagedOnly;
    public PlateStyle Style = new();
}

public sealed class StyleOverride
{
    public bool Enabled = true;
    public MatchKind Kind;
    public string Name = "";
    public int CfgId;
    public PlateStyle Style = new();
}

public sealed class Settings : PluginSettings
{
    static readonly Color Hostile = Color.FromArgb(0xFF, 0xC8, 0x32, 0x32);
    static readonly Color Friendly = Color.FromArgb(0xFF, 0x3C, 0xB4, 0x50);
    static readonly Color Neutral = Color.FromArgb(0xFF, 0x96, 0x96, 0x96);

    public float MaxDistance = 2500;
    public bool HideBehindPanels = true;
    public BucketSettings[] Buckets = DefaultBuckets();
    public List<StyleOverride> Overrides = new();

    static BucketSettings[] DefaultBuckets()
    {
        var buckets = new BucketSettings[Enum.GetValues<Bucket>().Length];
        buckets[(int)Bucket.Normal] = Plate(100, 12, Hostile);
        buckets[(int)Bucket.Magic] = Plate(100, 12, Color.FromArgb(0xFF, 0x50, 0x78, 0xE6));
        buckets[(int)Bucket.Rare] = Plate(120, 14, Color.FromArgb(0xFF, 0xE6, 0xC8, 0x32));
        buckets[(int)Bucket.Boss] = Plate(160, 18, Color.FromArgb(0xFF, 0xE6, 0x78, 0x28));
        buckets[(int)Bucket.Ally] = Plate(80, 10, Friendly);
        buckets[(int)Bucket.Minion] = Plate(60, 8, Friendly, show: false);
        buckets[(int)Bucket.Npc] = Plate(80, 10, Neutral, show: false);
        buckets[(int)Bucket.Self] = Plate(80, 10, Friendly);
        return buckets;
    }

    static BucketSettings Plate(float width, float height, Color fill, bool show = true)
    {
        var bucket = new BucketSettings { Show = show };
        bucket.Style.Bar.Width = width;
        bucket.Style.Bar.Height = height;
        bucket.Style.Bar.Fill = fill;
        return bucket;
    }

    public override void Draw() => SettingsUI.Draw(this);
}
