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

    public override void OnDisable() => Send.ReleaseAll();

    public override void Dispose() => Send.ReleaseAll();
}
