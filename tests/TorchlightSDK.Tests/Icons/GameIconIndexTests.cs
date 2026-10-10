using System.Text.Json;
using TorchlightSDK.Host.Icons;

namespace TorchlightSDK.Tests;

public sealed class GameIconIndexTests
{
    [Fact]
    public void MapsEachNameToItsFileAndIgnoresCase()
    {
        var files = GameIconIndex.Parse("""{"icons":{"UI_MiniMap_Portal":{"file":"UI_MiniMap_Portal.png","size":[80,80]},"Icon_X":{"file":"Icon_X.png","rect":[1,2,3,4]}},"missing":[]}""");

        Assert.Equal(2, files.Count);
        Assert.Equal("UI_MiniMap_Portal.png", files["ui_minimap_portal"]);
        Assert.Equal("Icon_X.png", files["Icon_X"]);
    }

    [Fact]
    public void BadEntriesAreSkippedNotFatal()
    {
        var files = GameIconIndex.Parse("""{"icons":{"Good":{"file":"Good.png"},"NoFile":{"size":[1,1]},"NotString":{"file":7},"Empty":{"file":""},"Plain":"text","Up":{"file":"..\\Secret.png"},"Slash":{"file":"sub/Secret.png"},"Rooted":{"file":"C:\\x.png"}}}""");

        Assert.Equal(["Good"], files.Keys.ToArray());
    }

    [Fact]
    public void ADocumentWithoutAnIconsObjectIsEmpty()
    {
        Assert.Empty(GameIconIndex.Parse("""{"source":{}}"""));
        Assert.Empty(GameIconIndex.Parse("""{"icons":[]}"""));
        Assert.Empty(GameIconIndex.Parse("[]"));
    }

    [Fact]
    public void InvalidJsonThrowsJsonException() => Assert.ThrowsAny<JsonException>(() => GameIconIndex.Parse("{not json"));
}
