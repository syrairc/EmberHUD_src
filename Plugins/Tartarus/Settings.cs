using EmberHUD;

namespace Tartarus;

public sealed class Settings : PluginSettings
{
    public bool ShowPanel = true;
    public float PanelX = 0.02f;
    public float PanelY = 0.3f;
    public double TierBase = 3;
    public double RainbowMult = 1.5;
    public double ChainUpside = 1;
    public double CloseMargin = 0.1;
    public double UnsureWidthRatio = 1;
    public double PriorLowPercentile = 0.25;
    public double PriorHighPercentile = 0.75;
    public int LateDays = 2;
    public int StandardDays = 20;
    public double[] DoorPrior = [0.418, 0.359, 0.114, 0.092, 0.018, 0];
    public double[] ReplacePrior = [0.422, 0.333, 0.145, 0.073, 0.023, 0.003];

    public override void Draw() { }
}
