using EmberHUD;
using EmberHUD.Ui;
using Hexa.NET.ImGui;

namespace Inspector;

public sealed class Settings : PluginSettings
{
    public bool PickEntities = true;
    public bool PickIcons = true;
    public float PickRadius = 24;

    public override void Draw()
    {
        ImGui.TextDisabled("Browse what the Api provides in Game.State, pick things in the world. Read-only.");
        Setting.Checkbox("Pick entities", ref PickEntities);
        Setting.Checkbox("Pick map icons", ref PickIcons, "Only icons that don't belong to a listed entity");
        Setting.Slider("Pick radius", ref PickRadius, 8, 80, "Pixels from the cursor");
        Controls.Section("Hotkeys");
        Controls.PluginHotkeys();
    }
}
