using System.Drawing;
using EmberHUD;
using EmberHUD.Ui;
using Hexa.NET.ImGui;

namespace Pickit;

static class SettingsUI
{
    public static void Draw(Settings s)
    {
        s.Normalize();
        Layout.TabBar("pickit", ("Autoloot", () => Autoloot(s)), ("Affixes", () => Affixes(s)));
    }

    static void Autoloot(Settings s)
    {
        Setting.Checkbox("Hold key", ref s.HoldKeyEnabled, "Presses the pickup key for you while this key is held");
        if (s.HoldKeyEnabled) Setting.Hotkey("Hold key", ref s.HoldKey, "Modifiers are ignored", "holdkey");
        Setting.Checkbox("Near items", ref s.NearEnabled, "Presses the pickup key while an item you can pick up is within the radius");
        if (s.NearEnabled) Setting.Slider("Radius", ref s.NearRadius, 50, 3000, "Ground distance from the player");
        Setting.Hotkey("Pickup key", ref s.PickupKey, "The key the game uses to pick up - modifiers are ignored", "pickupkey");
    }

    static void Affixes(Settings s)
    {
        Setting.Checkbox("Affixes", ref s.GemsEnabled, "Draws an icon per prefix (left) and suffix (right) on the item labels the game shows");
        Setting.Slider("Size", ref s.GemSize, 0.1f, 1, "Fraction of the label height", fmt: "%.2f");
        Setting.Slider("Spacing", ref s.GemSpacing, 0, 2, "Fraction of the icon size", fmt: "%.2f");
        Setting.Slider("Anchor Y", ref s.AnchorY, 0, 1, "0 is the top edge of the label, 1 the bottom edge", fmt: "%.2f");
        Setting.Slider("Inset X", ref s.InsetX, 0, 200, "Pixels inward from the label corner");
        Setting.Checkbox("Lowest tier is best", ref s.BestIsLowest, "The best tier sits at the corner");
        Setting.Checkbox("Debug rects", ref s.DebugRects, "Outlines every label rectangle");
        for (var i = 0; i < s.Tiers.Count; i++)
        {
            var tier = s.Tiers[i];
            var reset = Settings.DefaultTier(i);
            ImGui.PushID(i);
            Controls.IconPicker("icon", ref tier.Icon, ref tier.Color, ref tier.Scale, 0.1f, 10, reset.Icon, reset.Color, reset.Scale, baseSize: 24);
            ImGui.SameLine();
            var opacity = (int)tier.Color.A * 100 / 255;
            ImGui.SetNextItemWidth(160);
            if (ImGui.SliderInt($"T{(i == 0 ? "0 / unknown" : i)}##opacity", ref opacity, 0, 100, "%d%%")) tier.Color = Color.FromArgb(opacity * 255 / 100, tier.Color);
            ImGui.PopID();
        }
    }
}
