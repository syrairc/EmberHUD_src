using EmberHUD;
using EmberHUD.Ui;

namespace Pickit;

static class SettingsUI
{
    public static void Draw(Settings s)
    {
        s.Normalize();
        Controls.Section("Autoloot");
        Setting.Checkbox("Hold key", ref s.HoldKeyEnabled, "Presses the pickup key for you while this key is held");
        if (s.HoldKeyEnabled) Setting.Hotkey("Hold key", ref s.HoldKey, "Modifiers are ignored", "holdkey");
        Setting.Checkbox("Near items", ref s.NearEnabled, "Presses the pickup key while an item you can pick up is within the radius");
        if (s.NearEnabled) Setting.Slider("Radius", ref s.NearRadius, 50, 3000, "Ground distance from the player");
        Setting.Hotkey("Pickup key", ref s.PickupKey, "The key the game uses to pick up - modifiers are ignored", "pickupkey");
    }
}
