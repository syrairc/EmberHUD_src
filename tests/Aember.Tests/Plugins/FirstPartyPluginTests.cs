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
        Assert.Contains("PASS Inspector compile", report.ToString());
        Assert.Contains("PASS Inspector MainTests.FrozenHoldsTheSnapshotUntilResume", report.ToString());
        Assert.DoesNotContain(" defaults - ", report.ToString());
    }

    [Theory]
    [InlineData("Coffin")]
    [InlineData("Altar")]
    [InlineData("Elevator")]
    [InlineData("MapObject")]
    [InlineData("Destructible")]
    public void MinimapDefaultsShowEveryPreloadedMapEntity(string style)
    {
        var defaults = System.Text.Json.Nodes.JsonNode.Parse(File.ReadAllText(Path.Combine(Path.GetDirectoryName(Here())!, "..", "..", "..", "Plugins", "Minimap", "defaults.json")))!;
        var marker = defaults["Markers"]![style];
        Assert.NotNull(marker);
        Assert.NotEqual(false, marker["Show"]?.GetValue<bool>());
    }
}
