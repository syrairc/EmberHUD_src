using System.Runtime.InteropServices;
using Vortice.Direct3D;
using Vortice.Direct3D11;

namespace EmberHUD.Tests;

public sealed class TextureStoreTests : IDisposable
{
    const string Key = "plugin:Minimap/region";

    readonly ID3D11Device _device;
    readonly ID3D11DeviceContext _context;
    readonly TextureStore _store = new();

    public TextureStoreTests()
    {
        Assert.True(GraphicsDevice.TryCreate(DriverType.Warp, out var device, out var context, out var failure), failure);
        _device = device!;
        _context = context!;
        _store.Attach(_device, _context);
    }

    public void Dispose()
    {
        _store.Detach();
        _context.Dispose();
        _device.Dispose();
    }

    static byte[] Solid(int width, int height, byte red, byte green, byte blue)
    {
        var pixels = new byte[width * height * 4];
        for (var i = 0; i < pixels.Length; i += 4)
        {
            pixels[i] = red;
            pixels[i + 1] = green;
            pixels[i + 2] = blue;
            pixels[i + 3] = 255;
        }
        return pixels;
    }

    byte[] ReadBack(string key, ID3D11Device device, ID3D11DeviceContext context)
    {
        var texture = _store.TextureOf(key)!;
        var description = texture.Description;
        description.Usage = ResourceUsage.Staging;
        description.BindFlags = BindFlags.None;
        description.CPUAccessFlags = CpuAccessFlags.Read;
        using var staging = device.CreateTexture2D(description);
        context.CopyResource(staging, texture);
        context.Map(staging, 0, MapMode.Read, MapFlags.None, out var mapped);
        var width = (int)description.Width;
        var rows = (int)description.Height;
        var bytes = new byte[width * rows * 4];
        for (var row = 0; row < rows; row++) Marshal.Copy(mapped.DataPointer + row * (int)mapped.RowPitch, bytes, row * width * 4, width * 4);
        context.Unmap(staging, 0);
        return bytes;
    }

    [Fact]
    public void AnUploadGivesTheTextureTheUploadedPixels()
    {
        var red = Solid(3, 2, 255, 0, 0);

        Assert.True(_store.TryUpload(Key, red, 3, 2, out var failure), failure);

        Assert.Equal(red, ReadBack(Key, _device, _context));
    }

    [Fact]
    public void AReUploadOfTheSameSizeKeepsTheTextureAndChangesThePixels()
    {
        _store.TryUpload(Key, Solid(3, 2, 255, 0, 0), 3, 2, out _);
        var before = _store.Resolve(Key)!.Value.Texture.TexID;
        var blue = Solid(3, 2, 0, 0, 255);

        Assert.True(_store.TryUpload(Key, blue, 3, 2, out var failure), failure);

        Assert.Equal(before, _store.Resolve(Key)!.Value.Texture.TexID);
        Assert.Equal(blue, ReadBack(Key, _device, _context));
    }

    [Fact]
    public void AReUploadOfANewSizeReplacesTheTexture()
    {
        _store.TryUpload(Key, Solid(3, 2, 255, 0, 0), 3, 2, out _);
        var before = _store.Resolve(Key)!.Value.Texture.TexID;
        var green = Solid(5, 4, 0, 255, 0);

        Assert.True(_store.TryUpload(Key, green, 5, 4, out var failure), failure);

        Assert.NotEqual(before, _store.Resolve(Key)!.Value.Texture.TexID);
        Assert.Equal(green, ReadBack(Key, _device, _context));
    }

    [Fact]
    public void BadInputIsRejectedWithAMessageAndKeepsTheOldTexture()
    {
        var red = Solid(3, 2, 255, 0, 0);
        _store.TryUpload(Key, red, 3, 2, out _);

        Assert.False(_store.TryUpload(Key, new byte[4], 0, 1, out var empty));
        Assert.Equal("texture size 0x1 is not positive", empty);
        Assert.False(_store.TryUpload(Key, new byte[3], 3, 2, out var tooShort));
        Assert.Equal("RGBA data is 3 bytes, 3x2 needs 24", tooShort);
        Assert.False(_store.TryUpload("other", new byte[4], 1, -1, out _));

        Assert.Equal(red, ReadBack(Key, _device, _context));
        Assert.Null(_store.Resolve("other"));
    }

    [Fact]
    public void ARebuiltDeviceGetsTheLatestPixelsBack()
    {
        _store.TryUpload(Key, Solid(3, 2, 255, 0, 0), 3, 2, out _);
        var blue = Solid(3, 2, 0, 0, 255);
        _store.TryUpload(Key, blue, 3, 2, out _);
        Assert.True(GraphicsDevice.TryCreate(DriverType.Warp, out var device, out var context, out var failure), failure);
        try
        {
            _store.Detach();
            Assert.Null(_store.Resolve(Key));

            _store.Attach(device!, context!);

            Assert.NotNull(_store.Resolve(Key));
            Assert.Equal(blue, ReadBack(Key, device!, context!));
        }
        finally
        {
            _store.Detach();
            context!.Dispose();
            device!.Dispose();
        }
    }

    [Fact]
    public void AReleasedKeyStaysGoneAfterARebuild()
    {
        _store.TryUpload(Key, Solid(2, 2, 255, 0, 0), 2, 2, out _);

        _store.Release(Key);
        Assert.Null(_store.Resolve(Key));
        _store.Detach();
        _store.Attach(_device, _context);

        Assert.Null(_store.Resolve(Key));
    }

    [Fact]
    public void ReleaseDisposesTheGpuTexture()
    {
        _store.TryUpload(Key, Solid(2, 2, 1, 2, 3), 2, 2, out _);
        var texture = _store.TextureOf(Key)!;

        _store.Release(Key);

        Assert.Equal(nint.Zero, texture.NativePointer);
        Assert.Null(_store.TextureOf(Key));
        Assert.Null(_store.Resolve(Key));
    }

    [Fact]
    public void DetachDisposesTheGpuTextures()
    {
        _store.TryUpload(Key, Solid(2, 2, 1, 2, 3), 2, 2, out _);
        var texture = _store.TextureOf(Key)!;

        _store.Detach();

        Assert.Equal(nint.Zero, texture.NativePointer);
        Assert.Null(_store.TextureOf(Key));
    }

    [Fact]
    public void AttachingAgainDisposesTheTexturesTheOldDeviceHeld()
    {
        var pixels = Solid(2, 2, 9, 8, 7);
        _store.TryUpload(Key, pixels, 2, 2, out _);
        var before = _store.TextureOf(Key)!;

        _store.Attach(_device, _context);

        Assert.Equal(nint.Zero, before.NativePointer);
        Assert.NotNull(_store.TextureOf(Key));
        Assert.Equal(pixels, ReadBack(Key, _device, _context));
    }
}
