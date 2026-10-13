using Hexa.NET.ImGui;
using EmberHUD.Testing;

namespace EmberHUD.Api.Tests;

public sealed class TexturesTests : IDisposable
{
    static readonly Sprite Seven = new(new ImTextureRef { TexID = new ImTextureID(7ul) }, Vector2.Zero, Vector2.One);

    readonly List<string> _calls = [];

    public TexturesTests()
    {
        Textures.Uploader = (plugin, key, rgba, width, height) => _calls.Add($"upload {plugin} {key} {rgba.Length} {width}x{height}");
        Textures.Resolver = (plugin, key) => key == "ready" ? Seven : null;
        Textures.Releaser = (plugin, key) => _calls.Add($"release {plugin} {key}");
    }

    public void Dispose()
    {
        Textures.Uploader = null;
        Textures.Resolver = null;
        Textures.Releaser = null;
    }

    [Fact]
    public void CallsCarryTheRunningPluginsName()
    {
        Log.CurrentPlugin.Value = "Minimap";
        try
        {
            Textures.Upload("region", new byte[16], 2, 2);
            Textures.Release("region");
        }
        finally
        {
            Log.CurrentPlugin.Value = null;
        }
        Assert.Equal(["upload Minimap region 16 2x2", "release Minimap region"], _calls);
    }

    [Fact]
    public void TryGetHandsBackTheHostsSprite()
    {
        Assert.True(Textures.TryGet("ready", out var sprite));
        Assert.Equal(Seven, sprite);
        Assert.False(Textures.TryGet("missing", out _));
    }

    [Fact]
    public void WithoutAHostNothingHappens()
    {
        Dispose();
        Textures.Upload("region", new byte[4], 1, 1);
        Textures.Release("region");
        Assert.False(Textures.TryGet("region", out _));
    }

    [Fact]
    public void TestGameFakesTheHostWithoutThePluginName()
    {
        var released = new List<string>();
        TestGame.UseTextures((key, rgba, width, height) => _calls.Add($"{key} {width}x{height}"), key => key == "region" ? Seven : null, released.Add);
        Textures.Upload("region", new byte[4], 1, 1);
        Textures.Release("region");
        Assert.True(Textures.TryGet("region", out _));
        Assert.Equal(["region 1x1"], _calls);
        Assert.Equal(["region"], released);
        TestGame.UseTextures(null, null);
        Assert.False(Textures.TryGet("region", out _));
    }
}
