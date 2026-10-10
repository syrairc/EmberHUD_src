using System.Drawing;
using Hexa.NET.ImGui;
using TorchlightSDK.Host.Icons;

namespace TorchlightSDK.Tests;

public sealed class SvgIconStoreTests : IDisposable
{
    const string Square = """<svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 64 64"><rect x="8" y="8" width="48" height="48" fill="currentColor"/><rect class="accent" x="24" y="24" width="16" height="16" fill="currentColor"/></svg>""";

    readonly LogCapture _log = new();
    readonly Dictionary<string, (int Width, int Height)> _uploaded = [];
    readonly List<string> _released = [];
    ulong _handle = 1;

    public void Dispose() => _log.Dispose();

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

    void Release(string key)
    {
        _released.Add(key);
        _uploaded.Remove(key);
    }

    SvgIconStore Store(Func<string, int, DecodedImage>? rasterize = null, Func<string, byte[], int, int, bool>? upload = null, Action<string>? release = null, TextureLookup? lookup = null) =>
        new(new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase) { ["svg:square"] = Square, ["svg:broken"] = "<svg><rect" }, upload ?? Upload, lookup ?? new TextureLookup(Lookup), release ?? new Action<string>(Release), rasterize, inline: true);

    static Sprite? Ready(SvgIconStore store, string icon, int pixels, Color colour, Color accent)
    {
        store.Resolve(icon, pixels, colour, accent);
        store.Pump();
        store.Pump();
        return store.Resolve(icon, pixels, colour, accent);
    }

    static DecodedImage OnePixel(string svg, int pixels) => new(1, 1, new byte[4]);

    [Theory]
    [InlineData(-3, 16)]
    [InlineData(0, 16)]
    [InlineData(1, 16)]
    [InlineData(8, 16)]
    [InlineData(9, 32)]
    [InlineData(16, 32)]
    [InlineData(17, 64)]
    [InlineData(100, 256)]
    [InlineData(int.MaxValue, 256)]
    public void SizesRoundUpToTwiceTheScreenSizeInPowersOfTwo(int screen, int bucket) => Assert.Equal(bucket, SvgIconStore.Bucket(screen));

    [Fact]
    public void AnIconRendersOnFirstUseAndThenResolvesToTheWholeImage()
    {
        var store = Store();

        Assert.Null(store.Resolve("svg:square", 12, Color.Red, Color.Blue));
        store.Pump();
        store.Pump();

        var sprite = store.Resolve("svg:square", 12, Color.Red, Color.Blue);
        Assert.NotNull(sprite);
        Assert.Equal(Vector2.Zero, sprite.Value.Uv0);
        Assert.Equal(Vector2.One, sprite.Value.Uv1);
        Assert.Equal((32, 32), Assert.Single(_uploaded).Value);
        Assert.Empty(_log.Text);
    }

    [Fact]
    public void OneTexturePerIconSizeBucketAndRgbColoursAlphaDoesNotCount()
    {
        var store = Store();
        Ready(store, "svg:square", 12, Color.Red, Color.Blue);
        for (var i = 0; i < 3; i++)
        {
            store.Resolve("svg:square", 14, Color.FromArgb(40, Color.Red), Color.FromArgb(7, Color.Blue));
            store.Pump();
        }
        Assert.Single(_uploaded);

        Assert.NotNull(Ready(store, "svg:square", 12, Color.Lime, Color.Blue));
        Assert.NotNull(Ready(store, "svg:square", 12, Color.Red, Color.Yellow));
        Assert.NotNull(Ready(store, "svg:square", 40, Color.Red, Color.Blue));
        Assert.Equal(4, _uploaded.Count);
    }

    [Fact]
    public void UnknownIconsStayNullAndWarnOncePerName()
    {
        var store = Store();

        for (var i = 0; i < 3; i++) Assert.Null(Ready(store, "svg:nothing", 12, Color.Red, Color.Blue));
        Assert.Null(Ready(store, "svg:other", 12, Color.Red, Color.Blue));
        Assert.Empty(_uploaded);
        var messages = _log.Messages().ToList();
        Assert.Equal(2, messages.Count);
        Assert.Contains("svg:nothing", messages[0]);
        Assert.Contains("svg:other", messages[1]);
    }

    [Fact]
    public void ABrokenSvgWarnsOnceAndIsNeverRenderedAgain()
    {
        var renders = 0;
        var store = Store((svg, pixels) =>
        {
            renders++;
            return SvgRasterizer.Rasterize(svg, pixels);
        });

        foreach (var colour in new[] { Color.Red, Color.Lime, Color.Blue, Color.Red }) Assert.Null(Ready(store, "svg:broken", 12, colour, Color.Black));

        Assert.Equal(1, renders);
        Assert.Contains("svg:broken", Assert.Single(_log.Messages()));
        Assert.Empty(_uploaded);
        Assert.NotNull(Ready(store, "svg:square", 12, Color.Red, Color.Blue));
    }

    [Fact]
    public void AMissingSkiaTurnsSvgIconsOffWithOneWarning()
    {
        var renders = 0;
        var store = Store((_, _) =>
        {
            renders++;
            throw new DllNotFoundException("libSkiaSharp");
        });

        Assert.Null(Ready(store, "svg:square", 12, Color.Red, Color.Blue));
        Assert.Null(Ready(store, "svg:square", 12, Color.Lime, Color.Blue));
        store.Resolve("svg:square", 40, Color.Red, Color.Blue);
        store.Pump();

        Assert.Equal(1, renders);
        Assert.Contains("off", Assert.Single(_log.Messages()));
    }

    [Fact]
    public void AFailedOrThrowingUploadWarnsOnceAndFallsBack()
    {
        var store = Store(upload: (_, _, _, _) => false);
        Assert.Null(Ready(store, "svg:square", 12, Color.Red, Color.Blue));
        Assert.Null(Ready(store, "svg:square", 12, Color.Lime, Color.Blue));
        Assert.Single(_log.Messages());

        var throwing = Store(upload: (_, _, _, _) => throw new ArgumentException("bad size"));
        Assert.Null(Ready(throwing, "svg:square", 12, Color.Red, Color.Blue));
        Assert.Equal(2, _log.Messages().Count);
    }

    [Fact]
    public void TheLeastRecentlyUsedTexturesAreReleasedPastTheCap()
    {
        var store = Store(OnePixel);
        var favourite = Color.FromArgb(0);
        Ready(store, "svg:square", 12, favourite, Color.Blue);

        for (var i = 1; i <= 300; i++)
        {
            Assert.NotNull(Ready(store, "svg:square", 12, Color.FromArgb(i), Color.Blue));
            Assert.NotNull(store.Resolve("svg:square", 12, favourite, Color.Blue));
        }

        Assert.Equal(256, _uploaded.Count);
        Assert.Equal(301 - 256, _released.Count);
        Assert.Equal(_released.Count, _released.Distinct().Count());
        Assert.NotNull(store.Resolve("svg:square", 12, favourite, Color.Blue));
        Assert.Null(store.Resolve("svg:square", 12, Color.FromArgb(1), Color.Blue));
    }

    [Fact]
    public void ARebuiltDeviceHandsOutTheNewTextureHandle()
    {
        var store = Store();
        var before = Ready(store, "svg:square", 12, Color.Red, Color.Blue)!.Value.Texture.TexID;

        _handle = 2;

        Assert.NotEqual(before, store.Resolve("svg:square", 12, Color.Red, Color.Blue)!.Value.Texture.TexID);
    }

    [Fact]
    public void ResolvingAReadyIconAllocatesNothing()
    {
        var store = Store();
        Assert.NotNull(Ready(store, "svg:square", 12, Color.Red, Color.Blue));
        for (var i = 0; i < 100; i++) store.Resolve("svg:square", 12, Color.Red, Color.Blue);

        var before = GC.GetAllocatedBytesForCurrentThread();
        for (var i = 0; i < 1000; i++) store.Resolve("svg:square", 12, Color.Red, Color.Blue);

        Assert.Equal(before, GC.GetAllocatedBytesForCurrentThread());
    }

    [Fact]
    public void ARequestFloodIsCappedAndAFreshColourStillRendersOnceTheQueueDrains()
    {
        var renders = 0;
        var store = Store((svg, pixels) =>
        {
            renders++;
            return OnePixel(svg, pixels);
        });

        for (var i = 1; i <= 1000; i++) Assert.Null(store.Resolve("svg:square", 12, Color.FromArgb(i), Color.Blue));
        for (var i = 0; i < 2000; i++) store.Pump();

        Assert.InRange(renders, 1, 64);
        Assert.Equal(renders, _uploaded.Count);
        Assert.NotNull(Ready(store, "svg:square", 12, Color.FromArgb(0xABCDEF), Color.Blue));
        Assert.Empty(_log.Text);
    }

    [Fact]
    public void AThrowingReleaseStillEvictsPastTheCapWithoutThrowing()
    {
        var releases = 0;
        var store = Store(OnePixel, release: _ => { releases++; throw new InvalidOperationException("device removed"); });

        for (var i = 0; i < 261; i++) Assert.NotNull(Ready(store, "svg:square", 12, Color.FromArgb(i), Color.Blue));

        Assert.Equal(5, releases);
        Assert.Null(store.Resolve("svg:square", 12, Color.FromArgb(0), Color.Blue));
        Assert.NotNull(store.Resolve("svg:square", 12, Color.FromArgb(260), Color.Blue));
    }

    [Fact]
    public void AThrowingLookupResolvesToNullWithoutThrowing()
    {
        var store = Store(lookup: (string key, out ImTextureRef texture) => throw new InvalidOperationException("device removed"));

        Assert.Null(Ready(store, "svg:square", 12, Color.Red, Color.Blue));
        Assert.Single(_uploaded);
    }
}
