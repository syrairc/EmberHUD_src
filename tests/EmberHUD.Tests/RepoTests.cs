using System.Runtime.CompilerServices;
using System.Text.Json;
using EmberHUD.Host.Plugins;

namespace EmberHUD.Tests;

public class RepoTests
{
    static string Src([CallerFilePath] string file = "") => Path.GetFullPath(Path.Combine(Path.GetDirectoryName(file)!, "..", ".."));

    [Fact]
    public void LaunchProfileParsesAndPointsAtSrcPlugins()
    {
        var src = Src();
        using var json = JsonDocument.Parse(File.ReadAllText(Path.Combine(src, "EmberHUD", "Properties", "launchSettings.json")));
        var args = json.RootElement.GetProperty("profiles").GetProperty("EmberHUD").GetProperty("commandLineArgs").GetString();
        Assert.StartsWith("--plugins ", args);

        var plugins = Path.GetFullPath(args!["--plugins ".Length..], Path.Combine(src, "EmberHUD", "bin", "Debug", "net10.0-windows", "win-x64"));
        Assert.Equal(Path.Combine(src, "Plugins"), plugins);
    }

    [Fact]
    public void EveryShippedPluginCompilesAndPassesItsTests()
    {
        var report = new StringWriter();
        Environment.SetEnvironmentVariable("EMBER_PLUGINS", Path.Combine(Src(), "Plugins"));
        var code = CheckRunner.Run(Path.Combine(Src(), "Plugins"), report);
        Assert.True(code == 0, report.ToString());
        Assert.Contains("PASS Sample compile", report.ToString());
        Assert.Contains("PASS Mcp compile", report.ToString());
        Assert.Contains("PASS Inspector compile", report.ToString());
        Assert.DoesNotContain(" defaults - ", report.ToString());
    }
}
