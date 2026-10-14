using EmberHUD.Host.Icons;

namespace EmberHUD.Tests;

public sealed class AssetIconsTests
{
    static readonly string? Folder = Environment.GetEnvironmentVariable("TLI_ICON_ASSETS");

    [Fact]
    public void EveryExtractedIconDecodesWhenTheFolderIsSet()
    {
        Assert.SkipUnless(Folder is not null, "TLI_ICON_ASSETS is not set");

        var files = GameIconIndex.Parse(File.ReadAllText(Path.Combine(Folder!, "index.json")));
        Assert.NotEmpty(files);
        var failures = new List<string>();
        foreach (var (name, file) in files)
            if (!PngDecoder.TryDecode(File.ReadAllBytes(Path.Combine(Folder!, file)), out _, out var failure)) failures.Add($"{name} - {failure}");

        Assert.Empty(failures);
        Assert.True(files.ContainsKey("UI_MiniMap_Portal") && files.ContainsKey("UI_MiniMap_Player1") && files.ContainsKey("UI_Fight_IconBoss"));
    }
}
