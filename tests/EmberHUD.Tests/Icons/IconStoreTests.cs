using Hexa.NET.ImGui;
using EmberHUD.Host.Icons;

namespace EmberHUD.Tests;

public sealed class IconStoreTests : IDisposable
{
    readonly TempDir _temp = new();
    readonly LogCapture _log = new();
    readonly FakeTextures _textures = new();

    string _directory => _temp.Path;

    public void Dispose()
    {
        _log.Dispose();
        _temp.Dispose();
    }

    bool UploadFailingFor(string bad, string reason, string key, byte[] pixels, int width, int height, out string failure)
    {
        if (key != bad) return _textures.Upload(key, pixels, width, height, out failure);
        failure = reason;
        return false;
    }

    void Write(string name, byte[] bytes) => File.WriteAllBytes(Path.Combine(_directory, name), bytes);

    void WriteIndex(string json) => File.WriteAllText(Path.Combine(_directory, "index.json"), json);

    IconStore Open()
    {
        WriteIndex("""{"icons":{"UI_MiniMap_Portal":{"file":"UI_MiniMap_Portal.png"},"Broken":{"file":"Broken.png"},"Gone":{"file":"Gone.png"}}}""");
        Write("UI_MiniMap_Portal.png", PngFixture.Rgba(3, 2, PngFixture.Gradient(3, 2), [0]));
        Write("Broken.png", [1, 2, 3, 4]);
        return IconStore.Open(_directory, _textures.Upload, _textures.Resolve, inline: true)!;
    }

    [Fact]
    public void NamesListsTheIndexKeysSortedOrdinal() => Assert.Equal(["Broken", "Gone", "UI_MiniMap_Portal"], Open().Names);

    IconStore OpenTwoPngs(TextureUpload upload)
    {
        WriteIndex("""{"icons":{"A":{"file":"A.png"},"B":{"file":"B.png"}}}""");
        Write("A.png", PngFixture.Rgba(2, 2, PngFixture.Gradient(2, 2), [0]));
        Write("B.png", PngFixture.Rgba(2, 2, PngFixture.Gradient(2, 2), [0]));
        return IconStore.Open(_directory, upload, _textures.Resolve, inline: true)!;
    }

    [Fact]
    public void ATextureLoadsOnFirstUseAndThenResolvesToTheWholeImage()
    {
        var store = Open();

        Assert.Null(store.Resolve("UI_MiniMap_Portal"));
        store.Pump();
        store.Pump();

        var sprite = store.Resolve("ui_minimap_portal");
        Assert.NotNull(sprite);
        Assert.Equal(Vector2.Zero, sprite.Value.Uv0);
        Assert.Equal(Vector2.One, sprite.Value.Uv1);
        Assert.Equal((3, 2), _textures.Uploaded["game:ui_minimap_portal"]);
        Assert.Empty(_log.Text);
    }

    [Fact]
    public void ATextureLoadsOnlyOnce()
    {
        var store = Open();
        for (var i = 0; i < 4; i++)
        {
            store.Resolve("UI_MiniMap_Portal");
            store.Pump();
        }

        Assert.Single(_textures.Uploaded);
    }

    [Fact]
    public void AnUnknownNameStaysNullWithoutALogLine()
    {
        var store = Open();

        Assert.Null(store.Resolve("UI_MiniMap_Nothing"));
        Assert.Null(store.Resolve(""));
        Assert.Null(store.Resolve(null!));
        store.Pump();
        store.Pump();

        Assert.Empty(_textures.Uploaded);
        Assert.Empty(_log.Text);
    }

    [Fact]
    public void ABrokenPngWarnsOnceAndIsNeverRetried()
    {
        var store = Open();
        for (var i = 0; i < 6; i++)
        {
            Assert.Null(store.Resolve("Broken"));
            store.Pump();
        }

        Assert.Single(_log.Messages());
        Assert.Contains("Broken", _log.Messages()[0]);
        Assert.Empty(_textures.Uploaded);
    }

    [Fact]
    public void AMissingFileWarnsOnceAndStaysNull()
    {
        var store = Open();
        for (var i = 0; i < 4; i++)
        {
            Assert.Null(store.Resolve("Gone"));
            store.Pump();
        }

        Assert.Single(_log.Messages());
        Assert.Null(store.Resolve("Gone"));
    }

    [Fact]
    public void ADirectoryNamedLikeAPngWarnsOnceAndNeverThrows()
    {
        WriteIndex("""{"icons":{"Dir":{"file":"Dir.png"}}}""");
        Directory.CreateDirectory(Path.Combine(_directory, "Dir.png"));
        var store = IconStore.Open(_directory, _textures.Upload, _textures.Resolve, inline: true)!;

        for (var i = 0; i < 4; i++)
        {
            Assert.Null(store.Resolve("Dir"));
            store.Pump();
        }

        Assert.Single(_log.Messages());
        Assert.Empty(_textures.Uploaded);
    }

    [Fact]
    public void AFailedUploadWarnsOnceWithItsReasonAndTheNextTextureStillLoads()
    {
        var store = OpenTwoPngs((string key, byte[] pixels, int width, int height, out string failure) => UploadFailingFor("game:a", "upload failed - device lost", key, pixels, width, height, out failure));

        store.Resolve("A");
        store.Resolve("B");
        for (var i = 0; i < 6; i++) store.Pump();

        Assert.Null(store.Resolve("A"));
        Assert.NotNull(store.Resolve("B"));
        Assert.Contains("device lost", Assert.Single(_log.Messages()));
    }

    [Fact]
    public void ARebuiltDeviceHandsOutTheNewTextureHandle()
    {
        var store = Open();
        store.Resolve("UI_MiniMap_Portal");
        store.Pump();
        store.Pump();
        var before = store.Resolve("UI_MiniMap_Portal")!.Value.Texture.TexID;

        _textures.Handle = 2;

        Assert.NotEqual(before, store.Resolve("UI_MiniMap_Portal")!.Value.Texture.TexID);
    }

    [Fact]
    public void AMissingFolderOrIndexTurnsGameIconsOffWithOneWarning()
    {
        Assert.Null(IconStore.Open(Path.Combine(_directory, "nope"), _textures.Upload, _textures.Resolve, inline: true));
        Assert.Single(_log.Messages());

        Assert.Null(IconStore.Open(_directory, _textures.Upload, _textures.Resolve, inline: true));
        Assert.Equal(2, _log.Messages().Count);
    }

    [Fact]
    public void ACorruptOrEmptyIndexTurnsGameIconsOffWithOneWarning()
    {
        WriteIndex("{not json");
        Assert.Null(IconStore.Open(_directory, _textures.Upload, _textures.Resolve, inline: true));
        Assert.Single(_log.Messages());

        WriteIndex("""{"icons":{}}""");
        Assert.Null(IconStore.Open(_directory, _textures.Upload, _textures.Resolve, inline: true));
        Assert.Equal(2, _log.Messages().Count);
    }
}
