using EmberHUD.Host.Icons;

namespace EmberHUD.Tests;

public sealed class PngFileLoaderTests : IDisposable
{
    readonly LogCapture _log = new();
    readonly string _dir = Directory.CreateTempSubdirectory().FullName;
    readonly List<string> _uploads = [];

    public void Dispose()
    {
        _log.Dispose();
        Directory.Delete(_dir, true);
    }

    PngFileLoader Loader() => new((plugin, key, rgba, width, height) => _uploads.Add($"{key} {rgba.Length} {width}x{height}"));

    [Fact]
    public void LoadPngUploadsADecodedFile()
    {
        var path = Path.Combine(_dir, "a.png");
        File.WriteAllBytes(path, PngFixture.Rgba(3, 2, PngFixture.Gradient(3, 2), [0]));

        Assert.True(Loader().Load("icon", path));
        Assert.Equal(["icon 24 3x2"], _uploads);
    }

    [Fact]
    public void LoadPngReturnsFalseForAMissingFile()
    {
        var loader = Loader();
        var path = Path.Combine(_dir, "missing.png");

        Assert.False(loader.Load("icon", path));
        Assert.False(loader.Load("icon", path));
        Assert.Empty(_uploads);
        Assert.Single(_log.Messages());
    }

    [Fact]
    public void LoadPngReturnsFalseForANonPngFile()
    {
        var path = Path.Combine(_dir, "junk.png");
        File.WriteAllBytes(path, [1, 2, 3, 4, 5, 6, 7, 8, 9, 10]);
        var loader = Loader();

        Assert.False(loader.Load("icon", path));
        Assert.False(loader.Load("icon", path));
        Assert.Empty(_uploads);
        Assert.Single(_log.Messages());
    }
}
