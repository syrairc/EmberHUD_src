namespace EmberHUD.Launcher.Tests;

public class LaunchTests
{
    static readonly GameInstall Game = new(@"C:\Program Files (x86)\Torchlight Infinite", 5975312);
    static readonly GameInstall SteamGame = new(@"D:\SteamLibrary\steamapps\common\Torchlight Infinite", 5975312, true);
    const string Inner = @"set __COMPAT_LAYER=RunAsInvoker&& cd /d ""C:\Program Files (x86)\Torchlight Infinite\Game\UE_game\Binaries\Win64"" && start /high torchlight_infinite.exe -SkipGameLauncher -UserDir=Torchlight -Version=5975312";

    [Fact]
    public void GameInnerHasNoSpaceBeforeTheFirstAmpersands()
    {
        Assert.Equal(Inner, Launch.GameInner(Game));
    }

    [Fact]
    public void GameInnerDropsTheVersionWhenTheBuildIsUnknown() =>
        Assert.EndsWith("start /high torchlight_infinite.exe -SkipGameLauncher -UserDir=Torchlight", Launch.GameInner(Game with { Build = null }));

    [Fact]
    public void GameAsYourselfQuotesTheFolder()
    {
        var start = Launch.Game(Game, "");
        Assert.Equal("cmd", start.FileName);
        Assert.Equal("/c " + Inner, start.Arguments);
        Assert.Empty(start.ArgumentList);
        Assert.False(start.UseShellExecute);
        Assert.True(start.CreateNoWindow);
    }

    [Fact]
    public void GameAsAnotherUserGoesThroughRunasWithSavedCredentials()
    {
        var start = Launch.Game(Game, "POE");
        Assert.Equal("runas", start.FileName);
        Assert.Equal(["/user:POE", "/savecred", "cmd /c " + Inner], start.ArgumentList);
        Assert.False(start.UseShellExecute);
        Assert.False(start.CreateNoWindow);
    }

    [Fact]
    public void SavePasswordRunsAnEmptyCommandAsTheUser()
    {
        var start = Launch.SavePassword("POE");
        Assert.Equal("runas", start.FileName);
        Assert.Equal(["/user:POE", "/savecred", "cmd /c exit"], start.ArgumentList);
    }

    [Fact]
    public void EmberRunsUnderTheConfiguredProcessName()
    {
        var custom = Launch.Steps(new LauncherConfig { EmberProcessName = "svchost.exe" }, Game, @"D:\dist", _ => false, _ => { });
        Assert.Equal("--as \"svchost.exe\"", custom[1].Start.Arguments);
        var blank = Launch.Steps(new LauncherConfig { EmberProcessName = "" }, Game, @"D:\dist", _ => false, _ => { });
        Assert.Equal("", blank[1].Start.Arguments);
    }

    [Fact]
    public void StepsStartEverythingThatIsOnAndNotRunning()
    {
        var config = new LauncherConfig { Programs = [new LauncherProgram { Path = @"C:\Tools\obs64.exe", Arguments = "--minimize" }, new LauncherProgram { Path = @"C:\Tools\off.exe", Enabled = false }, new LauncherProgram()] };
        var steps = Launch.Steps(config, Game, @"D:\dist", _ => false, _ => { });
        Assert.Equal(["game", "EmberHUD", @"C:\Tools\obs64.exe"], steps.Select(step => step.Name));
        Assert.Equal(@"D:\dist\Ember.exe", steps[1].Start.FileName);
        Assert.Equal(@"D:\dist", steps[1].Start.WorkingDirectory);
        Assert.Equal("--as \"Ember.exe\"", steps[1].Start.Arguments);
        var program = steps[2].Start;
        Assert.Equal("--minimize", program.Arguments);
        Assert.Equal(@"C:\Tools", program.WorkingDirectory);
        Assert.True(program.UseShellExecute);
    }

    [Fact]
    public void StepsSkipWhatIsAlreadyRunning()
    {
        var config = new LauncherConfig { Programs = [new LauncherProgram { Path = @"C:\Tools\obs64.exe" }] };
        var log = new List<string>();
        var steps = Launch.Steps(config, Game, @"D:\dist", image => image is "torchlight_infinite.exe" or "Ember.exe" or "obs64.exe", log.Add);
        Assert.Empty(steps);
        Assert.Equal(3, log.Count(line => line.Contains("already running")));
    }

    [Fact]
    public void StepsSkipWhatIsSwitchedOff()
    {
        var log = new List<string>();
        Assert.Empty(Launch.Steps(new LauncherConfig { LaunchGame = false, LaunchEmberHUD = false }, Game, @"D:\dist", _ => false, log.Add));
        Assert.Equal(["game - off", "EmberHUD - off"], log);
    }

    [Fact]
    public void StepsLogAnUnknownBuild()
    {
        var log = new List<string>();
        var steps = Launch.Steps(new LauncherConfig { LaunchEmberHUD = false }, Game with { Build = null }, @"D:\dist", _ => false, log.Add);
        Assert.Equal("game", Assert.Single(steps).Name);
        Assert.Contains(log, line => line.Contains("-Version"));
    }

    [Fact]
    public void SteamInstallStartsThroughTheSteamUrl()
    {
        var start = Launch.Game(SteamGame, "");
        Assert.Equal("steam://rungameid/1974050", start.FileName);
        Assert.True(start.UseShellExecute);
        Assert.Equal("", start.Arguments);
    }

    [Fact]
    public void SteamInstallIgnoresRunAsUser()
    {
        var start = Launch.Game(SteamGame, "POE");
        Assert.Equal("steam://rungameid/1974050", start.FileName);
        Assert.Empty(start.ArgumentList);
    }

    [Fact]
    public void StepsLogThatRunAsUserIsIgnoredOnSteamAndSkipTheVersionWarning()
    {
        var log = new List<string>();
        var steps = Launch.Steps(new LauncherConfig { LaunchEmberHUD = false, RunAsUser = "POE" }, SteamGame with { Build = null }, @"D:\dist", _ => false, log.Add);
        Assert.Equal("game", Assert.Single(steps).Name);
        Assert.Single(log, line => line.Contains("RunAsUser is ignored"));
        Assert.DoesNotContain(log, line => line.Contains("-Version"));
    }

    [Fact]
    public void StepsStaySilentForASteamInstallWithoutRunAsUser()
    {
        var log = new List<string>();
        Launch.Steps(new LauncherConfig { LaunchEmberHUD = false }, SteamGame, @"D:\dist", _ => false, log.Add);
        Assert.DoesNotContain(log, line => line.Contains("RunAsUser"));
        Assert.DoesNotContain(log, line => line.Contains("-Version"));
    }

    [Fact]
    public void OpenLogWritesTheNamedLogFile()
    {
        using var temp = new TempDir();
        using (var log = Launch.OpenLog(temp.Path, "launcher")) log.WriteLine("hello");
        Assert.Contains("hello", File.ReadAllText(Path.Combine(temp.Path, "Logs", "launcher.log")));
    }

    [Fact]
    public void OpenLogFallsBackToNullWhenTheFolderCannotBeWritten()
    {
        using var temp = new TempDir();
        temp.File("Logs", "not a folder");
        Assert.Same(TextWriter.Null, Launch.OpenLog(temp.Path, "launcher"));
    }

    [Theory]
    [InlineData(null, "~ RUNASINVOKER")]
    [InlineData("~ HIGHDPIAWARE", "~ HIGHDPIAWARE RUNASINVOKER")]
    [InlineData("~ RUNASINVOKER", null)]
    [InlineData("~ HIGHDPIAWARE RUNASINVOKER", null)]
    [InlineData("~ RUNASADMIN", "~ RUNASINVOKER")]
    [InlineData("~ RUNASADMIN HIGHDPIAWARE", "~ HIGHDPIAWARE RUNASINVOKER")]
    public void WithRunAsInvokerMergesTheCompatFlags(string? existing, string? expected) => Assert.Equal(expected, Launch.WithRunAsInvoker(existing));

    [Fact]
    public void RunAsAdminStartsEmberElevatedAndLeavesTheStandaloneGameAlone()
    {
        var admin = new LauncherConfig { RunAsAdmin = true };
        var steam = Launch.Steps(admin, SteamGame, @"D:\dist", _ => false, _ => { });
        Assert.Equal("runas", steam[1].Start.Verb);
        Assert.True(steam[1].Start.UseShellExecute);
        Assert.Equal(@"D:\dist", steam[1].Start.WorkingDirectory);
        var standalone = Launch.Steps(admin, Game, @"D:\dist", _ => false, _ => { });
        Assert.Equal("runas", standalone[1].Start.Verb);
        Assert.Contains("RunAsInvoker", standalone[0].Start.Arguments);
        Assert.NotEqual("runas", Launch.Steps(new LauncherConfig(), SteamGame, @"D:\dist", _ => false, _ => { })[1].Start.Verb);
    }
}
