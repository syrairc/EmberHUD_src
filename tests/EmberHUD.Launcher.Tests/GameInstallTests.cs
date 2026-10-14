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

    [Fact]
    public void FindFallsBackToTheDefaultFolders()
    {
        using var steamDefault = new TempDir();
        Assert.Null(GameInstall.Find("", null, @"Z:\nope", steamDefault.Path));
        steamDefault.File(@"UE_game\Binaries\Win64\torchlight_infinite.exe");
        Assert.True(GameInstall.Find("", null, @"Z:\nope", steamDefault.Path)!.Steam);
        Assert.Equal(@"C:\Program Files (x86)\Steam\steamapps\common\Torchlight Infinite", GameInstall.SteamDefaultFolder);
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

    const string SteamExe = @"UE_game\Binaries\Win64\torchlight_infinite.exe";
    const string SteamPatchVersion = @"UE_game\TorchLight\Saved\PersistentDownloadDir\ReleaseGlobal\Config\live_patch_version.json";

    [Fact]
    public void AtAcceptsTheSteamLayoutWithoutTheGameFolder()
    {
        using var temp = new TempDir();
        temp.File(SteamExe);
        temp.File(SteamPatchVersion, """{ "CurrentBuild": 5975312 }""");
        var game = GameInstall.At(temp.Path)!;
        Assert.True(game.Steam);
        Assert.Equal(5975312, game.Build);
        Assert.Equal(Path.Combine(temp.Path, SteamExe), game.Exe);
        Assert.Equal(Path.Combine(temp.Path, @"UE_game\Binaries\Win64"), game.Binaries);
    }

    [Fact]
    public void AtPrefersTheStandaloneLayoutWhenBothExist()
    {
        using var temp = new TempDir();
        temp.File(Exe);
        temp.File(SteamExe);
        Assert.False(GameInstall.At(temp.Path)!.Steam);
    }

    [Fact]
    public void PaksFollowTheSteamLayout()
    {
        using var temp = new TempDir();
        var big = temp.File(@"UE_game\Content\Paks\pakchunk0-WindowsNoEditor.pak", "aaa");
        var small = temp.File(@"UE_game\Content\Paks\pakchunk1-WindowsNoEditor.pak", "a");
        var nine = temp.File(@"UE_game\Torchlight\Saved\PersistentDownloadDir\PreMountPak\UE_game_9_P.pak", "aaaaa");
        var ten = temp.File(@"UE_game\Torchlight\Saved\PersistentDownloadDir\PreMountPak\UE_game_10_P.pak", "a");
        Assert.Equal([small, big, nine, ten], new GameInstall(temp.Path, null, true).Paks());
    }

    [Fact]
    public void SteamInstallReadsEveryLibraryAndTheManifestInstallDir()
    {
        using var steam = new TempDir();
        using var library = new TempDir();
        steam.File(@"steamapps\libraryfolders.vdf", "\"libraryfolders\"\r\n{\r\n\t\"0\"\r\n\t{\r\n\t\t\"path\"\t\t\"" + steam.Path.Replace(@"\", @"\\") + "\"\r\n\t}\r\n\t\"1\"\r\n\t{\r\n\t\t\"path\"\t\t\"" + library.Path.Replace(@"\", @"\\") + "\"\r\n\t\t\"label\"\t\t\"\"\r\n\t}\r\n}\r\n");
        Assert.Null(GameInstall.SteamInstall(steam.Path));
        library.File(@"steamapps\appmanifest_1974050.acf", "\"AppState\"\n{\n\t\"appid\"\t\t\"1974050\"\n\t\"installdir\"\t\t\"Torchlight Infinite\"\n}\n");
        Assert.Equal(Path.Combine(library.Path, @"steamapps\common\Torchlight Infinite"), GameInstall.SteamInstall(steam.Path));
    }

    [Fact]
    public void SteamInstallFindsTheGameInTheSteamRootItself()
    {
        using var steam = new TempDir();
        steam.File(@"steamapps\libraryfolders.vdf", "\"libraryfolders\"\n{\n}\n");
        steam.File(@"steamapps\appmanifest_1974050.acf", "\"AppState\"\n{\n\t\"installdir\"\t\t\"Torchlight Infinite\"\n}\n");
        Assert.Equal(Path.Combine(steam.Path, @"steamapps\common\Torchlight Infinite"), GameInstall.SteamInstall(steam.Path));
    }

    [Fact]
    public void SteamInstallIsNullWithoutALibraryFile()
    {
        using var steam = new TempDir();
        Assert.Null(GameInstall.SteamInstall(steam.Path));
    }
}
