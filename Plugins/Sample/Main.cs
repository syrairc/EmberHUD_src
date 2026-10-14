using System.Numerics;
using Hexa.NET.ImGui;
using EmberHUD;

namespace Sample;

public sealed class Main : Plugin<Settings>
{
    public override void Initialize() => RegisterHotkey("Toggle", "Shows or hides the marker", () => Settings.Show = !Settings.Show, new Hotkey(Key.F8));

    public override void Render()
    {
        if (!Settings.Show) return;
        var centre = ImGui.GetIO().DisplaySize / 2;
        var radius = Settings.Radius;
        Draw.Marker(centre, radius, Settings.Colour);
        Draw.Text(StatusText(Game.State), centre + new Vector2(0, radius + 6), Settings.Label, Align.Center);
    }

    internal static string StatusText(GameState state) => $"Status: {state.Status}";
}
