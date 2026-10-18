using System;
using EmberHUD;
using Tartarus.Sim;

namespace Tartarus;

public sealed class Settings : PluginSettings
{
    public bool ShowPanel = true;
    public float PanelX = 0.02f;
    public float PanelY = 0.3f;
    public bool ShowAssumed = true;
    public int Playouts = 300;
    public double CloseMargin = 0.1;
    public int ChromaticCap = 10;
    public bool EaterKeepsBeast;
    public int SecondMealFromTier = 3;
    public bool ReplaceIsExecute;
    public int EventChance = 5;

    internal int ClampedPlayouts => Math.Clamp(Playouts, 20, 2000);

    internal Rules ToRules() => new() { ChromaticCap = Math.Clamp(ChromaticCap, 1, 30), EaterKeepsBeast = EaterKeepsBeast, SecondMealFromTier = SecondMealFromTier, ReplaceIsExecute = ReplaceIsExecute, EventChance = Math.Clamp(EventChance, 0, 100) };

    public override void Draw() { }
}
