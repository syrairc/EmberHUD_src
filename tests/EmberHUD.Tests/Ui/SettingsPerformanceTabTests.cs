using EmberHUD.Host.Reading;
using EmberHUD.Host.Ui;
using static EmberHUD.Tests.PluginFixture;

namespace EmberHUD.Tests;

public sealed class SettingsPerformanceTabTests : SettingsWindowFixture
{
    const int PerformanceTab = 6;

    [Fact]
    public void ThePerformanceTabShowsASliderPerRate()
    {
        using var host = Discovered();
        var window = Window(host);
        OpenTab(window, PerformanceTab);
        Assert.True(Frames(Settled(window)).Count >= 12);
    }

    [Fact]
    public void ResetAllPutsTheDefaultsBackAndAppliesThem()
    {
        try
        {
            using var host = Discovered();
            var window = Window(host);
            _fixture.Sdk.Performance.FighterMilliseconds = 900;
            _fixture.Sdk.Performance.TickMilliseconds = 100;
            Performance.Apply(_fixture.Sdk.Performance);
            OpenTab(window, PerformanceTab);
            var reset = Buttons(Settled(window)).Last();
            Click(window, reset.Centre);
            Assert.Equal(PerformanceSettings.Defaults.FighterMilliseconds, _fixture.Sdk.Performance.FighterMilliseconds);
            Assert.Equal(PerformanceSettings.Defaults.TickMilliseconds, Performance.TickMilliseconds);
        }
        finally { Performance.Reset(); }
    }
}
