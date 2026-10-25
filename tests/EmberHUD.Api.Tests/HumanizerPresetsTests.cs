namespace EmberHUD.Api.Tests;

public sealed class HumanizerPresetsTests
{
    [Fact]
    public void FromSpeedAt100IsTheOldHostDefault()
    {
        var c = HumanizerPresets.FromSpeed(100);
        Assert.Equal(40f, c.MaxStep);
        Assert.Equal((0, 3), (c.StepDelayMin, c.StepDelayMax));
        Assert.Equal((6, 18), (c.PreClickSettleMin, c.PreClickSettleMax));
        Assert.Equal((12, 30), (c.ClickDwellMin, c.ClickDwellMax));
        Assert.Equal((6, 18), (c.PostClickSettleMin, c.PostClickSettleMax));
    }

    [Fact]
    public void FromSpeedAt75AndAt35()
    {
        var normal = HumanizerPresets.FromSpeed(75);
        Assert.Equal(30f, normal.MaxStep);
        Assert.Equal((0, 4), (normal.StepDelayMin, normal.StepDelayMax));
        Assert.Equal((8, 24), (normal.PreClickSettleMin, normal.PreClickSettleMax));
        Assert.Equal((16, 40), (normal.ClickDwellMin, normal.ClickDwellMax));
        Assert.Equal((8, 24), (normal.PostClickSettleMin, normal.PostClickSettleMax));
        var verySlow = HumanizerPresets.FromSpeed(35);
        Assert.Equal(14f, verySlow.MaxStep);
        Assert.Equal((0, 9), (verySlow.StepDelayMin, verySlow.StepDelayMax));
        Assert.Equal((17, 51), (verySlow.PreClickSettleMin, verySlow.PreClickSettleMax));
        Assert.Equal((34, 86), (verySlow.ClickDwellMin, verySlow.ClickDwellMax));
        Assert.Equal((17, 51), (verySlow.PostClickSettleMin, verySlow.PostClickSettleMax));
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
        Assert.Equal(HumanizerPresets.FromSpeed(25), HumanizerPresets.FromSpeed(10));
        Assert.Equal(HumanizerPresets.FromSpeed(150), HumanizerPresets.FromSpeed(999));
    }

    [Theory]
    [InlineData(HumanizerPreset.VerySlow, 35)]
    [InlineData(HumanizerPreset.Slow, 50)]
    [InlineData(HumanizerPreset.Normal, 75)]
    [InlineData(HumanizerPreset.Fast, 100)]
    [InlineData(HumanizerPreset.VeryFast, 125)]
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
