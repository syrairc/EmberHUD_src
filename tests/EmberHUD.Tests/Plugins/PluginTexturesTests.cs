using Hexa.NET.ImGui;
using EmberHUD.Host.Icons;
using EmberHUD.Host.Plugins;

namespace EmberHUD.Tests;

public sealed class PluginTexturesTests : IDisposable
{
    readonly LogCapture _log = new();
    readonly List<Action> _queued = [];
    readonly Dictionary<string, int> _uploaded = [];
    readonly List<string> _released = [];
    TextureUpload? _upload;

    public void Dispose() => _log.Dispose();

    PluginTextures Host() => new(_queued.Add, Upload, Resolve, Release);

    bool Upload(string key, byte[] rgba, int width, int height, out string failure)
    {
        if (_upload is not null) return _upload(key, rgba, width, height, out failure);
        _uploaded[key] = width;
        failure = "";
        return true;
    }

    Sprite? Resolve(string key) => _uploaded.TryGetValue(key, out var width) ? new Sprite(new ImTextureRef { TexID = new ImTextureID((ulong)width) }, Vector2.Zero, Vector2.One) : null;

    void Release(string key)
    {
        _released.Add(key);
        _uploaded.Remove(key);
    }

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
        Assert.Empty(_uploaded);
        Assert.Null(textures.Resolve("Minimap", "region"));

        RunQueued();
        Assert.Equal(["plugin:Minimap/region"], _uploaded.Keys);
        Assert.Equal(new Sprite(new ImTextureRef { TexID = new ImTextureID(2ul) }, Vector2.Zero, Vector2.One), textures.Resolve("Minimap", "region"));
    }

    [Fact]
    public void TwoPluginsUsingTheSameKeyNeverShareATexture()
    {
        var textures = Host();
        textures.Upload("Minimap", "region", new byte[16], 2, 2);
        textures.Upload("Radar", "region", new byte[36], 3, 3);
        RunQueued();
        Assert.Equal(new ImTextureID(2ul), textures.Resolve("Minimap", "region")!.Value.Texture.TexID);
        Assert.Equal(new ImTextureID(3ul), textures.Resolve("Radar", "region")!.Value.Texture.TexID);
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
        Assert.Empty(_released);
        RunQueued();
        Assert.Equal(["plugin:Minimap/other", "plugin:Minimap/region"], _released.Order());
        Assert.Equal(["plugin:Radar/region"], _uploaded.Keys);
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
        Assert.Equal(["plugin:Minimap/region"], _released);
    }

    [Fact]
    public void AFailedUploadWarnsOnceUntilItWorksAndNeverThrows()
    {
        var textures = Host();
        _upload = (string _, byte[] _, int width, int _, out string failure) =>
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

        _upload = null;
        textures.Upload("Minimap", "region", new byte[4], 1, 1);
        RunQueued();
        _upload = (string _, byte[] _, int _, int _, out string failure) =>
        {
            failure = "still failing";
            return false;
        };
        textures.Upload("Minimap", "region", new byte[4], 1, 1);
        RunQueued();
        Assert.Equal(3, _log.Messages().Count);
    }
}
