namespace Aember.Launcher.Tests;

public class LaunchTests
{
    static readonly GameInstall Game = new(@"C:\Program Files (x86)\Torchlight Infinite", 5975312);
    const string Inner = @"set __COMPAT_LAYER=RunAsInvoker&& cd /d ""C:\Program Files (x86)\Torchlight Infinite\Game\UE_game\Binaries\Win64"" && start /high torchlight_infinite.exe -SkipGameLauncher -UserDir=Torchlight -Version=5975312";

    [Fact]
    public void NeedsSetupWithoutAConfigOrAGame()
    {
        Assert.True(Launch.NeedsSetup(null, Game));
        Assert.True(Launch.NeedsSetup(new LauncherConfig(), null));
        Assert.False(Launch.NeedsSetup(new LauncherConfig(), Game));
    }

    [Fact]
    public void GameInnerHasNoSpaceBeforeTheFirstAmpersands()
    {
        Assert.Equal(Inner, Launch.GameInner(Game));
        Assert.DoesNotContain("RunAsInvoker &&", Launch.GameInner(Game));
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
    public void StepsStartEverythingThatIsOnAndNotRunning()
    {
        var config = new LauncherConfig { Programs = [new LauncherProgram { Path = @"C:\Tools\obs64.exe", Arguments = "--minimize" }, new LauncherProgram { Path = @"C:\Tools\off.exe", Enabled = false }, new LauncherProgram()] };
        var steps = Launch.Steps(config, Game, @"D:\dist", _ => false, _ => { });
        Assert.Equal(["game", "Aember", @"C:\Tools\obs64.exe"], steps.Select(step => step.Name));
        Assert.Equal(@"D:\dist\Aember.exe", steps[1].Start.FileName);
        Assert.Equal(@"D:\dist", steps[1].Start.WorkingDirectory);
        Assert.Equal("", steps[1].Start.Arguments);
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
        var steps = Launch.Steps(config, Game, @"D:\dist", image => image is "torchlight_infinite.exe" or "Aember.exe" or "obs64.exe", log.Add);
        Assert.Empty(steps);
        Assert.Equal(3, log.Count(line => line.Contains("already running")));
    }

    [Fact]
    public void StepsSkipWhatIsSwitchedOff()
    {
        var log = new List<string>();
        Assert.Empty(Launch.Steps(new LauncherConfig { LaunchGame = false, LaunchAember = false }, Game, @"D:\dist", _ => false, log.Add));
        Assert.Equal(["game - off", "Aember - off"], log);
    }

    [Fact]
    public void StepsLogAnUnknownBuild()
    {
        var log = new List<string>();
        var steps = Launch.Steps(new LauncherConfig { LaunchAember = false }, Game with { Build = null }, @"D:\dist", _ => false, log.Add);
        Assert.Equal("game", Assert.Single(steps).Name);
        Assert.Contains(log, line => line.Contains("-Version"));
    }
}
