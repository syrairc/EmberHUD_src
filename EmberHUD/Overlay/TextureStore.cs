using System.Numerics;
using Hexa.NET.ImGui;
using Vortice.Direct3D11;
using Vortice.DXGI;

namespace EmberHUD.Host.Overlay;

internal sealed class TextureStore
{
    static readonly SourceLog Log = EmberHUD.Log.For("Overlay");

    readonly Dictionary<string, (byte[] Pixels, int Width, int Height)> _pixels = [];
    readonly Dictionary<string, (ID3D11Texture2D Texture, ID3D11ShaderResourceView View)> _textures = [];
    ID3D11Device _device = null!;
    ID3D11DeviceContext _context = null!;

    public void Attach(ID3D11Device device, ID3D11DeviceContext context)
    {
        Detach();
        _device = device;
        _context = context;
        foreach (var (key, data) in _pixels)
        {
            try
            {
                _textures[key] = Create(data.Pixels, data.Width, data.Height);
            }
            catch (Exception ex)
            {
                Log.Warn($"texture {key} not rebuilt - {ex.Message}");
            }
        }
    }

    public void Detach()
    {
        foreach (var key in _textures.Keys.ToList()) DisposeTexture(key);
    }

    public unsafe Sprite? Resolve(string key)
    {
        if (!_textures.TryGetValue(key, out var entry)) return null;
        return new Sprite(new ImTextureRef(null, new ImTextureID(entry.View.NativePointer)), Vector2.Zero, Vector2.One);
    }

    public void Release(string key)
    {
        DisposeTexture(key);
        _pixels.Remove(key);
    }

    internal ID3D11Texture2D? TextureOf(string key) => _textures.TryGetValue(key, out var entry) ? entry.Texture : null;

    public bool TryUpload(string key, byte[] pixels, int width, int height, out string failure)
    {
        failure = "";
        if (width <= 0 || height <= 0)
        {
            failure = $"texture size {width}x{height} is not positive";
            return false;
        }
        var expected = (long)width * height * 4;
        if (pixels.Length < expected)
        {
            failure = $"RGBA data is {pixels.Length} bytes, {width}x{height} needs {expected}";
            return false;
        }

        try
        {
            if (_pixels.TryGetValue(key, out var old) && old.Width == width && old.Height == height && _textures.TryGetValue(key, out var current))
                Write(current.Texture, pixels, width);
            else
            {
                var created = Create(pixels, width, height);
                DisposeTexture(key);
                _textures[key] = created;
            }
            _pixels[key] = (pixels, width, height);
            return true;
        }
        catch (Exception ex)
        {
            failure = $"upload failed - {ex.Message}";
            return false;
        }
    }

    unsafe void Write(ID3D11Texture2D texture, byte[] pixels, int width)
    {
        fixed (byte* source = pixels) _context.UpdateSubresource(texture, 0, null, (nint)source, (uint)(width * 4), 0);
    }

    unsafe (ID3D11Texture2D, ID3D11ShaderResourceView) Create(byte[] pixels, int width, int height)
    {
        fixed (byte* source = pixels)
        {
            var texture = _device.CreateTexture2D(
                new Texture2DDescription
                {
                    Width = (uint)width,
                    Height = (uint)height,
                    MipLevels = 1,
                    ArraySize = 1,
                    Format = Format.R8G8B8A8_UNorm,
                    SampleDescription = new SampleDescription(1, 0),
                    Usage = ResourceUsage.Default,
                    BindFlags = BindFlags.ShaderResource,
                },
                [new SubresourceData((nint)source, (uint)(width * 4))]);
            return (texture, _device.CreateShaderResourceView(texture));
        }
    }

    void DisposeTexture(string key)
    {
        if (!_textures.Remove(key, out var old)) return;
        old.View.Dispose();
        old.Texture.Dispose();
    }
}
