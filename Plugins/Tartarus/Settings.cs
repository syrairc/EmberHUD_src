using System;
using EmberHUD;
using Tartarus.Sim;

namespace Tartarus;

public sealed class Settings : PluginSettings
{
    public bool ShowPanel = true;
    public bool Lock = true;
    public Hotkey MoveKey = new(Key.Alt);
    public int PanelX = 40;
    public int PanelY = 430;
    public bool HighlightCards = true;
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
