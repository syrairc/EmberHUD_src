
namespace EmberHUD.Host.Ui;

internal sealed class InputSettings
{
    public MouseMode Mode = MouseMode.Humanized;
    public int SpeedPercent = 100;
    public float MaxStep = 15;
    public int StepDelayMin = 4;
    public int StepDelayMax = 9;
    public int PreClickSettleMin = 20;
    public int PreClickSettleMax = 60;
    public int ClickDwellMin = 40;
    public int ClickDwellMax = 90;
    public int PostClickSettleMin = 30;
    public int PostClickSettleMax = 90;

    public HumanizerPreset Preset = HumanizerPreset.Normal;

    public void ApplySpeed(int speedPercent)
    {
        SpeedPercent = Math.Clamp(speedPercent, 25, 300);
        var row = HumanizerPresets.FromSpeed(SpeedPercent);
        MaxStep = row.MaxStep;
        StepDelayMin = row.StepDelayMin; StepDelayMax = row.StepDelayMax;
        PreClickSettleMin = row.PreClickSettleMin; PreClickSettleMax = row.PreClickSettleMax;
        ClickDwellMin = row.ClickDwellMin; ClickDwellMax = row.ClickDwellMax;
        PostClickSettleMin = row.PostClickSettleMin; PostClickSettleMax = row.PostClickSettleMax;
    }

    public void ApplyPreset(HumanizerPreset preset)
    {
        if (preset != HumanizerPreset.Custom) ApplySpeed(HumanizerPresets.SpeedOf(preset));
        Preset = preset;
    }

    public void MarkEdited() => Preset = HumanizerPreset.Custom;

    public void MatchPreset()
    {
        var current = ToConfig();
        bool Matches(HumanizerPreset preset) => preset != HumanizerPreset.Custom && HumanizerPresets.Get(preset).Clamped() == current;
        if (Matches(Preset)) return;
        Preset = HumanizerPresets.Named.Cast<HumanizerPreset?>().FirstOrDefault(preset => Matches(preset!.Value)) ?? HumanizerPreset.Custom;
    }

    public void Reset()
    {
        Mode = MouseMode.Humanized;
        ApplyPreset(HumanizerPreset.Normal);
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
