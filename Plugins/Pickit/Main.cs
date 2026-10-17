using System;
using System.Drawing;
using EmberHUD;

namespace Pickit;

public sealed class Main : Plugin<Settings>
{
    readonly Autoloot _autoloot = new();

    public override void Initialize() => Settings.Normalize();

    public override void Tick()
    {
        Settings.Normalize();
        if (Settings.NearEnabled || Settings.GemsEnabled) Game.WantGroundItems();
        _autoloot.Step(Settings, Game.State, Input.IsDown, Send.Down, Send.Up, Send.Allowed);
    }

    public override void Render()
    {
        if (!Settings.GemsEnabled || Game.State.GroundItems is not { } items || Game.State.Ui.AnyFullScreen) return;
        foreach (var item in items)
        {
            if (Settings.DebugRects) EmberHUD.Draw.Rect(item.LabelMin, item.LabelMax, Color.Red);
            foreach (var gem in Gems.Layout(item, Settings))
            {
                var style = Settings.Tiers[Math.Clamp(gem.Tier, 0, Settings.Tiers.Count - 1)];
                EmberHUD.Draw.Icon(style.Icon, gem.Centre, gem.Radius, style.Color);
            }
        }
    }

    public override void OnDisable() => Send.ReleaseAll();

    public override void Dispose() => Send.ReleaseAll();
}
