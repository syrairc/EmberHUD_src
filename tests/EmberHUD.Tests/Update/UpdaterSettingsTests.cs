using EmberHUD.Host.Ui;

namespace EmberHUD.Tests;

public class UpdaterSettingsTests
{
    [Fact]
    public void SdkSettingsRoundTripsTheUpdaterFields()
    {
        var sdk = new SdkSettings { UpdaterEnabled = false, UpdateIntervalHours = 24, OverwriteModifiedPlugins = true };
        var loaded = (SdkSettings)SettingsJson.Load(typeof(SdkSettings), null, SettingsJson.Serialize(sdk), _ => { });
        Assert.False(loaded.UpdaterEnabled);
        Assert.Equal(24, loaded.UpdateIntervalHours);
        Assert.True(loaded.OverwriteModifiedPlugins);
    }

    [Fact]
    public void SdkSettingsDefaultsAreOnOneHourKeep()
    {
        var loaded = (SdkSettings)SettingsJson.Load(typeof(SdkSettings), null, null, _ => { });
        Assert.True(loaded.UpdaterEnabled);
        Assert.Equal(1, loaded.UpdateIntervalHours);
        Assert.False(loaded.OverwriteModifiedPlugins);
    }
}
