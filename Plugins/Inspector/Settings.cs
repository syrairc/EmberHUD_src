using Aember;
using Hexa.NET.ImGui;

namespace Inspector;

public sealed class Settings : PluginSettings
{
    [Menu("Pick entities")] public ToggleNode PickEntities = new(true);
    [Menu("Pick map icons", "Only icons that don't belong to a listed entity")] public ToggleNode PickIcons = new(true);
    [Menu("Pick UE actors", "Every Actor in the loaded level - scans all UObjects twice a second while pick mode is on")] public ToggleNode PickActors = new(false);
    [Menu("Pick radius", "Pixels from the cursor")] public RangeNode<float> PickRadius = new(24, 8, 80);
    [Menu("Refresh rate", "Raw memory reads per second, Freeze stops them")] public RangeNode<int> RefreshRate = new(10, 1, 60);

    public override void Draw()
    {
        ImGui.TextDisabled("Browse Game.State and raw game memory, pick things in the world. Read-only.");
        base.Draw();
    }
}
