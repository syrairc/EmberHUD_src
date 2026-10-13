using EmberHUD.Host.Ui;

namespace EmberHUD.Tests;

public sealed class SdkSettingsTests
{
    [Fact]
    public void GameIconsAreOffByDefault() => Assert.Equal("", new SdkSettings().GameIconsDirectory);

    [Fact]
    public void TheDirectoryComesFromTheSettingsFile()
    {
        var settings = (SdkSettings)SettingsJson.Load(typeof(SdkSettings), null, """{"GameIconsDirectory":"D:\\icons"}""", _ => { });

        Assert.Equal(@"D:\icons", settings.GameIconsDirectory);
    }
}
