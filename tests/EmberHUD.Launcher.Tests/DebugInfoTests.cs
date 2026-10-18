namespace EmberHUD.Launcher.Tests;

public class DebugInfoTests
{
    const string Exe = @"Game\UE_game\Binaries\Win64\torchlight_infinite.exe";
    const string PatchVersion = @"Game\UE_game\Torchlight\Saved\PersistentDownloadDir\ReleaseGlobal\Config\live_patch_version.json";

    [Theory]
    [InlineData(@"C:\Users\Sam\Games\TLI", @"C:\Users\Sam", "Sam", @"%USERPROFILE%\Games\TLI")]
    [InlineData(@"D:\Sam\TLI", @"C:\Users\Sam", "Sam", @"D:\<user>\TLI")]
    [InlineData(@"D:\Samurai\TLI", @"C:\Users\Sam", "Sam", @"D:\Samurai\TLI")]
    [InlineData(@"C:\Program Files (x86)\Torchlight Infinite", @"C:\Users\Sam", "Sam", @"C:\Program Files (x86)\Torchlight Infinite")]
    public void RedactPathHidesTheProfileAndTheUserName(string path, string profile, string user, string expected) =>
        Assert.Equal(expected, DebugInfo.RedactPath(path, profile, user));

    [Fact]
    public void DescribeListsVersionsFoldersAndElevation()
    {
        using var temp = new TempDir();
        temp.File(Exe, new byte[1234]);
        temp.File(PatchVersion, """{ "CurrentBuild": 5975312 }""");
        temp.File("Ember.dll", new byte[99]);

        var text = DebugInfo.Describe("overlay", new Version(1, 0, 160), temp.Path, temp.Path, null, true, "running, opened for reading", "logic world not created yet");

        Assert.Contains("EmberHUD: v1.0.160 (overlay)", text);
        Assert.Contains("EmberHUD binary: 99 bytes", text);
        Assert.Contains("EmberHUD elevated: yes", text);
        Assert.Contains("Torchlight install: standalone, build 5975312", text);
        Assert.Contains("Torchlight binary: 1234 bytes", text);
        Assert.Contains("Torchlight process: running, opened for reading", text);
        Assert.Contains("Overlay status: logic world not created yet", text);
        Assert.DoesNotContain(Environment.UserName, text.Replace("<user>", ""), StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(Environment.MachineName, text, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void DescribeSaysWhenTheGameIsMissing()
    {
        using var temp = new TempDir();
        var text = DebugInfo.Describe("setup", new Version(1, 0, 160), temp.Path, temp.Path, null, false, "not running");
        Assert.Contains("Torchlight: not found", text);
        Assert.Contains("EmberHUD binary: unreadable", text);
        Assert.Contains("EmberHUD elevated: no", text);
        Assert.DoesNotContain("Overlay status", text);
    }
}
