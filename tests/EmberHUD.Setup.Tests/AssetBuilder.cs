using System.Buffers.Binary;
using System.Text;
using EmberHUD.Setup.Icons;
using SkiaSharp;

namespace EmberHUD.Setup.Tests;

internal static class AssetBuilder
{
    public static byte[] Package(params string[] names)
    {
        var writer = new BinaryWriter(new MemoryStream());
        writer.Write(new byte[20]);
        writer.Write(1);
        writer.Write(new byte[20]);
        writer.Write(0);
        FString(writer, "None");
        writer.Write(0);
        writer.Write(names.Length);
        writer.Write((int)writer.BaseStream.Position + 4);
        foreach (var name in names)
        {
            FString(writer, name);
            writer.Write(0);
        }
        return ((MemoryStream)writer.BaseStream).ToArray();
    }

    public static byte[] Texture(string format, int width, int height, byte[] pixels, byte[]? emptyMipFirst = null)
    {
        var writer = new BinaryWriter(new MemoryStream());
        writer.Write(new byte[33]);
        writer.Write(width);
        writer.Write(height);
        writer.Write(1);
        FString(writer, format);
        writer.Write(0);
        writer.Write(emptyMipFirst is null ? 1 : 2);
        if (emptyMipFirst is not null) Mip(writer, 0x20, [], width * 2, height * 2);
        Mip(writer, 0x48, pixels, width, height);
        writer.Write(new byte[8]);
        return ((MemoryStream)writer.BaseStream).ToArray();
    }

    public static byte[] UbulkTexture(string format, int width, int height, int offset, int size)
    {
        var writer = new BinaryWriter(new MemoryStream());
        writer.Write(new byte[33]);
        writer.Write(width);
        writer.Write(height);
        writer.Write(1);
        FString(writer, format);
        writer.Write(0);
        writer.Write(1);
        writer.Write(1);
        writer.Write(0x100u);
        writer.Write(size);
        writer.Write(size);
        writer.Write((long)offset);
        writer.Write(width);
        writer.Write(height);
        writer.Write(1);
        return ((MemoryStream)writer.BaseStream).ToArray();
    }

    static void Mip(BinaryWriter writer, uint flags, byte[] pixels, int width, int height)
    {
        writer.Write(1);
        writer.Write(flags);
        writer.Write(pixels.Length);
        writer.Write(pixels.Length);
        writer.Write(0L);
        writer.Write(pixels);
        writer.Write(width);
        writer.Write(height);
        writer.Write(1);
    }

    public static byte[] Sprite(string[] names, (float X, float Y) uv, (float X, float Y) dimension)
    {
        var writer = new BinaryWriter(new MemoryStream());
        writer.Write(new byte[7]);
        foreach (var (property, value) in new[] { ("BakedSourceUV", uv), ("BakedSourceDimension", dimension) })
        {
            writer.Write(Array.IndexOf(names, property));
            writer.Write(0);
            writer.Write(Array.IndexOf(names, "StructProperty"));
            writer.Write(0);
            writer.Write(new byte[33]);
            writer.Write(value.X);
            writer.Write(value.Y);
            writer.Write(new byte[5]);
        }
        return ((MemoryStream)writer.BaseStream).ToArray();
    }

    public static byte[] MapIconConfig(params MapIconRow[] rows)
    {
        var writer = new BinaryWriter(new MemoryStream());
        writer.Write(847);
        writer.Write(rows.Length);
        foreach (var row in rows)
        {
            writer.Write(row.Id);
            writer.Write(row.Id);
            FString(writer, row.Key, Encoding.UTF8);
            FString(writer, row.Label, Encoding.UTF8);
            FString(writer, row.Texture, Encoding.UTF8);
            writer.Write(row.Priority);
            writer.Write((byte)1);
        }
        return ((MemoryStream)writer.BaseStream).ToArray();
    }

    public static byte[] Bc7Solid(byte r, byte g, byte b, byte a)
    {
        var block = UInt128.One << 6;
        var at = 7;
        foreach (var channel in new[] { r, r, g, g, b, b, a, a })
        {
            block |= (UInt128)(uint)(channel >> 1) << at;
            at += 7;
        }
        var bytes = new byte[16];
        BinaryPrimitives.WriteUInt128LittleEndian(bytes, block);
        return bytes;
    }

    public static (byte[] Rgba, int Width, int Height) Pixels(string png)
    {
        using var codec = SKCodec.Create(png);
        var info = new SKImageInfo(codec.Info.Width, codec.Info.Height, SKColorType.Rgba8888, SKAlphaType.Unpremul);
        var rgba = new byte[info.BytesSize];
        unsafe
        {
            fixed (byte* pixels = rgba) Assert.Equal(SKCodecResult.Success, codec.GetPixels(info, (nint)pixels));
        }
        return (rgba, info.Width, info.Height);
    }

    internal static void FString(BinaryWriter writer, string text, Encoding? encoding = null)
    {
        var bytes = (encoding ?? Encoding.ASCII).GetBytes(text);
        writer.Write(bytes.Length + 1);
        writer.Write(bytes);
        writer.Write((byte)0);
    }
}
