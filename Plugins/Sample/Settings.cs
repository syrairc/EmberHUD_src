using System.Drawing;
using EmberHUD;
using EmberHUD.Ui;

namespace Sample;

public sealed class Settings : PluginSettings
{
    public bool Show = true;
    public float Radius = 10;
    public Color Colour = Color.White;
    public TextStyle Label = TextStyle.WorldLabel();

    public override void Draw()
    {
        Setting.Checkbox("Show marker", ref Show);
        Setting.Slider("Radius", ref Radius, 2, 40);
        Setting.Color("Colour", ref Colour);
        Setting.Style("Status text", Label, "Style of the status line under the marker");
        Controls.Section("Hotkeys");
        Controls.PluginHotkeys();
    }
}
