using System.Numerics;
using EmberHUD;

namespace GroundRing;

public sealed class Main : Plugin<Settings>
{
    public override void Initialize() => RegisterHotkey("Toggle", "Shows or hides the rings", () => Settings.Show = !Settings.Show, new Hotkey(Key.F9));

    public override void Render()
    {
        var state = Game.State;
        if (!Settings.Show || Settings.HideBehindPanels && state.Ui.AnyFullScreen) return;
        foreach (var entity in state.Entities)
        {
            if (entity.Kind != EntityKind.Monster || entity.Friendly || entity.Hidden) continue;
            Ground.Circle(new Vector2(entity.Position.X, entity.Position.Y), Settings.WorldRadius, Settings.Colour, Settings.Thickness, entity.Position.Z);
        }
    }
}
