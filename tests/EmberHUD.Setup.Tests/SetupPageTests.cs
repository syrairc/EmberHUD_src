using System.ComponentModel;
using System.Diagnostics;
using EmberHUD.Host;
using EmberHUD.Launcher;
using EmberHUD.Testing;
using Hexa.NET.ImGui;

namespace EmberHUD.Setup.Tests;

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
        new LauncherConfig { RunAsUser = "jessi", LaunchEmberHUD = false, Programs = [new LauncherProgram { Path = @"C:\Tools\obs64.exe" }] }.Save(Path.Combine(temp.Path, "Config", "launcher.json"));
        var page = Page(temp.Path, "POE");
        Assert.Equal("jessi", page.Config.RunAsUser);
        Assert.False(page.Config.LaunchEmberHUD);
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
        Assert.Equal(Path.Combine(temp.Path, "EmberLauncher.exe"), Assert.Single(_started).FileName);
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

    [Fact]
    public void PercentSignsInAMessageDrawLiterally()
    {
        using var temp = new TempDir();
        int VerticesFor(char formatChar)
        {
            var page = new SetupPage(temp.Path, [], _ => null, _ => null, _ => throw new Win32Exception($"a%{formatChar}b"));
            page.SaveAndLaunch();
            TestFrame.Run(page.Draw);
            return ImGui.GetDrawData().TotalVtxCount;
        }
        Assert.Equal(VerticesFor('X'), VerticesFor('s'));
    }

    int VerticesFor(string layout, bool runAsAdmin = false)
    {
        using var temp = new TempDir();
        temp.File(layout);
        new LauncherConfig { GameDirectory = temp.Path, RunAsAdmin = runAsAdmin }.Save(Path.Combine(temp.Path, "Config", "launcher.json"));
        var page = new SetupPage(temp.Path, [], _ => null, _ => null, _ => null, (_, _) => { }, _ => false);
        TestFrame.Run(page.Draw);
        TestFrame.Run(page.Draw);
        return ImGui.GetDrawData().TotalVtxCount;
    }

    [Fact]
    public void RunWithoutAdminShowsOnlyForASteamInstall() =>
        Assert.True(VerticesFor(@"UE_game\Binaries\Win64\torchlight_infinite.exe") > VerticesFor(@"Game\UE_game\Binaries\Win64\torchlight_infinite.exe"));

    [Fact]
    public void RunAsAdministratorDrawsAndPersists()
    {
        VerticesFor(@"UE_game\Binaries\Win64\torchlight_infinite.exe", true);
        using var temp = new TempDir();
        new LauncherConfig { RunAsAdmin = true }.Save(Path.Combine(temp.Path, "Config", "launcher.json"));
        Assert.True(Page(temp.Path).Config.RunAsAdmin);
    }

    [Fact]
    public void ApplyFlagWritesThroughTheInjectedWriterAndReportsIt()
    {
        using var temp = new TempDir();
        var written = new List<GameInstall>();
        var page = new SetupPage(temp.Path, [], _ => null, _ => null, _ => null, (game, log) => { written.Add(game); log("game - set RunAsInvoker for the Steam exe so the overlay can read it"); }, _ => true);
        var steam = new GameInstall(@"D:\Steam\Torchlight Infinite", null, true);
        page.ApplyFlag(steam);
        Assert.Equal([steam], written);
        Assert.Equal("applied", page.Message);
        var failing = new SetupPage(temp.Path, [], _ => null, _ => null, _ => null, (_, log) => log("game - could not set RunAsInvoker for the Steam exe (denied)"), _ => false);
        failing.ApplyFlag(steam);
        Assert.Contains("denied", failing.Message);
    }

    [Fact]
    public void ExeNameMatchesTheProcessNameInTheOffsets() =>
        Assert.Equal(Offsets.GameProcessName, Path.GetFileNameWithoutExtension(GameInstall.ExeName));
}
