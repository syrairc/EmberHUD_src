using System.Runtime.CompilerServices;
using Aember.Host.Plugins;

namespace Aember.Tests;

public sealed class FirstPartyPluginTests
{
    static string Here([CallerFilePath] string path = "") => path;

    [Fact]
    public void EveryShippedPluginCompilesAndPassesItsTests()
    {
        var report = new StringWriter();
        var code = CheckRunner.Run(Path.GetFullPath(Path.Combine(Path.GetDirectoryName(Here())!, "..", "..", "..", "Plugins")), report);
        Assert.True(code == 0, report.ToString());
        Assert.Contains("PASS Sample compile", report.ToString());
        Assert.Contains("PASS Sample SampleTests.StatusTextShowsTheGameStatus", report.ToString());
        Assert.Contains("PASS Sample SampleTests.TickTogglesVisibilityOnTheHotkey", report.ToString());
        Assert.DoesNotContain(" defaults - ", report.ToString());
    }
}
