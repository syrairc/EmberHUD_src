using System.Text.Json;

namespace EmberHUD.Assets.Tests;

public class AssetJobTests
{
    const string Config = "UE_game/Content/Script/Data/ConfigView/config_map_icon_ChunkA0.bin";
    const string Textures = "UE_game/Content/Art/UI/Textures/Common/";
    static readonly byte[] Red = [255, 0, 0, 255, 255, 0, 0, 255, 255, 0, 0, 255, 255, 0, 0, 255];

    static byte[] Bgra(byte[] rgba) => [.. rgba.Chunk(4).SelectMany(p => new[] { p[2], p[1], p[0], p[3] })];

    static PakSource[] Texture(string path) =>
    [
        new(path + ".uasset", AssetBuilder.Package("None", "Texture2D", "Default__Texture2D")),
        new(path + ".uexp", AssetBuilder.Texture("PF_B8G8R8A8", 2, 2, Bgra(Red))),
    ];

    static PakSource[] Files(params string[] textures) => [.. textures.SelectMany(Texture), new(Config, AssetBuilder.MapIconConfig(new MapIconRow(2, "boss", "boss", "UI_MiniMap_Boss", 98)))];

    static string Pak(TempDir temp, string name, params PakSource[] files) => temp.File($@"game\{name}", PakBuilder.Build(files));

    static IAssetJob Job(AssetType type) => AssetJobs.All.Single(job => job.Type == type);

    static string Root(TempDir temp) => Path.Combine(temp.Path, "GameIcons");

    [Fact]
    public void FolderJob_ExportsOnlyMatchingFolders_ToItsOwnFolderWithIndex()
    {
        using var temp = new TempDir();
        var pak = Pak(temp, "a.pak", Files(Textures + "Icon/Item/128/A", Textures + "Icon/Skill/Buff/32/B", "UE_game/Content/Season/S8/Icon/GamePlay/C", "UE_game/Content/Elsewhere/EquipCommon/Weapon/128/D"));
        var result = Job(AssetType.Items).Run([pak], Root(temp), PakBuilder.Key, _ => { }, CancellationToken.None);
        var folder = Path.Combine(Root(temp), "Items");
        Assert.Equal(new AssetResult(3, 0, 0), result);
        Assert.Equal(Red, AssetBuilder.Pixels(Path.Combine(folder, "A.png")).Rgba);
        Assert.True(File.Exists(Path.Combine(folder, "C.png")));
        Assert.True(File.Exists(Path.Combine(folder, "D.png")));
        Assert.False(File.Exists(Path.Combine(folder, "B.png")));
        using var index = JsonDocument.Parse(File.ReadAllText(Path.Combine(folder, "index.json")));
        Assert.Equal(["A", "C", "D"], index.RootElement.GetProperty("icons").EnumerateObject().Select(icon => icon.Name));
        Assert.Equal(["a.pak"], index.RootElement.GetProperty("source").EnumerateObject().Select(source => source.Name));
        Assert.Empty(Directory.GetDirectories(Root(temp), "Items.new*"));
    }

    [Fact]
    public void Status_NotExtracted_Current_OutOfDate()
    {
        using var temp = new TempDir();
        var pak = Pak(temp, "a.pak", Files(Textures + "Icon/Item/128/A"));
        var job = Job(AssetType.Items);
        Assert.Equal(new AssetStatus(AssetState.NotExtracted, 0, 0), job.Status(Root(temp), [pak]));

        job.Run([pak], Root(temp), PakBuilder.Key, _ => { }, CancellationToken.None);
        var current = job.Status(Root(temp), [pak]);
        Assert.Equal(AssetState.Current, current.State);
        Assert.Equal(1, current.Files);
        Assert.Equal(new FileInfo(Path.Combine(Root(temp), "Items", "A.png")).Length, current.Bytes);

        File.AppendAllText(pak, "patched");
        Assert.Equal(AssetState.OutOfDate, job.Status(Root(temp), [pak]).State);
        Assert.Equal(AssetState.OutOfDate, job.Status(Root(temp), []).State);

        File.WriteAllText(Path.Combine(Root(temp), "Items", "index.json"), "{ nope");
        Assert.Equal(AssetState.Unreadable, job.Status(Root(temp), [pak]).State);
    }

    [Fact]
    public void Status_ComparesOnlyPaksThatSuppliedFiles()
    {
        using var temp = new TempDir();
        var supplier = Pak(temp, "a.pak", Files(Textures + "Icon/Item/128/A"));
        var unrelated = Pak(temp, "b.pak", Files(Textures + "Icon/Skill/Buff/32/B"));
        var job = Job(AssetType.Items);
        job.Run([supplier, unrelated], Root(temp), PakBuilder.Key, _ => { }, CancellationToken.None);
        File.AppendAllText(unrelated, "patched");
        Assert.Equal(AssetState.Current, job.Status(Root(temp), [supplier, unrelated]).State);
        File.AppendAllText(supplier, "patched");
        Assert.Equal(AssetState.OutOfDate, job.Status(Root(temp), [supplier, unrelated]).State);
    }

    [Fact]
    public void MinimapRun_LeavesOtherTypeFoldersAlone()
    {
        using var temp = new TempDir();
        var pak = Pak(temp, "a.pak", Files("UE_game/Content/Art/UI/MiniMap/UI_MiniMap_Boss"));
        temp.File(@"GameIcons\Items\x.png", "x");
        temp.File(@"GameIcons\Items\index.json", "items");
        temp.File(@"GameIcons\Old.png", "old");
        temp.File(@"GameIcons\Keep.txt", "keep");
        temp.File(@"GameIcons\index.json", "{\"source\":{},\"icons\":{\"Old\":{\"file\":\"Old.png\",\"pak\":\"a.pak\"}},\"missing\":[],\"failed\":{}}");

        var result = Job(AssetType.Minimap).Run([pak], Root(temp), PakBuilder.Key, _ => { }, CancellationToken.None);

        Assert.Equal(new AssetResult(1, 0, 0), result);
        Assert.Equal("x", File.ReadAllText(Path.Combine(Root(temp), "Items", "x.png")));
        Assert.Equal("items", File.ReadAllText(Path.Combine(Root(temp), "Items", "index.json")));
        Assert.Equal("keep", File.ReadAllText(Path.Combine(Root(temp), "Keep.txt")));
        Assert.False(File.Exists(Path.Combine(Root(temp), "Old.png")));
        Assert.Equal(Red, AssetBuilder.Pixels(Path.Combine(Root(temp), "UI_MiniMap_Boss.png")).Rgba);
        Assert.Empty(Directory.GetDirectories(temp.Path, "GameIcons.new*"));
        Assert.Equal(AssetState.Current, Job(AssetType.Minimap).Status(Root(temp), [pak]).State);
    }

    [Fact]
    public void Status_IndexWithNullFile_IsUnreadable()
    {
        using var temp = new TempDir();
        var pak = Pak(temp, "a.pak", Files(Textures + "Icon/Item/128/A"));
        temp.File(@"GameIcons\Items\index.json", "{\"source\":{\"a.pak\":1},\"icons\":{\"A\":{\"file\":null}},\"missing\":[],\"failed\":{}}");
        Assert.Equal(AssetState.Unreadable, Job(AssetType.Items).Status(Root(temp), [pak]).State);
    }

    [Fact]
    public void Status_IndexWithNoSourcePaks_IsOutOfDate()
    {
        using var temp = new TempDir();
        var pak = Pak(temp, "a.pak", Files(Textures + "Icon/Item/128/A"));
        temp.File(@"GameIcons\Items\index.json", "{\"source\":{},\"icons\":{},\"missing\":[],\"failed\":{}}");
        Assert.Equal(AssetState.OutOfDate, Job(AssetType.Items).Status(Root(temp), [pak]).State);
    }

    [Fact]
    public void MinimapRun_OldIndexWithNullFile_StillRuns()
    {
        using var temp = new TempDir();
        var pak = Pak(temp, "a.pak", Files("UE_game/Content/Art/UI/MiniMap/UI_MiniMap_Boss"));
        temp.File(@"GameIcons\index.json", "{\"source\":{},\"icons\":{\"Old\":{\"file\":null}},\"missing\":[],\"failed\":{}}");
        Assert.Equal(new AssetResult(1, 0, 0), Job(AssetType.Minimap).Run([pak], Root(temp), PakBuilder.Key, _ => { }, CancellationToken.None));
    }

    [Fact]
    public void Run_Cancelled_KeepsOldSet()
    {
        using var temp = new TempDir();
        var pak = Pak(temp, "a.pak", Files(Textures + "Icon/Item/128/A", Textures + "Icon/Item/128/B"));
        var old = temp.File(@"GameIcons\Items\index.json", "old");
        using var cancel = new CancellationTokenSource();
        Assert.Throws<OperationCanceledException>(() => Job(AssetType.Items).Run([pak], Root(temp), PakBuilder.Key, _ => cancel.Cancel(), cancel.Token));
        Assert.Equal("old", File.ReadAllText(old));
        Assert.Empty(Directory.GetDirectories(Root(temp), "Items.new*"));
    }

    [Fact]
    public void Run_Failure_KeepsOldSet()
    {
        using var temp = new TempDir();
        var good = Pak(temp, "a.pak", Files(Textures + "Icon/Item/128/A"));
        var broken = temp.File(@"game\b.pak", PakBuilder.Build(PakBuilder.Key, 0x5A6F12E1, 12, new PakSource("UE_game/x.bin", [1])));
        var old = temp.File(@"GameIcons\Items\index.json", "old");
        Assert.Throws<InvalidDataException>(() => Job(AssetType.Items).Run([good, broken], Root(temp), PakBuilder.Key, _ => { }, CancellationToken.None));
        Assert.Equal("old", File.ReadAllText(old));
        Assert.Empty(Directory.GetDirectories(Root(temp), "Items.new*"));
    }
}
