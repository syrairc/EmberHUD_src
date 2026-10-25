using EmberHUD.Host.Ui;

namespace EmberHUD.Tests;

public class InputSettingsTests
{
    [Theory]
    [InlineData(150, 60, 0, 2, 4, 12, 8, 20, 4, 12)]
    [InlineData(100, 40, 0, 3, 6, 18, 12, 30, 6, 18)]
    [InlineData(50, 20, 0, 6, 12, 36, 24, 60, 12, 36)]
    public void ApplySpeedMatchesExileTradeAt50100And150(int speed, float maxStep, int stepMin, int stepMax, int preMin, int preMax, int dwellMin, int dwellMax, int postMin, int postMax)
    {
        var input = new InputSettings();
        input.ApplySpeed(speed);
        Assert.Equal(maxStep, input.MaxStep);
        Assert.Equal((stepMin, stepMax), (input.StepDelayMin, input.StepDelayMax));
        Assert.Equal((preMin, preMax), (input.PreClickSettleMin, input.PreClickSettleMax));
        Assert.Equal((dwellMin, dwellMax), (input.ClickDwellMin, input.ClickDwellMax));
        Assert.Equal((postMin, postMax), (input.PostClickSettleMin, input.PostClickSettleMax));
    }

    [Fact]
    public void ApplySpeedClampsTheSpeedAndKeepsItInTheRange()
    {
        var input = new InputSettings();
        input.ApplySpeed(10);
        Assert.Equal(50, input.SpeedPercent);
        Assert.Equal(20, input.MaxStep);
        input.ApplySpeed(999);
        Assert.Equal(150, input.SpeedPercent);
        Assert.Equal(60, input.MaxStep);
    }

    [Fact]
    public void DefaultsAreHumanizedAt100Percent()
    {
        var fresh = new InputSettings();
        var applied = new InputSettings();
        applied.ApplySpeed(100);
        Assert.Equal(MouseMode.Humanized, fresh.Mode);
        Assert.Equal(100, fresh.SpeedPercent);
        Assert.Equal(applied.ToConfig(), fresh.ToConfig());
        Assert.Equal(applied.MaxStep, fresh.MaxStep);
    }

    [Fact]
    public void RoundTripsThroughSettingsJson()
    {
        var sdk = new SdkSettings();
        sdk.Input.Mode = MouseMode.Direct;
        sdk.Input.ApplySpeed(70);
        var loaded = (SdkSettings)SettingsJson.Load(typeof(SdkSettings), null, SettingsJson.Serialize(sdk), _ => { });
        Assert.Equal(MouseMode.Direct, loaded.Input.Mode);
        Assert.Equal(70, loaded.Input.SpeedPercent);
        Assert.Equal(sdk.Input.ToConfig(), loaded.Input.ToConfig());
    }

    [Fact]
    public void OldSdkJsonWithoutInputLoadsDefaults()
    {
        var loaded = (SdkSettings)SettingsJson.Load(typeof(SdkSettings), null, "{}", _ => { });
        Assert.Equal(MouseMode.Humanized, loaded.Input.Mode);
        Assert.Equal(100, loaded.Input.SpeedPercent);
        Assert.Equal(40, loaded.Input.MaxStep);
    }

    [Fact]
    public void RawEditsSurviveSaveAndLoadWithoutTouchingSpeed()
    {
        var sdk = new SdkSettings();
        sdk.Input.ClickDwellMin = 33;
        var loaded = (SdkSettings)SettingsJson.Load(typeof(SdkSettings), null, SettingsJson.Serialize(sdk), _ => { });
        Assert.Equal(33, loaded.Input.ClickDwellMin);
        Assert.Equal(100, loaded.Input.SpeedPercent);
    }

    [Fact]
    public void ResetRestoresEverything()
    {
        var input = new InputSettings { Mode = MouseMode.Direct, ClickDwellMin = 77 };
        input.ApplySpeed(60);
        input.Reset();
        Assert.Equal(new InputSettings().ToConfig(), input.ToConfig());
        Assert.Equal(MouseMode.Humanized, input.Mode);
        Assert.Equal(100, input.SpeedPercent);
    }

    [Fact]
    public void ToConfigClampsBadRanges()
    {
        var config = new InputSettings { MaxStep = 0, StepDelayMin = 9, StepDelayMax = 2, PreClickSettleMin = -5, ClickDwellMin = 50, ClickDwellMax = 10 }.ToConfig();
        Assert.True(config.MaxStep >= 1);
        Assert.True(config.StepDelayMin <= config.StepDelayMax);
        Assert.True(config.PreClickSettleMin >= 0);
        Assert.True(config.ClickDwellMin <= config.ClickDwellMax);
    }
}
