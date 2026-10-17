using System;
using EmberHUD;

namespace Pickit;

public sealed class Settings : PluginSettings
{
    public bool HoldKeyEnabled = true;
    public Hotkey HoldKey = new(Key.Space);
    public bool NearEnabled = true;
    public float NearRadius = 500;
    public Hotkey PickupKey = new(Key.A);
    public bool GemsEnabled = true;

    public void Normalize() => NearRadius = float.IsFinite(NearRadius) ? Math.Clamp(NearRadius, 50, 3000) : 500;

    public override void Draw() => SettingsUI.Draw(this);
}
