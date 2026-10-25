namespace EmberHUD.Api.Tests;

public sealed class HumanizerPresetsTests
{
    [Fact]
    public void FromSpeedAt100IsTheDefaultConfig()
    {
        Assert.Equal(new HumanizerConfig(), HumanizerPresets.FromSpeed(100));
    }

    [Theory]
    [InlineData(25, 16, 36, 80, 240, 160, 360, 120, 360)]
    [InlineData(50, 8, 18, 40, 120, 80, 180, 60, 180)]
    [InlineData(70, 6, 13, 29, 86, 57, 129, 43, 129)]
    [InlineData(150, 3, 6, 13, 40, 27, 60, 20, 60)]
    [InlineData(225, 2, 4, 9, 27, 18, 40, 13, 40)]
    [InlineData(300, 1, 3, 7, 20, 13, 30, 10, 30)]
    public void FromSpeedScalesEveryDelayAndKeepsTheRest(int speed, int stepMin, int stepMax, int preMin, int preMax, int dwellMin, int dwellMax, int postMin, int postMax)
    {
        var c = HumanizerPresets.FromSpeed(speed);
        Assert.Equal((stepMin, stepMax), (c.StepDelayMin, c.StepDelayMax));
        Assert.Equal((preMin, preMax), (c.PreClickSettleMin, c.PreClickSettleMax));
        Assert.Equal((dwellMin, dwellMax), (c.ClickDwellMin, c.ClickDwellMax));
        Assert.Equal((postMin, postMax), (c.PostClickSettleMin, c.PostClickSettleMax));
        Assert.Equal(c with { StepDelayMin = 4, StepDelayMax = 9, PreClickSettleMin = 20, PreClickSettleMax = 60, ClickDwellMin = 40, ClickDwellMax = 90, PostClickSettleMin = 30, PostClickSettleMax = 90 }, new HumanizerConfig());
    }

    [Fact]
    public void FromSpeedClamps()
    {
        Assert.Equal(HumanizerPresets.FromSpeed(25), HumanizerPresets.FromSpeed(10));
        Assert.Equal(HumanizerPresets.FromSpeed(300), HumanizerPresets.FromSpeed(999));
    }

    [Theory]
    [InlineData(HumanizerPreset.VerySlow, 50)]
    [InlineData(HumanizerPreset.Slow, 70)]
    [InlineData(HumanizerPreset.Normal, 100)]
    [InlineData(HumanizerPreset.Fast, 150)]
    [InlineData(HumanizerPreset.VeryFast, 225)]
    public void NamedPresetsAreFromTheirAnchorSpeeds(HumanizerPreset preset, int speed)
    {
        Assert.Equal(speed, HumanizerPresets.SpeedOf(preset));
        Assert.Equal(HumanizerPresets.FromSpeed(speed), HumanizerPresets.Get(preset));
    }

    [Theory]
    [InlineData(138, 52, 108)]
    [InlineData(827, 350, 650)]
    public void NormalAveragesInTheBand(int distance, double min, double max) => Assert.InRange(AverageDurationMs(HumanizerPreset.Normal, distance), min, max);

    [Fact]
    public void VeryFastAndVerySlowAveragesInTheBand()
    {
        Assert.InRange(AverageDurationMs(HumanizerPreset.VeryFast, 827), 150, 300);
        Assert.InRange(AverageDurationMs(HumanizerPreset.VerySlow, 827), 750, 1300);
    }

    [Theory]
    [InlineData(138)]
    [InlineData(827)]
    public void AverageDurationRisesFromVeryFastToVerySlow(int distance)
    {
        var durations = new[] { HumanizerPreset.VeryFast, HumanizerPreset.Fast, HumanizerPreset.Normal, HumanizerPreset.Slow, HumanizerPreset.VerySlow }
            .Select(preset => AverageDurationMs(preset, distance)).ToArray();
        Assert.True(durations.Zip(durations.Skip(1), (fast, slow) => fast < slow).All(x => x), string.Join(" < ", durations));
    }

    static double AverageDurationMs(HumanizerPreset preset, int distance)
    {
        var config = HumanizerPresets.Get(preset);
        var target = new MetricsRect(100 + distance - 2, 98, 100 + distance + 3, 103);
        return Enumerable.Range(1, 40).Average(seed =>
        {
            var path = MousePath.Generate((100, 100), (100 + distance, 100), config, new Random(seed));
            return PathMetrics.Measure(PathMetrics.FromWaypoints((100, 100), path), target).DurationMs;
        });
    }

    [Fact]
    public void NamedListHasTheFiveInOrder()
    {
        Assert.Equal(
            new[] { HumanizerPreset.VerySlow, HumanizerPreset.Slow, HumanizerPreset.Normal, HumanizerPreset.Fast, HumanizerPreset.VeryFast },
            HumanizerPresets.Named);
    }

    [Fact]
    public void GetCustomThrows()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => HumanizerPresets.Get(HumanizerPreset.Custom));
        Assert.Throws<ArgumentOutOfRangeException>(() => HumanizerPresets.SpeedOf(HumanizerPreset.Custom));
    }
}
