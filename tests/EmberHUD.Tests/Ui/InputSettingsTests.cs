using EmberHUD.Host.Ui;

namespace EmberHUD.Tests;

public class InputSettingsTests
{
    [Theory]
    [InlineData(150, 60, 0, 2, 4, 12, 8, 20, 4, 12)]
    [InlineData(75, 30, 0, 4, 8, 24, 16, 40, 8, 24)]
    [InlineData(35, 14, 0, 9, 17, 51, 34, 86, 17, 51)]
    [InlineData(100, 40, 0, 3, 6, 18, 12, 30, 6, 18)]
    [InlineData(50, 20, 0, 6, 12, 36, 24, 60, 12, 36)]
    public void ApplySpeedMatchesTheFormulaAtTheAnchors(int speed, float maxStep, int stepMin, int stepMax, int preMin, int preMax, int dwellMin, int dwellMax, int postMin, int postMax)
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
        Assert.Equal(25, input.SpeedPercent);
        Assert.Equal(10, input.MaxStep);
        input.ApplySpeed(999);
        Assert.Equal(150, input.SpeedPercent);
        Assert.Equal(60, input.MaxStep);
    }

    [Fact]
    public void DefaultsAreHumanizedAtTheNormalPreset()
    {
        var fresh = new InputSettings();
        var applied = new InputSettings();
        applied.ApplySpeed(75);
        Assert.Equal(MouseMode.Humanized, fresh.Mode);
        Assert.Equal(75, fresh.SpeedPercent);
        Assert.Equal(HumanizerPreset.Normal, fresh.Preset);
        Assert.Equal(HumanizerPresets.Get(HumanizerPreset.Normal).Clamped(), fresh.ToConfig());
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
        Assert.Equal(75, loaded.Input.SpeedPercent);
        Assert.Equal(30, loaded.Input.MaxStep);
    }

    [Fact]
    public void RawEditsSurviveSaveAndLoadWithoutTouchingSpeed()
    {
        var sdk = new SdkSettings();
        sdk.Input.ClickDwellMin = 33;
        var loaded = (SdkSettings)SettingsJson.Load(typeof(SdkSettings), null, SettingsJson.Serialize(sdk), _ => { });
        Assert.Equal(33, loaded.Input.ClickDwellMin);
        Assert.Equal(75, loaded.Input.SpeedPercent);
    }

    [Fact]
    public void ResetRestoresEverything()
    {
        var input = new InputSettings { Mode = MouseMode.Direct, ClickDwellMin = 77 };
        input.ApplySpeed(60);
        input.Reset();
        Assert.Equal(new InputSettings().ToConfig(), input.ToConfig());
        Assert.Equal(MouseMode.Humanized, input.Mode);
        Assert.Equal(75, input.SpeedPercent);
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

    [Theory]
    [InlineData(HumanizerPreset.VerySlow)]
    [InlineData(HumanizerPreset.Slow)]
    [InlineData(HumanizerPreset.Normal)]
    [InlineData(HumanizerPreset.Fast)]
    [InlineData(HumanizerPreset.VeryFast)]
    public void ApplyPresetCopiesTheRowAndSetsSpeed(HumanizerPreset preset)
    {
        var input = new InputSettings { ClickDwellMin = 77 };
        input.ApplyPreset(preset);
        Assert.Equal(preset, input.Preset);
        Assert.Equal(HumanizerPresets.SpeedOf(preset), input.SpeedPercent);
        Assert.Equal(HumanizerPresets.Get(preset).Clamped(), input.ToConfig());
    }

    [Fact]
    public void ApplyPresetCustomOnlySetsTheName()
    {
        var input = new InputSettings { ClickDwellMin = 33, SpeedPercent = 70 };
        input.ApplyPreset(HumanizerPreset.Custom);
        Assert.Equal(HumanizerPreset.Custom, input.Preset);
        Assert.Equal(33, input.ClickDwellMin);
        Assert.Equal(70, input.SpeedPercent);
    }

    [Fact]
    public void EditingARawRangeOrSpeedMarksCustom()
    {
        var input = new InputSettings();
        Assert.Equal(HumanizerPreset.Normal, input.Preset);
        input.MarkEdited();
        Assert.Equal(HumanizerPreset.Custom, input.Preset);
        input.ApplyPreset(HumanizerPreset.Fast);
        input.ApplySpeed(90);
        input.MarkEdited();
        Assert.Equal(HumanizerPreset.Custom, input.Preset);
    }

    [Fact]
    public void MatchPresetKeepsAnExactMatch()
    {
        var input = new InputSettings();
        input.ApplyPreset(HumanizerPreset.Slow);
        input.MatchPreset();
        Assert.Equal(HumanizerPreset.Slow, input.Preset);
    }

    [Fact]
    public void MatchPresetRecognisesAnUnnamedMatchingRow()
    {
        var input = new InputSettings();
        input.ApplyPreset(HumanizerPreset.Fast);
        input.Preset = HumanizerPreset.Custom;
        input.MatchPreset();
        Assert.Equal(HumanizerPreset.Fast, input.Preset);
    }

    [Fact]
    public void MatchPresetRecognisesDefaultsAsNormal()
    {
        var fresh = new InputSettings();
        fresh.MatchPreset();
        Assert.Equal(HumanizerPreset.Normal, fresh.Preset);
        var old = (SdkSettings)SettingsJson.Load(typeof(SdkSettings), null, "{\"Input\":{\"Mode\":\"Direct\"}}", _ => { });
        old.Input.MatchPreset();
        Assert.Equal(HumanizerPreset.Normal, old.Input.Preset);
    }

    [Fact]
    public void MatchPresetFallsBackToCustomAndKeepsTheUsersValues()
    {
        var input = new InputSettings { ClickDwellMin = 33, Preset = HumanizerPreset.Slow };
        input.MatchPreset();
        Assert.Equal(HumanizerPreset.Custom, input.Preset);
        Assert.Equal(33, input.ClickDwellMin);
        Assert.Equal(75, input.SpeedPercent);
    }

    [Fact]
    public void OldDefaultValuesLoadAsFastAndAreKept()
    {
        var json = "{\"Input\":{\"SpeedPercent\":100,\"MaxStep\":40,\"StepDelayMin\":0,\"StepDelayMax\":3,\"PreClickSettleMin\":6,\"PreClickSettleMax\":18,\"ClickDwellMin\":12,\"ClickDwellMax\":30,\"PostClickSettleMin\":6,\"PostClickSettleMax\":18}}";
        var loaded = (SdkSettings)SettingsJson.Load(typeof(SdkSettings), null, json, _ => { });
        loaded.Input.MatchPreset();
        Assert.Equal(HumanizerPreset.Fast, loaded.Input.Preset);
        Assert.Equal(40, loaded.Input.MaxStep);
        Assert.Equal(100, loaded.Input.SpeedPercent);
        Assert.Equal((12, 30), (loaded.Input.ClickDwellMin, loaded.Input.ClickDwellMax));
    }

    [Fact]
    public void UnknownPresetNameInJsonLoadsAsCustomOrMatch()
    {
        var loaded = (SdkSettings)SettingsJson.Load(typeof(SdkSettings), null, "{\"Input\":{\"Preset\":\"Bogus\"}}", _ => { });
        loaded.Input.MatchPreset();
        Assert.Equal(HumanizerPreset.Normal, loaded.Input.Preset);
        var edited = (SdkSettings)SettingsJson.Load(typeof(SdkSettings), null, "{\"Input\":{\"Preset\":\"Bogus\",\"ClickDwellMin\":33}}", _ => { });
        edited.Input.MatchPreset();
        Assert.Equal(HumanizerPreset.Custom, edited.Input.Preset);
        Assert.Equal(33, edited.Input.ClickDwellMin);
    }

    [Fact]
    public void PresetRoundTripsThroughSettingsJson()
    {
        var sdk = new SdkSettings();
        sdk.Input.ApplyPreset(HumanizerPreset.VerySlow);
        var json = SettingsJson.Serialize(sdk);
        Assert.Contains("\"VerySlow\"", json);
        var loaded = (SdkSettings)SettingsJson.Load(typeof(SdkSettings), null, json, _ => { });
        loaded.Input.MatchPreset();
        Assert.Equal(HumanizerPreset.VerySlow, loaded.Input.Preset);
        Assert.Equal(35, loaded.Input.SpeedPercent);
    }
}
