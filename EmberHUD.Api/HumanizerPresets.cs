namespace EmberHUD;

public enum HumanizerPreset { VerySlow, Slow, Normal, Fast, VeryFast, Custom }

public static class HumanizerPresets
{
    public static IReadOnlyList<HumanizerPreset> Named { get; } =
        [HumanizerPreset.VerySlow, HumanizerPreset.Slow, HumanizerPreset.Normal, HumanizerPreset.Fast, HumanizerPreset.VeryFast];

    public static HumanizerConfig FromSpeed(int speedPercent)
    {
        var speed = Math.Clamp(speedPercent, 25, 150);
        int Delay(int atFullSpeed) => (int)Math.Round(atFullSpeed * 150.0 / speed);
        return new HumanizerConfig
        {
            MaxStep = (float)Math.Round(60.0 * speed / 150, 1),
            StepDelayMin = Delay(0), StepDelayMax = Delay(2),
            PreClickSettleMin = Delay(4), PreClickSettleMax = Delay(12),
            ClickDwellMin = Delay(8), ClickDwellMax = Delay(20),
            PostClickSettleMin = Delay(4), PostClickSettleMax = Delay(12),
        };
    }

    public static HumanizerConfig Get(HumanizerPreset preset) => FromSpeed(SpeedOf(preset));

    public static int SpeedOf(HumanizerPreset preset) => preset switch
    {
        HumanizerPreset.VerySlow => 35,
        HumanizerPreset.Slow => 50,
        HumanizerPreset.Normal => 75,
        HumanizerPreset.Fast => 100,
        HumanizerPreset.VeryFast => 125,
        _ => throw new ArgumentOutOfRangeException(nameof(preset), preset, null),
    };
}
