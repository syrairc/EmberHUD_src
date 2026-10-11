using System.Numerics;
using Hexa.NET.ImGui;
using TorchlightSDK;

namespace Sample;

public sealed class Main : Plugin<Settings>
{
    public override void Tick()
    {
        if (Settings.Display.Toggle.Pressed) Settings.Display.Show.Value = !Settings.Display.Show.Value;
    }

    public override void Render()
    {
        if (!Settings.Display.Show.Value) return;
        var centre = ImGui.GetIO().DisplaySize / 2;
        var radius = Settings.Display.Radius.Value;
        Draw.Marker(centre, radius, Settings.Display.Colour.Value);
        Draw.Text(StatusText(Game.State), centre + new Vector2(0, radius + 6), Settings.Display.Label, Align.Center);
    }

    internal static string StatusText(GameState state) => $"Status: {state.Status}";
}
