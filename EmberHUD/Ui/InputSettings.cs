using System.Reflection;

namespace EmberHUD.Host.Ui;

[Obfuscation(Exclude = true, ApplyToMembers = true)]
internal sealed class InputSettings
{
    public MouseMode Mode = MouseMode.Humanized;
    public int SpeedPercent = 100;
    public float MaxStep = 40;
    public int StepDelayMin = 0;
    public int StepDelayMax = 3;
    public int PreClickSettleMin = 6;
    public int PreClickSettleMax = 18;
    public int ClickDwellMin = 12;
    public int ClickDwellMax = 30;
    public int PostClickSettleMin = 6;
    public int PostClickSettleMax = 18;

    public void ApplySpeed(int speedPercent)
    {
        SpeedPercent = Math.Clamp(speedPercent, 50, 150);
        int Delay(int atFullSpeed) => (int)Math.Round(atFullSpeed * 150.0 / SpeedPercent);
        MaxStep = (float)Math.Round(60.0 * SpeedPercent / 150, 1);
        StepDelayMin = Delay(0); StepDelayMax = Delay(2);
        PreClickSettleMin = Delay(4); PreClickSettleMax = Delay(12);
        ClickDwellMin = Delay(8); ClickDwellMax = Delay(20);
        PostClickSettleMin = Delay(4); PostClickSettleMax = Delay(12);
    }

    public void Reset()
    {
        Mode = MouseMode.Humanized;
        ApplySpeed(100);
    }

    public HumanizerConfig ToConfig() => new HumanizerConfig()
    {
        MaxStep = MaxStep,
        StepDelayMin = StepDelayMin, StepDelayMax = StepDelayMax,
        PreClickSettleMin = PreClickSettleMin, PreClickSettleMax = PreClickSettleMax,
        ClickDwellMin = ClickDwellMin, ClickDwellMax = ClickDwellMax,
        PostClickSettleMin = PostClickSettleMin, PostClickSettleMax = PostClickSettleMax,
    }.Clamped();
}
