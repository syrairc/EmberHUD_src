using Hexa.NET.ImGui;
using EmberHUD.Host.Icons;

namespace EmberHUD.Tests;

public sealed class IconStoreTests : IDisposable
{
    readonly string _directory = Directory.CreateTempSubdirectory("emberhud-icons-").FullName;
    readonly LogCapture _log = new();
    readonly Dictionary<string, (int Width, int Height)> _uploaded = [];
    ulong _handle = 1;

    public void Dispose()
    {
        _log.Dispose();
        Directory.Delete(_directory, true);
    }

    bool Upload(string key, byte[] pixels, int width, int height)
    {
        _uploaded[key] = (width, height);
        return true;
    }

    bool Lookup(string key, out ImTextureRef texture)
    {
        texture = new ImTextureRef { TexID = new ImTextureID(_handle) };
        return _uploaded.ContainsKey(key);
    }

    void Write(string name, byte[] bytes) => File.WriteAllBytes(Path.Combine(_directory, name), bytes);

    void WriteIndex(string json) => File.WriteAllText(Path.Combine(_directory, "index.json"), json);

    IconStore Open()
    {
        WriteIndex("""{"icons":{"UI_MiniMap_Portal":{"file":"UI_MiniMap_Portal.png"},"Broken":{"file":"Broken.png"},"Gone":{"file":"Gone.png"}}}""");
        Write("UI_MiniMap_Portal.png", PngFixture.Rgba(3, 2, PngFixture.Gradient(3, 2), [0]));
        Write("Broken.png", [1, 2, 3, 4]);
        return IconStore.Open(_directory, Upload, Lookup, inline: true)!;
    }

    [Fact]
    public void NamesListsTheIndexKeysSortedOrdinal() => Assert.Equal(["Broken", "Gone", "UI_MiniMap_Portal"], Open().Names);

    IconStore OpenTwoPngs(Func<string, byte[], int, int, bool> upload)
    {
        WriteIndex("""{"icons":{"A":{"file":"A.png"},"B":{"file":"B.png"}}}""");
        Write("A.png", PngFixture.Rgba(2, 2, PngFixture.Gradient(2, 2), [0]));
        Write("B.png", PngFixture.Rgba(2, 2, PngFixture.Gradient(2, 2), [0]));
        return IconStore.Open(_directory, upload, Lookup, inline: true)!;
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
        Assert.Equal((3, 2), _uploaded["game:ui_minimap_portal"]);
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

        Assert.Single(_uploaded);
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

        Assert.Empty(_uploaded);
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
        Assert.Empty(_uploaded);
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
        var store = IconStore.Open(_directory, Upload, Lookup, inline: true)!;

        for (var i = 0; i < 4; i++)
        {
            Assert.Null(store.Resolve("Dir"));
            store.Pump();
        }

        Assert.Single(_log.Messages());
        Assert.Empty(_uploaded);
    }

    [Fact]
    public void AFailedUploadWarnsAndFallsBackAndTheNextTextureStillLoads()
    {
        var store = OpenTwoPngs((key, pixels, width, height) => key != "game:a" && Upload(key, pixels, width, height));

        store.Resolve("A");
        store.Resolve("B");
        for (var i = 0; i < 6; i++) store.Pump();

        Assert.Null(store.Resolve("A"));
        Assert.NotNull(store.Resolve("B"));
        Assert.Single(_log.Messages());
    }

    [Fact]
    public void AnUploadThatThrowsWarnsOnceAndNeverThrows()
    {
        var store = OpenTwoPngs((key, pixels, width, height) => key == "game:a" ? throw new InvalidOperationException("device lost") : Upload(key, pixels, width, height));

        store.Resolve("A");
        store.Resolve("B");
        for (var i = 0; i < 6; i++) store.Pump();

        Assert.Null(store.Resolve("A"));
        Assert.NotNull(store.Resolve("B"));
        Assert.Single(_log.Messages());
        Assert.Contains("device lost", _log.Messages()[0]);
    }

    [Fact]
    public void ARebuiltDeviceHandsOutTheNewTextureHandle()
    {
        var store = Open();
        store.Resolve("UI_MiniMap_Portal");
        store.Pump();
        store.Pump();
        var before = store.Resolve("UI_MiniMap_Portal")!.Value.Texture.TexID;

        _handle = 2;

        Assert.NotEqual(before, store.Resolve("UI_MiniMap_Portal")!.Value.Texture.TexID);
    }

    [Fact]
    public void AMissingFolderOrIndexTurnsGameIconsOffWithOneWarning()
    {
        Assert.Null(IconStore.Open(Path.Combine(_directory, "nope"), Upload, Lookup, inline: true));
        Assert.Single(_log.Messages());

        Assert.Null(IconStore.Open(_directory, Upload, Lookup, inline: true));
        Assert.Equal(2, _log.Messages().Count);
    }

    [Fact]
    public void ACorruptOrEmptyIndexTurnsGameIconsOffWithOneWarning()
    {
        WriteIndex("{not json");
        Assert.Null(IconStore.Open(_directory, Upload, Lookup, inline: true));
        Assert.Single(_log.Messages());

        WriteIndex("""{"icons":{}}""");
        Assert.Null(IconStore.Open(_directory, Upload, Lookup, inline: true));
        Assert.Equal(2, _log.Messages().Count);
    }
}
