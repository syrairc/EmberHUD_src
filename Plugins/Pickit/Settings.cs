using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using EmberHUD;

namespace Pickit;

public sealed class TierStyle
{
    public string Icon = "marker:diamond";
    public Color Color = Color.White;
    public float Scale = 1;
}

public sealed class Settings : PluginSettings
{
    public bool HoldKeyEnabled = true;
    public Hotkey HoldKey = new(Key.Space);
    public bool NearEnabled = true;
    public float NearRadius = 500;
    public Hotkey PickupKey = new(Key.A);
    public bool GemsEnabled = true;
    public List<TierStyle> Tiers = DefaultTiers();
    public float GemSize = 0.33f;
    public float GemSpacing = 0.1f;
    public float AnchorY = 0;
    public float InsetX = 0;
    public bool BestIsLowest = true;
    public bool DebugRects = false;

    public const int TierCount = 10;

    static readonly uint[] TierColours = [0xFFFF2B2B, 0xFFFF2B2B, 0xFFD44BFF, 0xEBA95CE8, 0xD28A6BC4, 0xFF3DB2FF, 0xD75C86B3, 0xAA5F7185, 0xAA5F7185, 0xAA5F7185];

    public static TierStyle DefaultTier(int tier) => new() { Color = Color.FromArgb((int)TierColours[tier]) };

    static List<TierStyle> DefaultTiers() => [.. Enumerable.Range(0, TierCount).Select(DefaultTier)];

    public void Normalize()
    {
        NearRadius = float.IsFinite(NearRadius) ? Math.Clamp(NearRadius, 50, 3000) : 500;
        GemSize = Clamp(GemSize, 0.1f, 1, 0.33f);
        GemSpacing = Clamp(GemSpacing, 0, 2, 0.1f);
        AnchorY = Clamp(AnchorY, 0, 1, 0);
        InsetX = Clamp(InsetX, 0, 200, 0);
        Tiers = [.. (Tiers ?? []).Take(TierCount).Select((t, i) => t ?? DefaultTier(i))];
        while (Tiers.Count < TierCount) Tiers.Add(DefaultTier(Tiers.Count));
        foreach (var tier in Tiers)
        {
            tier.Icon ??= "marker:diamond";
            tier.Scale = Clamp(tier.Scale, 0.1f, 10, 1);
        }
    }

    static float Clamp(float value, float min, float max, float fallback) => float.IsFinite(value) ? Math.Clamp(value, min, max) : fallback;

    public override void Draw() => SettingsUI.Draw(this);
}
