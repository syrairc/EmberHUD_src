namespace EmberHUD;

public enum HumanizerPreset { VerySlow, Slow, Normal, Fast, VeryFast, Custom }

public static class HumanizerPresets
{
    public static IReadOnlyList<HumanizerPreset> Named { get; } =
        [HumanizerPreset.VerySlow, HumanizerPreset.Slow, HumanizerPreset.Normal, HumanizerPreset.Fast, HumanizerPreset.VeryFast];

    public static HumanizerConfig FromSpeed(int speedPercent)
    {
        var speed = Math.Clamp(speedPercent, 25, 300);
        int Delay(int atNormal) => (int)Math.Round(atNormal * 100.0 / speed);
        var normal = new HumanizerConfig();
        return normal with
        {
            StepDelayMin = Delay(normal.StepDelayMin), StepDelayMax = Delay(normal.StepDelayMax),
            PreClickSettleMin = Delay(normal.PreClickSettleMin), PreClickSettleMax = Delay(normal.PreClickSettleMax),
            ClickDwellMin = Delay(normal.ClickDwellMin), ClickDwellMax = Delay(normal.ClickDwellMax),
            PostClickSettleMin = Delay(normal.PostClickSettleMin), PostClickSettleMax = Delay(normal.PostClickSettleMax),
        };
    }

    public static HumanizerConfig Get(HumanizerPreset preset) => FromSpeed(SpeedOf(preset));

    public static int SpeedOf(HumanizerPreset preset) => preset switch
    {
        HumanizerPreset.VerySlow => 50,
        HumanizerPreset.Slow => 70,
        HumanizerPreset.Normal => 100,
        HumanizerPreset.Fast => 150,
        HumanizerPreset.VeryFast => 225,
        _ => throw new ArgumentOutOfRangeException(nameof(preset), preset, null),
    };
}
