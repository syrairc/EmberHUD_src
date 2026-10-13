namespace EmberHUD.Launcher.Tests;

public class GameInstallTests
{
    const string Exe = @"Game\UE_game\Binaries\Win64\torchlight_infinite.exe";
    const string PatchVersion = @"Game\UE_game\Torchlight\Saved\PersistentDownloadDir\ReleaseGlobal\Config\live_patch_version.json";

    [Fact]
    public void AtNeedsTheGameExe()
    {
        using var temp = new TempDir();
        Assert.Null(GameInstall.At(temp.Path));
        temp.File(Exe);
        var game = GameInstall.At(temp.Path)!;
        Assert.Equal(temp.Path, game.Folder);
        Assert.Equal(Path.Combine(temp.Path, Exe), game.Exe);
        Assert.Equal(Path.Combine(temp.Path, @"Game\UE_game\Binaries\Win64"), game.Binaries);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void AtIgnoresABlankFolder(string folder) => Assert.Null(GameInstall.At(folder));

    [Fact]
    public void AtReadsTheBuild()
    {
        using var temp = new TempDir();
        temp.File(Exe);
        temp.File(PatchVersion, """{ "CurrentVersion": 5991864, "MinimumBuild": 5612870, "CurrentBuild": 5975312 }""");
        Assert.Equal(5975312, GameInstall.At(temp.Path)!.Build);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("{")]
    [InlineData("[]")]
    [InlineData("""{ "CurrentBuild": "x" }""")]
    [InlineData("""{ "CurrentVersion": 1 }""")]
    public void AtLeavesTheBuildUnknownWhenTheFileIsMissingOrBad(string? json)
    {
        using var temp = new TempDir();
        temp.File(Exe);
        if (json is not null) temp.File(PatchVersion, json);
        Assert.Null(GameInstall.At(temp.Path)!.Build);
    }

    [Fact]
    public void FindUsesOnlyTheConfiguredFolderWhenOneIsSet()
    {
        using var configured = new TempDir();
        using var registered = new TempDir();
        registered.File(Exe);
        Assert.Null(GameInstall.Find(configured.Path, registered.Path));
        configured.File(Exe);
        Assert.Equal(configured.Path, GameInstall.Find(configured.Path, registered.Path)!.Folder);
    }

    [Fact]
    public void FindUsesTheRegisteredFolderWhenNothingIsConfigured()
    {
        using var registered = new TempDir();
        registered.File(Exe);
        Assert.Equal(registered.Path, GameInstall.Find("", registered.Path)!.Folder);
    }

    [Theory]
    [InlineData(@"C:\Program Files (x86)\Torchlight Infinite\uninst.exe", @"C:\Program Files (x86)\Torchlight Infinite")]
    [InlineData(@"""C:\Program Files (x86)\Torchlight Infinite\uninst.exe"" /S", @"C:\Program Files (x86)\Torchlight Infinite")]
    [InlineData("  ", null)]
    [InlineData("\"\"", null)]
    public void FolderOfTakesTheUninstallersFolder(string uninstall, string? expected) => Assert.Equal(expected, GameInstall.FolderOf(uninstall));

    [Fact]
    public void PaksListsBasePaksBySizeThenPatchPaksByVersion()
    {
        using var temp = new TempDir();
        var big = temp.File(@"Game\UE_game\Content\Paks\pakchunk0-WindowsNoEditor.pak", "aaa");
        var small = temp.File(@"Game\UE_game\Content\Paks\pakchunk999-WindowsNoEditor.pak", "a");
        temp.File(@"Game\UE_game\Content\Paks\notes.pakx", "aaaa");
        var ten = temp.File(@"Game\UE_game\Torchlight\Saved\PersistentDownloadDir\PreMountPak\UE_game_10_P.pak", "a");
        var nine = temp.File(@"Game\UE_game\Torchlight\Saved\PersistentDownloadDir\PreMountPak\UE_game_9_P.pak", "aaaaa");
        temp.File(@"Game\UE_game\Torchlight\Saved\PersistentDownloadDir\PreMountPak\other.pak", "a");
        Assert.Equal([small, big, nine, ten], new GameInstall(temp.Path, null).Paks());
    }

    [Fact]
    public void PaksIsEmptyWithoutThePakFolders()
    {
        using var temp = new TempDir();
        Assert.Empty(new GameInstall(temp.Path, null).Paks());
    }
}
