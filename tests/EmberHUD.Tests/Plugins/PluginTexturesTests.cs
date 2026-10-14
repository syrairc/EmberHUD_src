using Hexa.NET.ImGui;
using EmberHUD.Host.Icons;
using EmberHUD.Host.Plugins;

namespace EmberHUD.Tests;

public sealed class PluginTexturesTests : IDisposable
{
    readonly LogCapture _log = new();
    readonly List<Action> _queued = [];
    readonly FakeTextures _textures = new();

    public void Dispose() => _log.Dispose();

    PluginTextures Host() => new(_queued.Add, _textures.Upload, _textures.Resolve, _textures.Release);

    void RunQueued()
    {
        foreach (var action in _queued.ToList()) action();
        _queued.Clear();
    }

    [Fact]
    public void AnUploadWaitsForTheRenderThreadAndLandsUnderThePluginsName()
    {
        var textures = Host();
        textures.Upload("Minimap", "region", new byte[16], 2, 2);
        Assert.Empty(_textures.Uploaded);
        Assert.Null(textures.Resolve("Minimap", "region"));

        RunQueued();
        Assert.Equal(["plugin:Minimap/region"], _textures.Uploaded.Keys);
        Assert.Equal(new Sprite(new ImTextureRef { TexID = new ImTextureID(_textures.Handle) }, Vector2.Zero, Vector2.One), textures.Resolve("Minimap", "region"));
    }

    [Fact]
    public void TwoPluginsUsingTheSameKeyNeverShareATexture()
    {
        var textures = Host();
        textures.Upload("Minimap", "region", new byte[16], 2, 2);
        textures.Upload("Radar", "region", new byte[36], 3, 3);
        RunQueued();
        Assert.Equal(2, _textures.Uploaded["plugin:Minimap/region"].Width);
        Assert.Equal(3, _textures.Uploaded["plugin:Radar/region"].Width);
        Assert.NotNull(textures.Resolve("Minimap", "region"));
        Assert.NotNull(textures.Resolve("Radar", "region"));
        Assert.Null(textures.Resolve("Sample", "region"));
    }

    [Fact]
    public void ReleaseAllDropsOnlyThatPluginsTexturesOnTheRenderThread()
    {
        var textures = Host();
        textures.Upload("Minimap", "region", new byte[16], 2, 2);
        textures.Upload("Minimap", "other", new byte[4], 1, 1);
        textures.Upload("Radar", "region", new byte[4], 1, 1);
        RunQueued();

        textures.ReleaseAll("Minimap");
        Assert.Empty(_textures.Released);
        RunQueued();
        Assert.Equal(["plugin:Minimap/other", "plugin:Minimap/region"], _textures.Released.Order());
        Assert.Equal(["plugin:Radar/region"], _textures.Uploaded.Keys);
    }

    [Fact]
    public void AReleasedKeyIsNotReleasedAgainOnUnload()
    {
        var textures = Host();
        textures.Upload("Minimap", "region", new byte[4], 1, 1);
        textures.Release("Minimap", "region");
        RunQueued();
        textures.ReleaseAll("Minimap");
        RunQueued();
        Assert.Equal(["plugin:Minimap/region"], _textures.Released);
    }

    [Fact]
    public void AFailedUploadWarnsOnceUntilItWorksAndNeverThrows()
    {
        var textures = Host();
        _textures.UploadOverride = (string _, byte[] _, int width, int _, out string failure) =>
        {
            failure = width > 0 ? "upload failed - texture is 20000 texels wide, the limit is 16384" : "texture size 0x1 is not positive";
            return false;
        };
        textures.Upload("Minimap", "region", new byte[4], 1, 1);
        textures.Upload("Minimap", "region", new byte[4], 1, 1);
        textures.Upload("Minimap", "bad", new byte[4], 0, 1);
        RunQueued();
        Assert.Equal(["Minimap texture region - upload failed - texture is 20000 texels wide, the limit is 16384", "Minimap texture bad - texture size 0x1 is not positive"], _log.Messages());
        Assert.Null(textures.Resolve("Minimap", "region"));

        _textures.UploadOverride = null;
        textures.Upload("Minimap", "region", new byte[4], 1, 1);
        RunQueued();
        _textures.UploadOverride = (string _, byte[] _, int _, int _, out string failure) =>
        {
            failure = "still failing";
            return false;
        };
        textures.Upload("Minimap", "region", new byte[4], 1, 1);
        RunQueued();
        Assert.Equal(3, _log.Messages().Count);
    }
}
