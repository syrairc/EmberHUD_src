using EmberHUD;
using EmberHUD.Ui;
using Hexa.NET.ImGui;

namespace Inspector;

public sealed class Settings : PluginSettings
{
    public bool PickEntities = true;
    public bool PickIcons = true;
    public bool PickActors;
    public float PickRadius = 24;
    public int RefreshRate = 10;

    public override void Draw()
    {
        ImGui.TextDisabled("Browse Game.State and raw game memory, pick things in the world. Read-only.");
        Setting.Checkbox("Pick entities", ref PickEntities);
        Setting.Checkbox("Pick map icons", ref PickIcons, "Only icons that don't belong to a listed entity");
        Setting.Checkbox("Pick UE actors", ref PickActors, "Every Actor in the loaded level - scans all UObjects twice a second while pick mode is on");
        Setting.Slider("Pick radius", ref PickRadius, 8, 80, "Pixels from the cursor");
        Setting.Slider("Refresh rate", ref RefreshRate, 1, 60, "Raw memory reads per second, Freeze stops them");
    }
}
