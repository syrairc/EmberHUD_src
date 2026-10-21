using System.Text.Json;
using EmberHUD.Host;
using EmberHUD.Launcher;

namespace EmberHUD.Assets.Tests;

public class IconExportTests
{
    const string Config = "UE_game/Content/Script/Data/ConfigView/config_map_icon_ChunkA0.bin";
    static readonly byte[] Red = [255, 0, 0, 255, 255, 0, 0, 255, 255, 0, 0, 255, 255, 0, 0, 255];
    static readonly byte[] Blue = [0, 0, 255, 255, 0, 0, 255, 255, 0, 0, 255, 255, 0, 0, 255, 255];

    static byte[] Bgra(byte[] rgba) => [.. rgba.Chunk(4).SelectMany(p => new[] { p[2], p[1], p[0], p[3] })];

    static byte[] Atlas => [.. Enumerable.Range(0, 16).SelectMany(i => new[] { (byte)(i * 10), (byte)i, (byte)(255 - i), (byte)255 })];

    static PakSource[] BasePak()
    {
        string[] spriteNames = ["None", "PaperSprite", "Default__PaperSprite", "/Game/Art/UI/AtlasMap/Icon_Egg_Sprite", "/Game/Art/UI/AtlasMap/AtlasMap_Atlas", "StructProperty", "BakedSourceUV", "BakedSourceDimension"];
        return
        [
            new("UE_game/Content/Art/UI/MiniMap/UI_MiniMap_Boss.uasset", AssetBuilder.Package("None", "Texture2D", "Default__Texture2D")),
            new("UE_game/Content/Art/UI/MiniMap/UI_MiniMap_Boss.uexp", AssetBuilder.Texture("PF_B8G8R8A8", 2, 2, Bgra(Red)), "Zlib"),
            new("UE_game/Content/Art/UI/AtlasMap/AtlasMap_Atlas.uasset", AssetBuilder.Package("None", "Texture2D", "Default__Texture2D")),
            new("UE_game/Content/Art/UI/AtlasMap/AtlasMap_Atlas.uexp", AssetBuilder.Texture("PF_B8G8R8A8", 4, 4, Bgra(Atlas))),
            new("UE_game/Content/Art/UI/AtlasMap/Icon_Egg_Sprite.uasset", AssetBuilder.Package(spriteNames), Encrypted: true),
            new("UE_game/Content/Art/UI/AtlasMap/Icon_Egg_Sprite.uexp", AssetBuilder.Sprite(spriteNames, (1, 1), (2, 2))),
            new("UE_game/Content/Other/UI_Elsewhere.uasset", AssetBuilder.Package("None", "Texture2D", "Default__Texture2D")),
            new(Config, AssetBuilder.MapIconConfig(new MapIconRow(2, "boss", "首领", "UI_MiniMap_Boss", 98), new MapIconRow(3, "ghost", "鬼", "UI_Ghost", 1), new MapIconRow(4, "egg", "蛋", "Icon_Egg", 5))),
        ];
    }

    static AssetResult Run(IReadOnlyList<string> paks, string folder) => new MinimapJob(Offsets.TradePriceTexture).Run(paks, folder, PakBuilder.Key, _ => { }, CancellationToken.None);

    static string[] Staging(string folder) => Directory.GetDirectories(Path.GetDirectoryName(folder)!, Path.GetFileName(folder) + ".new*");

    static (string Folder, string[] Paks) Game(TempDir temp, params PakSource[][] patches)
    {
        string[] paks =
        [
            temp.File(@"game\pakchunk0-WindowsNoEditor.pak", PakBuilder.Build(BasePak())),
            .. patches.Select((files, i) => temp.File($@"game\UE_game_{i + 9}_P.pak", PakBuilder.Build(files))),
        ];
        return (Path.Combine(temp.Path, "GameIcons"), paks);
    }

    [Fact]
    public void ExportsTexturesAndSpritesWithAnIndex()
    {
        using var temp = new TempDir();
        var (folder, paks) = Game(temp);
        var result = Run(paks, folder);
        Assert.Equal(new AssetResult(3, 2, 0), result);
        Assert.Equal(Red, AssetBuilder.Pixels(Path.Combine(folder, "UI_MiniMap_Boss.png")).Rgba);
        Assert.Equal(UAsset.Crop(Atlas, 4, 4, 1, 1, 2, 2), AssetBuilder.Pixels(Path.Combine(folder, "Icon_Egg.png")).Rgba);
        Assert.False(File.Exists(Path.Combine(folder, "UI_Elsewhere.png")));

        using var index = JsonDocument.Parse(File.ReadAllText(Path.Combine(folder, "index.json")));
        var root = index.RootElement;
        Assert.Equal(new FileInfo(paks[0]).Length, root.GetProperty("source").GetProperty("pakchunk0-WindowsNoEditor.pak").GetInt64());
        Assert.Equal(["AtlasMap_Atlas", "Icon_Egg", "UI_MiniMap_Boss"], root.GetProperty("icons").EnumerateObject().Select(icon => icon.Name));
        var egg = root.GetProperty("icons").GetProperty("Icon_Egg");
        Assert.Equal(["file", "pak"], egg.EnumerateObject().Select(property => property.Name));
        Assert.Equal("Icon_Egg.png", egg.GetProperty("file").GetString());
        Assert.Equal("pakchunk0-WindowsNoEditor.pak", root.GetProperty("icons").GetProperty("UI_MiniMap_Boss").GetProperty("pak").GetString());
        Assert.Equal(["UI_Ghost", Offsets.TradePriceTexture], root.GetProperty("missing").EnumerateArray().Select(v => v.GetString()));
        Assert.False(root.TryGetProperty("map_icon_config", out _));
    }

    [Fact]
    public void ExportsTheMarketPriceIconFromOutsideTheIconFolders()
    {
        using var temp = new TempDir();
        PakSource[] fe =
        [
            new($"UE_game/Content/Art/UI/Textures/Common/Icon/Item/128/{Offsets.TradePriceTexture}.uasset", AssetBuilder.Package("None", "Texture2D", "Default__Texture2D")),
            new($"UE_game/Content/Art/UI/Textures/Common/Icon/Item/128/{Offsets.TradePriceTexture}.uexp", AssetBuilder.Texture("PF_B8G8R8A8", 2, 2, Bgra(Blue))),
        ];
        var (folder, paks) = Game(temp, fe);
        Run(paks, folder);
        Assert.Equal(Blue, AssetBuilder.Pixels(Path.Combine(folder, Offsets.TradePriceTexture + ".png")).Rgba);
    }

    [Fact]
    public void PatchPakOverridesTheBasePak()
    {
        using var temp = new TempDir();
        var (folder, paks) = Game(temp,
            [new("UE_game/Content/Art/UI/MiniMap/UI_MiniMap_Boss.uexp", AssetBuilder.Texture("PF_B8G8R8A8", 2, 2, Bgra(Red)))],
            [new("UE_game/Content/Art/UI/MiniMap/UI_MiniMap_Boss.uasset", AssetBuilder.Package("None", "Texture2D", "Default__Texture2D")), new("UE_game/Content/Art/UI/MiniMap/UI_MiniMap_Boss.uexp", AssetBuilder.Texture("PF_B8G8R8A8", 2, 2, Bgra(Blue))), new(Config, AssetBuilder.MapIconConfig(new MapIconRow(2, "boss", "首领", "UI_MiniMap_Boss", 98), new MapIconRow(5, "door", "门", "UI_MiniMap_Boss", 1)))]);
        Run(paks, folder);
        Assert.Equal(Blue, AssetBuilder.Pixels(Path.Combine(folder, "UI_MiniMap_Boss.png")).Rgba);
        using var index = JsonDocument.Parse(File.ReadAllText(Path.Combine(folder, "index.json")));
        Assert.Equal("UE_game_10_P.pak", index.RootElement.GetProperty("icons").GetProperty("UI_MiniMap_Boss").GetProperty("pak").GetString());
        Assert.Equal(2, index.RootElement.GetProperty("source").EnumerateObject().Count());
    }

    [Fact]
    public void ReExportReplacesTheOldFolder()
    {
        using var temp = new TempDir();
        var (folder, paks) = Game(temp);
        temp.File(@"GameIcons\stale.png", "x");
        temp.File(@"GameIcons\index.json", "{\"icons\":{\"stale\":{\"file\":\"stale.png\"}}}");
        Run(paks, folder);
        Assert.False(File.Exists(Path.Combine(folder, "stale.png")));
        Assert.True(File.Exists(Path.Combine(folder, "UI_MiniMap_Boss.png")));
        Assert.Empty(Staging(folder));
    }

    [Fact]
    public void FailedExtractLeavesTheOldIconsAlone()
    {
        using var temp = new TempDir();
        var (folder, paks) = Game(temp);
        var old = temp.File(@"GameIcons\index.json", "old");
        var broken = temp.File(@"game\pakchunk1-WindowsNoEditor.pak", PakBuilder.Build(PakBuilder.Key, 0x5A6F12E1, 12, new PakSource("UE_game/x.bin", [1])));
        Assert.Throws<InvalidDataException>(() => Run([.. paks, broken], folder));
        Assert.Equal("old", File.ReadAllText(old));
        Assert.Empty(Staging(folder));
    }

    [Fact]
    public void MissingConfigIsAClearError()
    {
        using var temp = new TempDir();
        var pak = temp.File("a.pak", PakBuilder.Build(new PakSource("UE_game/x.bin", [1])));
        Assert.Contains("config_map_icon_ChunkA0.bin", Assert.Throws<InvalidDataException>(() => Run([pak], Path.Combine(temp.Path, "GameIcons"))).Message);
    }

    [Fact]
    public void PaksAreClosedAfterARunAndAfterAFailedOne()
    {
        using var temp = new TempDir();
        var (folder, paks) = Game(temp);
        Run(paks, folder);
        using (File.Open(paks[0], FileMode.Open, FileAccess.ReadWrite, FileShare.None)) { }

        var noConfig = temp.File("a.pak", PakBuilder.Build(new PakSource("UE_game/x.bin", [1])));
        Assert.Throws<InvalidDataException>(() => Run([noConfig], folder));
        using (File.Open(noConfig, FileMode.Open, FileAccess.ReadWrite, FileShare.None)) { }
    }

    [Fact]
    public void AnyPerIconFailureLandsInFailed()
    {
        using var temp = new TempDir();
        string[] flatNames = ["None", "PaperSprite", "Default__PaperSprite", "/Game/Art/UI/AtlasMap/Icon_Flat_Sprite", "/Game/Art/UI/AtlasMap/AtlasMap_Atlas", "StructProperty", "BakedSourceUV", "BakedSourceDimension"];
        var (folder, paks) = Game(temp, [[new("UE_game/Content/Art/UI/AtlasMap/Icon_Flat_Sprite.uasset", AssetBuilder.Package(flatNames)), new("UE_game/Content/Art/UI/AtlasMap/Icon_Flat_Sprite.uexp", AssetBuilder.Sprite(flatNames, (1, 1), (0, 0)))]]);
        Assert.Equal(new AssetResult(3, 2, 1), Run(paks, folder));
        using var index = JsonDocument.Parse(File.ReadAllText(Path.Combine(folder, "index.json")));
        Assert.Equal(["Icon_Flat"], index.RootElement.GetProperty("failed").EnumerateObject().Select(failed => failed.Name));
    }

    [Fact]
    public void SwapThatCannotPlaceTheNewFolderPutsTheOldOneBack()
    {
        using var temp = new TempDir();
        var target = Path.Combine(temp.Path, "GameIcons");
        temp.File(@"GameIcons\index.json", "old");
        temp.File(@"GameIcons.new\index.json", "new");
        using (File.Open(Path.Combine(target + ".new", "index.json"), FileMode.Open, FileAccess.Read, FileShare.None))
            Assert.True(Record.Exception(() => IconExport.Swap(target + ".new", target)) is IOException or UnauthorizedAccessException);
        Assert.Equal("old", File.ReadAllText(Path.Combine(target, "index.json")));
        Assert.False(Directory.Exists(target + ".old"));
    }

    [Fact]
    public void SwapWithALockedFileKeepsTheOldFolder()
    {
        using var temp = new TempDir();
        var target = Path.Combine(temp.Path, "GameIcons");
        temp.File(@"GameIcons\index.json", "old");
        temp.File(@"GameIcons.new\index.json", "new");
        using (File.Open(Path.Combine(target, "index.json"), FileMode.Open, FileAccess.Read, FileShare.None))
            Assert.True(Record.Exception(() => IconExport.Swap(target + ".new", target)) is IOException or UnauthorizedAccessException);
        Assert.Equal("old", File.ReadAllText(Path.Combine(target, "index.json")));
    }

    [Fact]
    public void ZeroIconsKeepsTheOldSet()
    {
        using var temp = new TempDir();
        var pak = temp.File(@"game\a.pak", PakBuilder.Build(new PakSource(Config, AssetBuilder.MapIconConfig(new MapIconRow(2, "boss", "boss", "UI_Gone", 1)))));
        var folder = Path.Combine(temp.Path, "GameIcons");
        var old = temp.File(@"GameIcons\index.json", "old");
        var png = temp.File(@"GameIcons\Old.png", "x");
        Assert.Contains("no icons matched", Assert.Throws<InvalidDataException>(() => Run([pak], folder)).Message);
        Assert.Equal("old", File.ReadAllText(old));
        Assert.True(File.Exists(png));
        Assert.Empty(Staging(folder));
    }

    [Fact]
    public void StagingIsPerProcessAndStaleSiblingsAreCleanedUp()
    {
        using var temp = new TempDir();
        var target = Path.Combine(temp.Path, "GameIcons");
        var stale = Path.Combine(temp.Path, "GameIcons.new-1");
        var fresh = Path.Combine(temp.Path, "GameIcons.new-2");
        Directory.CreateDirectory(stale);
        Directory.CreateDirectory(fresh);
        Directory.SetLastWriteTimeUtc(stale, DateTime.UtcNow.AddDays(-2));

        Assert.EndsWith($".new-{Environment.ProcessId}", AssetJobs.Staging(target));
        Assert.False(Directory.Exists(stale));
        Assert.True(Directory.Exists(fresh));
    }

    // config_map_icon_ChunkA0.bin's content format changed (encrypted/obfuscated, not the plaintext rows IconExport expects) -
    // these only resolve through that config, not the MiniMap/NoAtlasMap/AtlasMap folder scan, so they're missing until it's decoded.
    [Fact]
    public void ExtractMatchesTheCheckedInIconsWhenTheGameIsSet()
    {
        var game = RealGame.Install();
        var assets = RealGame.IconAssets();
        using var temp = new TempDir();
        var folder = Path.Combine(temp.Path, "GameIcons");
        var result = new MinimapJob(Offsets.TradePriceTexture).Run(game.Paks(), folder, Convert.FromHexString(Offsets.PakAesKey), _ => { }, CancellationToken.None);
        Assert.Equal(0, result.Failed);

        using var reference = JsonDocument.Parse(File.ReadAllText(Path.Combine(assets, "index.json")));
        using var extracted = JsonDocument.Parse(File.ReadAllText(Path.Combine(folder, "index.json")));
        var icons = extracted.RootElement.GetProperty("icons");
        var differences = new List<string>();
        foreach (var icon in reference.RootElement.GetProperty("icons").EnumerateObject())
        {
            if (!icons.TryGetProperty(icon.Name, out var mine))
            {
                differences.Add($"{icon.Name} missing");
                continue;
            }
            if (mine.GetProperty("pak").GetString()!.EndsWith("_P.pak", StringComparison.OrdinalIgnoreCase)) continue;
            var expected = AssetBuilder.Pixels(Path.Combine(assets, icon.Value.GetProperty("file").GetString()!));
            var actual = AssetBuilder.Pixels(Path.Combine(folder, mine.GetProperty("file").GetString()!));
            if (expected.Width != actual.Width || expected.Height != actual.Height || !expected.Rgba.AsSpan().SequenceEqual(actual.Rgba)) differences.Add($"{icon.Name} pixels differ");
        }
        Assert.Empty(differences);
    }
}
