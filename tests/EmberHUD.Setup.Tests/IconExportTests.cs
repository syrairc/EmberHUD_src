using System.Text.Json;
using EmberHUD.Host;
using EmberHUD.Launcher;
using EmberHUD.Setup.Icons;

namespace EmberHUD.Setup.Tests;

public class IconExportTests
{
    const string Config = "UE_game/Content/Script/Data/ConfigView/config_map_icon.bin";
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
        var result = IconExport.Run(paks, folder, PakBuilder.Key, _ => { });
        Assert.Equal(new ExportResult(3, 2, 0), result);
        Assert.Equal(Red, AssetBuilder.Pixels(Path.Combine(folder, "UI_MiniMap_Boss.png")).Rgba);
        Assert.Equal(UAsset.Crop(Atlas, 4, 4, 1, 1, 2, 2), AssetBuilder.Pixels(Path.Combine(folder, "Icon_Egg.png")).Rgba);
        Assert.False(File.Exists(Path.Combine(folder, "UI_Elsewhere.png")));

        using var index = JsonDocument.Parse(File.ReadAllText(Path.Combine(folder, "index.json")));
        var root = index.RootElement;
        Assert.Equal(new FileInfo(paks[0]).Length, root.GetProperty("source").GetProperty("pakchunk0-WindowsNoEditor.pak").GetInt64());
        Assert.Equal(["AtlasMap_Atlas", "Icon_Egg", "UI_MiniMap_Boss"], root.GetProperty("icons").EnumerateObject().Select(icon => icon.Name));
        var egg = root.GetProperty("icons").GetProperty("Icon_Egg");
        Assert.Equal("sprite", egg.GetProperty("kind").GetString());
        Assert.Equal("/Game/Art/UI/AtlasMap/AtlasMap_Atlas", egg.GetProperty("atlas").GetString());
        Assert.Equal([1, 1, 2, 2], egg.GetProperty("rect").EnumerateArray().Select(v => v.GetInt32()));
        Assert.Equal("Icon_Egg.png", egg.GetProperty("file").GetString());
        var boss = root.GetProperty("icons").GetProperty("UI_MiniMap_Boss");
        Assert.Equal("texture", boss.GetProperty("kind").GetString());
        Assert.Equal("/Game/Art/UI/MiniMap/UI_MiniMap_Boss", boss.GetProperty("asset").GetString());
        Assert.Equal("pakchunk0-WindowsNoEditor.pak", boss.GetProperty("pak").GetString());
        Assert.Equal("PF_B8G8R8A8", boss.GetProperty("format").GetString());
        Assert.Equal([2, 2], boss.GetProperty("size").EnumerateArray().Select(v => v.GetInt32()));
        Assert.False(boss.TryGetProperty("atlas", out _));
        Assert.Equal(["UI_Ghost", Offsets.TradePriceTexture], root.GetProperty("missing").EnumerateArray().Select(v => v.GetString()));
        var row = root.GetProperty("map_icon_config")[0];
        Assert.Equal((2, "boss", "首领", "UI_MiniMap_Boss", 98), (row.GetProperty("id").GetInt32(), row.GetProperty("key").GetString(), row.GetProperty("label").GetString(), row.GetProperty("texture").GetString(), row.GetProperty("priority").GetInt32()));
        Assert.Contains("首领", File.ReadAllText(Path.Combine(folder, "index.json")));
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
        IconExport.Run(paks, folder, PakBuilder.Key, _ => { });
        Assert.Equal(Blue, AssetBuilder.Pixels(Path.Combine(folder, Offsets.TradePriceTexture + ".png")).Rgba);
    }

    [Fact]
    public void PatchPakOverridesTheBasePak()
    {
        using var temp = new TempDir();
        var (folder, paks) = Game(temp,
            [new("UE_game/Content/Art/UI/MiniMap/UI_MiniMap_Boss.uexp", AssetBuilder.Texture("PF_B8G8R8A8", 2, 2, Bgra(Red)))],
            [new("UE_game/Content/Art/UI/MiniMap/UI_MiniMap_Boss.uasset", AssetBuilder.Package("None", "Texture2D", "Default__Texture2D")), new("UE_game/Content/Art/UI/MiniMap/UI_MiniMap_Boss.uexp", AssetBuilder.Texture("PF_B8G8R8A8", 2, 2, Bgra(Blue))), new(Config, AssetBuilder.MapIconConfig(new MapIconRow(2, "boss", "首领", "UI_MiniMap_Boss", 98), new MapIconRow(5, "door", "门", "UI_MiniMap_Boss", 1)))]);
        IconExport.Run(paks, folder, PakBuilder.Key, _ => { });
        Assert.Equal(Blue, AssetBuilder.Pixels(Path.Combine(folder, "UI_MiniMap_Boss.png")).Rgba);
        using var index = JsonDocument.Parse(File.ReadAllText(Path.Combine(folder, "index.json")));
        Assert.Equal("UE_game_10_P.pak", index.RootElement.GetProperty("icons").GetProperty("UI_MiniMap_Boss").GetProperty("pak").GetString());
        Assert.Equal(2, index.RootElement.GetProperty("map_icon_config").GetArrayLength());
        Assert.Equal(3, index.RootElement.GetProperty("source").EnumerateObject().Count());
    }

    [Fact]
    public void ReExportReplacesTheOldFolder()
    {
        using var temp = new TempDir();
        var (folder, paks) = Game(temp);
        temp.File(@"GameIcons\stale.png", "x");
        IconExport.Run(paks, folder, PakBuilder.Key, _ => { });
        Assert.False(File.Exists(Path.Combine(folder, "stale.png")));
        Assert.False(Directory.Exists(folder + ".new"));
        Assert.False(Directory.Exists(folder + ".old"));
    }

    [Fact]
    public void FailedExtractLeavesTheOldIconsAlone()
    {
        using var temp = new TempDir();
        var (folder, paks) = Game(temp);
        var old = temp.File(@"GameIcons\index.json", "old");
        var broken = temp.File(@"game\pakchunk1-WindowsNoEditor.pak", PakBuilder.Build(PakBuilder.Key, 0x5A6F12E1, 12, new PakSource("UE_game/x.bin", [1])));
        Assert.Throws<InvalidDataException>(() => IconExport.Run([.. paks, broken], folder, PakBuilder.Key, _ => { }));
        Assert.Equal("old", File.ReadAllText(old));
        Assert.False(Directory.Exists(folder + ".new"));
    }

    [Fact]
    public void MissingConfigIsAClearError()
    {
        using var temp = new TempDir();
        var pak = temp.File("a.pak", PakBuilder.Build(new PakSource("UE_game/x.bin", [1])));
        Assert.Contains("config_map_icon.bin", Assert.Throws<InvalidDataException>(() => IconExport.Run([pak], Path.Combine(temp.Path, "GameIcons"), PakBuilder.Key, _ => { })).Message);
    }

    [Fact]
    public void AnyPerIconFailureLandsInFailed()
    {
        using var temp = new TempDir();
        string[] flatNames = ["None", "PaperSprite", "Default__PaperSprite", "/Game/Art/UI/AtlasMap/Icon_Flat_Sprite", "/Game/Art/UI/AtlasMap/AtlasMap_Atlas", "StructProperty", "BakedSourceUV", "BakedSourceDimension"];
        var (folder, paks) = Game(temp, [[new("UE_game/Content/Art/UI/AtlasMap/Icon_Flat_Sprite.uasset", AssetBuilder.Package(flatNames)), new("UE_game/Content/Art/UI/AtlasMap/Icon_Flat_Sprite.uexp", AssetBuilder.Sprite(flatNames, (1, 1), (0, 0)))]]);
        Assert.Equal(new ExportResult(3, 2, 1), IconExport.Run(paks, folder, PakBuilder.Key, _ => { }));
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
    public void DescribeSaysNoneCurrentOrOutOfDate()
    {
        using var temp = new TempDir();
        var (folder, paks) = Game(temp);
        Assert.Equal("none", IconExport.Describe(folder, paks, 5975312));
        IconExport.Run(paks, folder, PakBuilder.Key, _ => { });
        Assert.Equal("3 icons, game build 5975312", IconExport.Describe(folder, paks, 5975312));
        Assert.Equal("3 icons, game build unknown", IconExport.Describe(folder, paks, null));
        File.AppendAllText(paks[0], "patched");
        Assert.Contains("out of date", IconExport.Describe(folder, paks, 5975312));
        var extra = temp.File(@"game\UE_game_99_P.pak", "x");
        Assert.Contains("out of date", IconExport.Describe(folder, [.. paks, extra], 5975312));
        File.WriteAllText(Path.Combine(folder, "index.json"), "{ nope");
        Assert.Contains("unreadable", IconExport.Describe(folder, paks, 5975312));
    }

    [Fact]
    public void ExtractMatchesTheCheckedInIconsWhenTheGameIsSet()
    {
        var game = RealGame.Install();
        var assets = RealGame.IconAssets();
        using var temp = new TempDir();
        var folder = Path.Combine(temp.Path, "GameIcons");
        var result = IconExport.Run(game.Paks(), folder, Convert.FromHexString(Offsets.PakAesKey), _ => { });
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
