using System.Text.Json;

namespace EmberHUD.Tests;

public class LaunchSettingsTests
{
    private static string SrcFolder([System.Runtime.CompilerServices.CallerFilePath] string file = "") => Path.GetFullPath(Path.Combine(Path.GetDirectoryName(file)!, "..", ".."));

    [Fact]
    public void ProfileParsesAndPointsAtSrcPlugins()
    {
        var src = SrcFolder();
        using var json = JsonDocument.Parse(File.ReadAllText(Path.Combine(src, "EmberHUD", "Properties", "launchSettings.json")));
        var args = json.RootElement.GetProperty("profiles").GetProperty("EmberHUD").GetProperty("commandLineArgs").GetString();
        Assert.StartsWith("--plugins ", args);

        var plugins = Path.GetFullPath(args!["--plugins ".Length..], Path.Combine(src, "EmberHUD", "bin", "Debug", "net10.0-windows", "win-x64"));
        Assert.Equal(Path.Combine(src, "Plugins"), plugins);
    }
}
