using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using EmberHUD;

namespace Pickit;

public sealed class TierStyle
{
    public string Icon = "svg:gem";
    public Color Color = Color.White;
    public float Scale = 1;
}

public sealed class NumberStyle
{
    public Color Color = Color.White;
    public float Scale = 1;
    public bool Outline = true;
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
    public float GlobalScale = 1;
    public float GemSpacing = 0.1f;
    public float AnchorY = 0;
    public float InsetX = 0;
    public bool InventoryEnabled = true;
    public float GroundScale = 1;
    public float InventoryScale = 1;
    public bool GroundTierNumbers = false;
    public bool InventoryTierNumbers = false;
    public NumberStyle GroundNumbers = new();
    public NumberStyle InventoryNumbers = new();
    public bool DebugRects = false;

    public const int TierCount = 10;

    static readonly uint[] TierColours = [0xFFFF2B2B, 0xFFFF2B2B, 0xFFD44BFF, 0xEBA95CE8, 0xD28A6BC4, 0xFF3DB2FF, 0xD75C86B3, 0xAA5F7185, 0xAA5F7185, 0xAA5F7185];

    public static TierStyle DefaultTier(int tier) => new() { Color = Color.FromArgb((int)TierColours[tier]) };

    static List<TierStyle> DefaultTiers() => [.. Enumerable.Range(0, TierCount).Select(DefaultTier)];

    public void Normalize()
    {
        NearRadius = float.IsFinite(NearRadius) ? Math.Clamp(NearRadius, 50, 3000) : 500;
        GlobalScale = Clamp(GlobalScale, 0.25f, 3, 1);
        GemSpacing = Clamp(GemSpacing, 0, 2, 0.1f);
        AnchorY = Clamp(AnchorY, 0, 1, 0);
        InsetX = Clamp(InsetX, 0, 200, 0);
        GroundScale = Clamp(GroundScale, 0.25f, 3, 1);
        InventoryScale = Clamp(InventoryScale, 0.25f, 3, 1);
        GroundNumbers = Normalized(GroundNumbers);
        InventoryNumbers = Normalized(InventoryNumbers);
        Tiers = [.. (Tiers ?? []).Take(TierCount).Select((t, i) => t ?? DefaultTier(i))];
        while (Tiers.Count < TierCount) Tiers.Add(DefaultTier(Tiers.Count));
        foreach (var tier in Tiers)
        {
            tier.Icon ??= "svg:gem";
            tier.Scale = Clamp(tier.Scale, 0.1f, 10, 1);
        }
    }

    static NumberStyle Normalized(NumberStyle? style)
    {
        style ??= new();
        style.Scale = Clamp(style.Scale, 0.5f, 3, 1);
        return style;
    }

    static float Clamp(float value, float min, float max, float fallback) => float.IsFinite(value) ? Math.Clamp(value, min, max) : fallback;

    public override void Draw() => SettingsUI.Draw(this);
}
