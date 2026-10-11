using Hexa.NET.ImGui;
using Aember.Host.Plugins;

namespace Aember.Tests;

public sealed class PluginTexturesTests : IDisposable
{
    readonly LogCapture _log = new();
    readonly List<Action> _queued = [];
    readonly Dictionary<string, int> _uploaded = [];
    readonly List<string> _released = [];
    Func<string, byte[], int, int, bool>? _upload;
    string _uploadError = "";

    public void Dispose() => _log.Dispose();

    PluginTextures Host() => new(_queued.Add, (key, rgba, width, height) => _upload?.Invoke(key, rgba, width, height) ?? Store(key, width), Lookup, Release, () => _uploadError);

    bool Store(string key, int width)
    {
        _uploaded[key] = width;
        return true;
    }

    bool Lookup(string key, out ImTextureRef texture)
    {
        texture = new ImTextureRef { TexID = new ImTextureID((ulong)(_uploaded.TryGetValue(key, out var width) ? width : 0)) };
        return _uploaded.ContainsKey(key);
    }

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
        _uploadError = "texture is 20000 texels wide, the limit is 16384";
        _upload = (_, _, width, _) => width > 0 ? false : throw new ArgumentException("texture size 0x1 is not positive");
        textures.Upload("Minimap", "region", new byte[4], 1, 1);
        textures.Upload("Minimap", "region", new byte[4], 1, 1);
        textures.Upload("Minimap", "bad", new byte[4], 0, 1);
        RunQueued();
        Assert.Equal(["Minimap texture region - upload failed - texture is 20000 texels wide, the limit is 16384", "Minimap texture bad - texture size 0x1 is not positive"], _log.Messages());
        Assert.Null(textures.Resolve("Minimap", "region"));

        _upload = null;
        textures.Upload("Minimap", "region", new byte[4], 1, 1);
        RunQueued();
        _upload = (_, _, _, _) => false;
        textures.Upload("Minimap", "region", new byte[4], 1, 1);
        RunQueued();
        Assert.Equal(3, _log.Messages().Count);
    }
}
