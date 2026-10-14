namespace EmberHUD.Launcher.Tests;

public class LauncherConfigTests
{
    [Fact]
    public void LoadReturnsNullWhenTheFileIsMissing()
    {
        using var temp = new TempDir();
        var problems = new List<string>();
        Assert.Null(LauncherConfig.Load(Path.Combine(temp.Path, "Config", "launcher.json"), problems.Add));
        Assert.Empty(problems);
    }

    [Fact]
    public void LoadReadsEveryField()
    {
        using var temp = new TempDir();
        var path = temp.File("launcher.json", """{ "RunAsUser": "POE", "GameDirectory": "D:\\TLI", "LaunchGame": false, "LaunchEmberHUD": false, "Programs": [ { "Path": "C:\\Tools\\obs64.exe", "Arguments": "--minimize", "Enabled": false } ] }""");
        var config = LauncherConfig.Load(path, _ => { })!;
        Assert.Equal("POE", config.RunAsUser);
        Assert.Equal(@"D:\TLI", config.GameDirectory);
        Assert.False(config.LaunchGame);
        Assert.False(config.LaunchEmberHUD);
        var program = Assert.Single(config.Programs);
        Assert.Equal(@"C:\Tools\obs64.exe", program.Path);
        Assert.Equal("--minimize", program.Arguments);
        Assert.False(program.Enabled);
    }

    [Fact]
    public void LoadFillsDefaultsForMissingFields()
    {
        using var temp = new TempDir();
        var config = LauncherConfig.Load(temp.File("launcher.json", "{}"), _ => { })!;
        Assert.Equal("", config.RunAsUser);
        Assert.Equal("", config.GameDirectory);
        Assert.True(config.LaunchGame);
        Assert.True(config.LaunchEmberHUD);
        Assert.Empty(config.Programs);
    }

    [Fact]
    public void LoadTurnsNullsIntoEmpty()
    {
        using var temp = new TempDir();
        var config = LauncherConfig.Load(temp.File("launcher.json", """{ "RunAsUser": null, "GameDirectory": null, "Programs": [ null, { "Path": null, "Arguments": null } ] }"""), _ => { })!;
        Assert.Equal("", config.RunAsUser);
        Assert.Equal("", config.GameDirectory);
        var program = Assert.Single(config.Programs);
        Assert.Equal("", program.Path);
        Assert.Equal("", program.Arguments);
        Assert.True(program.Enabled);
    }

    [Fact]
    public void LoadTurnsANullProgramListIntoEmpty()
    {
        using var temp = new TempDir();
        Assert.Empty(LauncherConfig.Load(temp.File("launcher.json", """{ "Programs": null }"""), _ => { })!.Programs);
    }

    [Theory]
    [InlineData("{ not json")]
    [InlineData("null")]
    [InlineData("[]")]
    public void LoadReportsBadJsonAndReturnsNull(string json)
    {
        using var temp = new TempDir();
        var problems = new List<string>();
        Assert.Null(LauncherConfig.Load(temp.File("launcher.json", json), problems.Add));
        Assert.Contains("launcher.json", Assert.Single(problems));
    }

    [Fact]
    public void RunAsAdminDefaultsToFalseAndRoundTrips()
    {
        using var temp = new TempDir();
        Assert.False(LauncherConfig.Load(temp.File("old.json", """{ "RunAsUser": "POE" }"""), _ => { })!.RunAsAdmin);
        var path = Path.Combine(temp.Path, "new.json");
        new LauncherConfig { RunAsAdmin = true }.Save(path);
        Assert.True(LauncherConfig.Load(path, _ => { })!.RunAsAdmin);
    }

    [Fact]
    public void SaveCreatesTheFolderAndRoundTrips()
    {
        using var temp = new TempDir();
        var path = Path.Combine(temp.Path, "Config", "launcher.json");
        var config = new LauncherConfig { RunAsUser = "POE", GameDirectory = @"C:\Program Files (x86)\Torchlight Infinite", LaunchEmberHUD = false, Programs = [new LauncherProgram { Path = @"C:\Tools\a b.exe", Arguments = "\"quoted arg\"" }] };
        config.Save(path);
        var loaded = LauncherConfig.Load(path, _ => { })!;
        Assert.Equal(config.RunAsUser, loaded.RunAsUser);
        Assert.Equal(config.GameDirectory, loaded.GameDirectory);
        Assert.False(loaded.LaunchEmberHUD);
        Assert.Equal("\"quoted arg\"", Assert.Single(loaded.Programs).Arguments);
        Assert.False(File.Exists(path + ".tmp"));
    }

    [Fact]
    public void FirstRunPicksPoeWhenThatUserExists() => Assert.Equal("poe", LauncherConfig.FirstRun(["Admin", "poe"]).RunAsUser);

    [Fact]
    public void FirstRunRunsAsYourselfWithoutPoe() => Assert.Equal("", LauncherConfig.FirstRun(["Admin"]).RunAsUser);
}
