using EmberHUD;
using EmberHUD.Ui;

namespace Logging;

public sealed class Settings : PluginSettings
{
    public bool ShowPanel = true;
    public bool Lock = true;
    public Hotkey MoveKey = new(Key.Alt);
    public int PanelX = 40;
    public int PanelY = 40;
    public int Width = 900;
    public int Height = 360;
    public int MaxLines = 2000;
    public LogLevel DefaultLevel = LogLevel.Info;

    public override void Draw()
    {
        Setting.Checkbox("Show panel", ref ShowPanel);
        Setting.Checkbox("Lock", ref Lock, "A locked panel cannot be dragged or resized unless the move key is held - its buttons and filters stay usable");
        Setting.Hotkey("Move key", ref MoveKey, "Hold to move or resize the panel while locked");
        Setting.Slider("Width", ref Width, 300, 4000);
        Setting.Slider("Height", ref Height, 120, 2000);
        Setting.Slider("Max lines", ref MaxLines, 50, 20000, "Newest lines kept in the list");
        Setting.Choice("Default level", ref DefaultLevel, "Level the panel starts on, the host minimum is not changed");
        Controls.Section("Hotkeys");
        Controls.PluginHotkeys();
    }
}
