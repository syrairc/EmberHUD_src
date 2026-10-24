using System.Drawing;
using EmberHUD;
using EmberHUD.Ui;

namespace ActivationMedium;

public sealed class Settings : PluginSettings
{
    public Hotkey HoldKey = new(Key.D1);
    public Hotkey ToggleKey;
    public bool WaitForStop;
    public int StopDelayMs = 50;
    public Hotkey YieldKey = new(Key.Space);
    public bool NearbyEnabled = true;
    public float Radius = 600;
    public bool ShowRadius;
    public Rarity MinRarity = Rarity.Normal;
    public int Slot = 1;
    public bool ShowHighlight = true;
    public Color HighlightColor = Color.FromArgb(0xFF, 0xFF, 0x8C, 0x28);

    public override void Draw()
    {
        Setting.Hotkey("Hold key", ref HoldKey, "The key held down in game while auto attack is active - modifiers are ignored");
        Setting.Hotkey("Toggle key", ref ToggleKey, "Turns auto attack on and off, while on the key stays held no matter who is near");
        Setting.Checkbox("Wait until stopped", ref WaitForStop, "Only hold after you have stood still for the delay, and let go as soon as you move again");
        Setting.Slider("Stop delay (ms)", ref StopDelayMs, 0, 2000, "How long you must stand still first");
        Setting.Hotkey("Yield key", ref YieldKey, "While you physically hold this key auto attack lets go of the hold key, the game only keeps one held skill key at a time");
        Setting.Checkbox("Hold when enemies are near", ref NearbyEnabled, "Hold the key while at least one enemy is within the radius");
        Setting.Slider("Radius", ref Radius, 50, 3000, "World units around you");
        Setting.Checkbox("Show radius", ref ShowRadius, "Draws the radius as a ring on the ground around you");
        Setting.Choice("Min rarity", ref MinRarity, "Weakest enemy that counts as near");
        Setting.Slider("Skill slot", ref Slot, 1, 5, "The skill bar slot that lights up while active");
        Setting.Checkbox("Highlight slot", ref ShowHighlight);
        Setting.Color("Highlight color", ref HighlightColor);
    }
}
