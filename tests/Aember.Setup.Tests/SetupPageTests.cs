using System.Diagnostics;
using Aember.Launcher;
using Aember.Testing;

namespace Aember.Setup.Tests;

public class SetupPageTests
{
    readonly List<ProcessStartInfo> _started = [];

    SetupPage Page(string root, params string[] users) => new(root, users, _ => null, _ => null, start => { _started.Add(start); return null; });

    [Fact]
    public void FirstRunPrefillsPoeAndDraws()
    {
        using var temp = new TempDir();
        var page = Page(temp.Path, "POE", "jessi");
        Assert.Equal("POE", page.Config.RunAsUser);
        Assert.Equal("", page.Config.GameDirectory);
        TestFrame.Run(page.Draw);
        TestFrame.Run(page.Draw);
    }

    [Fact]
    public void LoadsAnExistingConfig()
    {
        using var temp = new TempDir();
        new LauncherConfig { RunAsUser = "jessi", LaunchAember = false, Programs = [new LauncherProgram { Path = @"C:\Tools\obs64.exe" }] }.Save(Path.Combine(temp.Path, "Config", "launcher.json"));
        var page = Page(temp.Path, "POE");
        Assert.Equal("jessi", page.Config.RunAsUser);
        Assert.False(page.Config.LaunchAember);
        TestFrame.Run(page.Draw);
    }

    [Fact]
    public void BadJsonShowsDefaultsAndSaysSo()
    {
        using var temp = new TempDir();
        temp.File(@"Config\launcher.json", "{ nope");
        var page = Page(temp.Path);
        Assert.True(page.Config.LaunchGame);
        Assert.Contains("could not be read", page.Message);
        TestFrame.Run(page.Draw);
    }

    [Fact]
    public void SaveWritesLauncherJson()
    {
        using var temp = new TempDir();
        var page = Page(temp.Path, "POE");
        page.Config.Programs.Add(new LauncherProgram { Path = @"C:\Tools\a.exe" });
        Assert.True(page.Save());
        Assert.Equal(@"C:\Tools\a.exe", Assert.Single(LauncherConfig.Load(Path.Combine(temp.Path, "Config", "launcher.json"), _ => { })!.Programs).Path);
    }

    [Fact]
    public void SaveAndLaunchStartsTheLauncherAndCloses()
    {
        using var temp = new TempDir();
        var page = Page(temp.Path);
        page.SaveAndLaunch();
        Assert.Equal(Path.Combine(temp.Path, "AemberLauncher.exe"), Assert.Single(_started).FileName);
        Assert.True(page.CloseRequested);
        Assert.True(File.Exists(Path.Combine(temp.Path, "Config", "launcher.json")));
    }

    [Fact]
    public void SavePasswordRunsRunasForTheChosenUser()
    {
        using var temp = new TempDir();
        var page = Page(temp.Path, "POE");
        page.SavePassword();
        Assert.Equal(["/user:POE", "/savecred", "cmd /c exit"], Assert.Single(_started).ArgumentList);
    }

    [Fact]
    public void SavePasswordDoesNothingAsYourself()
    {
        using var temp = new TempDir();
        Page(temp.Path).SavePassword();
        Assert.Empty(_started);
    }

    [Fact]
    public void IconsSectionDrawsWithoutAGame()
    {
        using var temp = new TempDir();
        var page = Page(temp.Path);
        page.Config.GameDirectory = temp.Path;
        TestFrame.Run(page.Draw);
    }

    [Fact]
    public void LocalUsersSkipsTheCurrentUser() => Assert.DoesNotContain(LocalUsers.List(), user => user.Equals(Environment.UserName, StringComparison.OrdinalIgnoreCase));
}
