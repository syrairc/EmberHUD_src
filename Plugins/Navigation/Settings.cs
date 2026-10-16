using EmberHUD;
using EmberHUD.Ui;

namespace NavigationPlugin;

public sealed class GeneralTab
{
    public bool Show = true;
    public bool Ground = true;
    public bool Minimap = true;
}

public sealed class SurfaceStyle
{
    public float Size = 3;
    public float Spacing = 400;
    public float Speed = 300;
    public float Opacity = 0.9f;
    public float Trail = 150;
    public float BaseLine = 1;
}

public sealed class Settings : PluginSettings
{
    public GeneralTab General = new();
    public SurfaceStyle GroundStyle = new() { Size = 25, Spacing = 250, Speed = 400, Trail = 200, BaseLine = 2 };
    public SurfaceStyle MinimapStyle = new();

    public override void Draw() => Layout.TabBar("navigation",
        ("General", DrawGeneral),
        ("Ground", () => DrawStyle("Ground", GroundStyle, "Particle radius in world units")),
        ("Minimap", () => DrawStyle("Minimap", MinimapStyle, "Particle radius in pixels")));

    void DrawGeneral()
    {
        Setting.Checkbox("Show", ref General.Show);
        Setting.Checkbox("Ground", ref General.Ground, "Particles flow along the path on the ground in the game world");
        Setting.Checkbox("Minimap", ref General.Minimap, "Particles flow along the path on the Minimap plugin's map");
    }

    static void DrawStyle(string name, SurfaceStyle style, string sizeTip)
    {
        Setting.Slider("Size", ref style.Size, 0.5f, 100, sizeTip, id: name + "Size");
        Setting.Slider("Spacing", ref style.Spacing, 20, 2000, "World units between particles", id: name + "Spacing");
        Setting.Slider("Speed", ref style.Speed, 0, 2000, "World units per second", id: name + "Speed");
        Setting.Slider("Opacity", ref style.Opacity, 0.05f, 1, id: name + "Opacity");
        Setting.Slider("Trail", ref style.Trail, 0, 1000, "Length in world units of the fading tail behind each particle, 0 = off", id: name + "Trail");
        Setting.Slider("Base line", ref style.BaseLine, 0, 10, "Thickness in pixels of the faint line under the particles, 0 = off", id: name + "BaseLine");
    }
}
