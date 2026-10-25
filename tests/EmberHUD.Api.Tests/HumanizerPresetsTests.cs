namespace EmberHUD.Api.Tests;

public sealed class HumanizerPresetsTests
{
    [Fact]
    public void FromSpeedMatchesTheHostDefaultsAt100()
    {
        var c = HumanizerPresets.FromSpeed(100);
        Assert.Equal(40f, c.MaxStep);
        Assert.Equal((0, 3), (c.StepDelayMin, c.StepDelayMax));
        Assert.Equal((6, 18), (c.PreClickSettleMin, c.PreClickSettleMax));
        Assert.Equal((12, 30), (c.ClickDwellMin, c.ClickDwellMax));
        Assert.Equal((6, 18), (c.PostClickSettleMin, c.PostClickSettleMax));
    }

    [Fact]
    public void FromSpeedAt50And150()
    {
        var slow = HumanizerPresets.FromSpeed(50);
        Assert.Equal(20f, slow.MaxStep);
        Assert.Equal((0, 6), (slow.StepDelayMin, slow.StepDelayMax));
        Assert.Equal((12, 36), (slow.PreClickSettleMin, slow.PreClickSettleMax));
        Assert.Equal((24, 60), (slow.ClickDwellMin, slow.ClickDwellMax));
        Assert.Equal((12, 36), (slow.PostClickSettleMin, slow.PostClickSettleMax));
        var fast = HumanizerPresets.FromSpeed(150);
        Assert.Equal(60f, fast.MaxStep);
        Assert.Equal((0, 2), (fast.StepDelayMin, fast.StepDelayMax));
        Assert.Equal((4, 12), (fast.PreClickSettleMin, fast.PreClickSettleMax));
        Assert.Equal((8, 20), (fast.ClickDwellMin, fast.ClickDwellMax));
        Assert.Equal((4, 12), (fast.PostClickSettleMin, fast.PostClickSettleMax));
    }

    [Fact]
    public void FromSpeedClamps()
    {
        Assert.Equal(HumanizerPresets.FromSpeed(50), HumanizerPresets.FromSpeed(10));
        Assert.Equal(HumanizerPresets.FromSpeed(150), HumanizerPresets.FromSpeed(999));
    }

    [Theory]
    [InlineData(HumanizerPreset.VerySlow, 50)]
    [InlineData(HumanizerPreset.Slow, 75)]
    [InlineData(HumanizerPreset.Normal, 100)]
    [InlineData(HumanizerPreset.Fast, 125)]
    [InlineData(HumanizerPreset.VeryFast, 150)]
    public void NamedPresetsAreFromTheirAnchorSpeeds(HumanizerPreset preset, int speed)
    {
        Assert.Equal(speed, HumanizerPresets.SpeedOf(preset));
        Assert.Equal(HumanizerPresets.FromSpeed(speed), HumanizerPresets.Get(preset));
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
