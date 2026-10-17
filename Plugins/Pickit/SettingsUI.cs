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

    static void NumberOptions(NumberStyle style, string id)
    {
        ImGui.Indent();
        Setting.Color("Text colour", ref style.Color, "Tier number colour and opacity", id + "-colour");
        Setting.Slider("Text size", ref style.Scale, 0.5f, 3, "Multiplies the tier number size", id + "-size", "%.2f");
        Setting.Checkbox("Outline", ref style.Outline, "Black outline around the tier number", id + "-outline");
        ImGui.Unindent();
    }

    static void Affixes(Settings s)
    {
        Setting.Checkbox("Affixes (ground)", ref s.GemsEnabled, "Draws an icon per prefix (left) and suffix (right) on the item labels the game shows");
        Setting.Checkbox("Tier number (ground)", ref s.GroundTierNumbers, "Draws the affix tier number in the centre of each ground icon");
        if (s.GroundTierNumbers) NumberOptions(s.GroundNumbers, "ground");
        Setting.Checkbox("Affixes (inventory and stash)", ref s.InventoryEnabled, "Draws an icon per prefix (left) and suffix (right) on each item in the open bag and stash");
        Setting.Checkbox("Tier number (inventory and stash)", ref s.InventoryTierNumbers, "Draws the affix tier number in the centre of each bag and stash icon");
        if (s.InventoryTierNumbers) NumberOptions(s.InventoryNumbers, "inventory");
        Setting.Slider("Ground scale", ref s.GroundScale, 0.25f, 3, "Multiplies the icon size on ground labels", fmt: "%.2f");
        Setting.Slider("Inventory scale", ref s.InventoryScale, 0.25f, 3, "Multiplies the icon size in the bag and stash", fmt: "%.2f");
        Setting.Slider("Size", ref s.GemSize, 0.1f, 1, "Fraction of the label height", fmt: "%.2f");
        Setting.Slider("Spacing", ref s.GemSpacing, 0, 2, "Fraction of the icon size", fmt: "%.2f");
        Setting.Slider("Anchor Y", ref s.AnchorY, 0, 1, "0 is the top edge of the label, 1 the bottom edge", fmt: "%.2f");
        Setting.Slider("Inset X", ref s.InsetX, 0, 200, "Pixels inward from the label corner");
        Setting.Checkbox("Debug rects", ref s.DebugRects, "Outlines every label rectangle");
        for (var i = 0; i < s.Tiers.Count; i++)
        {
            var tier = s.Tiers[i];
            var reset = Settings.DefaultTier(i);
            ImGui.PushID(i);
            Controls.IconPicker("icon", ref tier.Icon, ref tier.Color, ref tier.Scale, 0.1f, 10, reset.Icon, reset.Color, reset.Scale, baseSize: 24);
            ImGui.SameLine();
            ImGui.TextUnformatted($"T{(i == 0 ? "0 / unknown" : i)}");
            ImGui.PopID();
        }
    }
}
