using EmberHUD;
using EmberHUD.Ui;

namespace Tarot;

public sealed class Settings : PluginSettings
{
    public bool ShowPanel = true;
    public float PanelX = 0.02f;
    public float PanelY = 0.3f;
    public int AheadTiles = 24;

    public override void Draw()
    {
        Setting.Checkbox("Show panel", ref ShowPanel, "The board card next to the screen");
        Setting.Slider("Panel X", ref PanelX, 0, 1, "Left edge, as a fraction of the screen width");
        Setting.Slider("Panel Y", ref PanelY, 0, 1, "Top edge, as a fraction of the screen height");
        Setting.Slider("Tiles ahead", ref AheadTiles, 4, 80, "How many tiles the grid shows, counted from the start of the current row of 4");
    }
}
