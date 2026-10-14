using System.Collections;
using System.Drawing;
using Hexa.NET.ImGui;
using EmberHUD.Host.Icons;

namespace EmberHUD.Tests;

public sealed class SvgIconStoreTests : IDisposable
{
    sealed class CountingSources(Dictionary<string, string> inner) : IReadOnlyDictionary<string, string>
    {
        public int Lookups;

        public string this[string key]
        {
            get
            {
                Lookups++;
                return inner[key];
            }
        }

        public IEnumerable<string> Keys => inner.Keys;

        public IEnumerable<string> Values => inner.Values;

        public int Count => inner.Count;

        public bool ContainsKey(string key)
        {
            Lookups++;
            return inner.ContainsKey(key);
        }

        public bool TryGetValue(string key, out string value)
        {
            Lookups++;
            return inner.TryGetValue(key, out value!);
        }

        public IEnumerator<KeyValuePair<string, string>> GetEnumerator() => inner.GetEnumerator();

        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
    }

    const string Square = """<svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 64 64"><rect x="8" y="8" width="48" height="48" fill="currentColor"/><rect class="accent" x="24" y="24" width="16" height="16" fill="currentColor"/></svg>""";

    readonly LogCapture _log = new();
    readonly FakeTextures _textures = new();

    public void Dispose() => _log.Dispose();

    SvgIconStore Store(Func<string, int, DecodedImage>? rasterize = null, TextureUpload? upload = null, Action<string>? release = null) =>
        new(new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase) { ["svg:square"] = Square, ["svg:broken"] = "<svg><rect" }, upload ?? _textures.Upload, _textures.Resolve, release ?? new Action<string>(_textures.Release), rasterize, inline: true);

    [Fact]
    public void NamesListsTheSourceKeysSortedOrdinal() => Assert.Equal(["svg:broken", "svg:square"], Store().Names);

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
    public void DifferentSpellingsOfAnIconShareOneTexture()
    {
        var store = Store();

        store.Resolve("SVG:Square", 12, Color.Red, Color.Blue);
        store.Resolve("svg:square", 12, Color.Red, Color.Blue);
        store.Pump();
        store.Pump();
        store.Pump();

        Assert.Single(_textures.Uploaded);
        Assert.NotNull(store.Resolve("SVG:SQUARE", 12, Color.Red, Color.Blue));
        Assert.NotNull(store.Resolve("svg:square", 12, Color.Red, Color.Blue));
        Assert.Single(_textures.Uploaded);
    }

    [Fact]
    public void AHitNeverTouchesTheSources()
    {
        var sources = new CountingSources(new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase) { ["svg:square"] = Square });
        var store = new SvgIconStore(sources, _textures.Upload, _textures.Resolve, _textures.Release, rasterize: null, inline: true);
        Assert.NotNull(Ready(store, "svg:square", 12, Color.Red, Color.Blue));
        var before = sources.Lookups;

        for (var i = 0; i < 100; i++) Assert.NotNull(store.Resolve("svg:square", 12, Color.Red, Color.Blue));

        Assert.Equal(before, sources.Lookups);
    }

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
        Assert.Equal((32, 32), Assert.Single(_textures.Uploaded).Value);
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
        Assert.Single(_textures.Uploaded);

        Assert.NotNull(Ready(store, "svg:square", 12, Color.Lime, Color.Blue));
        Assert.NotNull(Ready(store, "svg:square", 12, Color.Red, Color.Yellow));
        Assert.NotNull(Ready(store, "svg:square", 40, Color.Red, Color.Blue));
        Assert.Equal(4, _textures.Uploaded.Count);
    }

    [Fact]
    public void UnknownIconsStayNullAndWarnOncePerName()
    {
        var store = Store();

        for (var i = 0; i < 3; i++) Assert.Null(Ready(store, "svg:nothing", 12, Color.Red, Color.Blue));
        Assert.Null(Ready(store, "svg:other", 12, Color.Red, Color.Blue));
        Assert.Empty(_textures.Uploaded);
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
            return SvgIcons.Rasterize(svg, pixels);
        });

        foreach (var colour in new[] { Color.Red, Color.Lime, Color.Blue, Color.Red }) Assert.Null(Ready(store, "svg:broken", 12, colour, Color.Black));

        Assert.Equal(1, renders);
        Assert.Contains("svg:broken", Assert.Single(_log.Messages()));
        Assert.Empty(_textures.Uploaded);
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
    public void AFailedUploadWarnsOnceWithItsReasonAndFallsBack()
    {
        var store = Store(upload: (string _, byte[] _, int _, int _, out string failure) =>
        {
            failure = "upload failed - device lost";
            return false;
        });
        Assert.Null(Ready(store, "svg:square", 12, Color.Red, Color.Blue));
        Assert.Null(Ready(store, "svg:square", 12, Color.Lime, Color.Blue));
        Assert.Contains("device lost", Assert.Single(_log.Messages()));
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

        Assert.Equal(256, _textures.Uploaded.Count);
        Assert.Equal(301 - 256, _textures.Released.Count);
        Assert.Equal(_textures.Released.Count, _textures.Released.Distinct().Count());
        Assert.NotNull(store.Resolve("svg:square", 12, favourite, Color.Blue));
        Assert.Null(store.Resolve("svg:square", 12, Color.FromArgb(1), Color.Blue));
    }

    [Fact]
    public void ARebuiltDeviceHandsOutTheNewTextureHandle()
    {
        var store = Store();
        var before = Ready(store, "svg:square", 12, Color.Red, Color.Blue)!.Value.Texture.TexID;

        _textures.Handle = 2;

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
        Assert.Equal(renders, _textures.Uploaded.Count);
        Assert.NotNull(Ready(store, "svg:square", 12, Color.FromArgb(0xABCDEF), Color.Blue));
        Assert.Empty(_log.Text);
    }
}
