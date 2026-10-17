namespace EmberHUD.Updater.Tests;

public class ManifestTests
{
    [Fact]
    public void BuildHashesEveryIncludedFileWithForwardSlashKeys()
    {
        using var temp = new TempDir();
        temp.File("EmberHUD.dll", "host");
        temp.File("Plugins/Market/Main.cs", "class Main {}");
        var manifest = Manifest.Build(temp.Path, "1.0.12", _ => true);
        Assert.Equal("1.0.12", manifest.Version);
        Assert.Equal(2, manifest.Files.Count);
        Assert.Equal(Manifest.Hash(Path.Combine(temp.Path, "EmberHUD.dll")), manifest.Files["EmberHUD.dll"]);
        Assert.Equal(Manifest.Hash(Path.Combine(temp.Path, "Plugins", "Market", "Main.cs")), manifest.Files["Plugins/Market/Main.cs"]);
    }

    [Fact]
    public void BuildSkipsFilesTheFilterRejects()
    {
        using var temp = new TempDir();
        temp.File("EmberHUD.dll", "host");
        temp.File("Config/sdk.json", "{}");
        var manifest = Manifest.Build(temp.Path, "1.0.12", path => !path.StartsWith("Config/"));
        Assert.Equal("EmberHUD.dll", Assert.Single(manifest.Files.Keys));
    }

    [Fact]
    public void SaveThenLoadRoundTrips()
    {
        using var temp = new TempDir();
        var path = Path.Combine(temp.Path, "Updates", "1.0.12", "manifest.json");
        new Manifest { Version = "1.0.12", Files = new Dictionary<string, string> { ["Plugins/Market/Main.cs"] = "abc" } }.Save(path);
        var loaded = Manifest.Load(path, _ => { })!;
        Assert.Equal("1.0.12", loaded.Version);
        Assert.Equal("abc", loaded.Files["Plugins/Market/Main.cs"]);
        Assert.False(File.Exists(path + ".tmp"));
    }

    [Fact]
    public void LoadReturnsNullWhenTheFileIsMissingWithoutReporting()
    {
        using var temp = new TempDir();
        var problems = new List<string>();
        Assert.Null(Manifest.Load(Path.Combine(temp.Path, "manifest.json"), problems.Add));
        Assert.Empty(problems);
    }

    [Theory]
    [InlineData("{ not json")]
    [InlineData("null")]
    [InlineData("[]")]
    public void LoadReportsBadJsonAndReturnsNull(string json)
    {
        using var temp = new TempDir();
        var problems = new List<string>();
        Assert.Null(Manifest.Load(temp.File("manifest.json", json), problems.Add));
        Assert.Contains("manifest.json", Assert.Single(problems));
    }

    [Theory]
    [InlineData("../x")]
    [InlineData("a/../b")]
    [InlineData("C:/x")]
    [InlineData("/x")]
    [InlineData("a\\b")]
    [InlineData("")]
    public void SafeRelativeRejectsRootedDotDotColonAndBackslash(string path) => Assert.False(Manifest.SafeRelative(path));

    [Fact]
    public void SafeRelativeAcceptsPluginPaths() => Assert.True(Manifest.SafeRelative("Plugins/Market/Main.cs"));
}
