using System.Drawing;
using TorchlightSDK;
using TorchlightSDK.Ui;

namespace Sample;

public sealed class Settings : PluginSettings
{
    public DisplayTab Display = new();

    public override void Draw() => SettingsDrawer.DrawNodes(Display);
}

public sealed class DisplayTab
{
    [Menu("Show marker")] public ToggleNode Show = new(true);
    [Menu("Toggle", "Shows or hides the marker")] public HotkeyNode Toggle = new(new Hotkey(Key.F8));
    public RangeNode<float> Radius = new(8, 2, 40);
    public ColorNode Colour = new(Color.White);
    [Menu("Status text", "Style of the status line under the marker")] public TextStyle Label = TextStyle.WorldLabel();
}
