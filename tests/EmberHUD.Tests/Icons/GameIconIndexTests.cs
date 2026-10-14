using System.Text.Json;
using EmberHUD.Host.Icons;

namespace EmberHUD.Tests;

public sealed class GameIconIndexTests
{
    static readonly string? Folder = Environment.GetEnvironmentVariable("TLI_ICON_ASSETS");

    [Fact]
    public void MapsEachNameToItsFileAndIgnoresCase()
    {
        var files = IconStore.ParseIndex("""{"icons":{"UI_MiniMap_Portal":{"file":"UI_MiniMap_Portal.png","size":[80,80]},"Icon_X":{"file":"Icon_X.png","rect":[1,2,3,4]}},"missing":[]}""");

        Assert.Equal(2, files.Count);
        Assert.Equal("UI_MiniMap_Portal.png", files["ui_minimap_portal"]);
        Assert.Equal("Icon_X.png", files["Icon_X"]);
    }

    [Fact]
    public void BadEntriesAreSkippedNotFatal()
    {
        var files = IconStore.ParseIndex("""{"icons":{"Good":{"file":"Good.png"},"NoFile":{"size":[1,1]},"NotString":{"file":7},"Empty":{"file":""},"Plain":"text","Up":{"file":"..\\Secret.png"},"Slash":{"file":"sub/Secret.png"},"Rooted":{"file":"C:\\x.png"}}}""");

        Assert.Equal(["Good"], files.Keys.ToArray());
    }

    [Fact]
    public void ADocumentWithoutAnIconsObjectIsEmpty()
    {
        Assert.Empty(IconStore.ParseIndex("""{"source":{}}"""));
        Assert.Empty(IconStore.ParseIndex("""{"icons":[]}"""));
        Assert.Empty(IconStore.ParseIndex("[]"));
    }

    [Fact]
    public void InvalidJsonThrowsJsonException() => Assert.ThrowsAny<JsonException>(() => IconStore.ParseIndex("{not json"));

    [Fact]
    public void EveryExtractedIconDecodesWhenTheFolderIsSet()
    {
        Assert.SkipUnless(Folder is not null, "TLI_ICON_ASSETS is not set");

        var files = IconStore.ParseIndex(File.ReadAllText(Path.Combine(Folder!, "index.json")));
        Assert.NotEmpty(files);
        var failures = new List<string>();
        foreach (var (name, file) in files)
            if (!PngDecoder.TryDecode(File.ReadAllBytes(Path.Combine(Folder!, file)), out _, out var failure)) failures.Add($"{name} - {failure}");

        Assert.Empty(failures);
        Assert.True(files.ContainsKey("UI_MiniMap_Portal") && files.ContainsKey("UI_MiniMap_Player1") && files.ContainsKey("UI_Fight_IconBoss"));
    }
}
