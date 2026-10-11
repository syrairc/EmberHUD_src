using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using BCnEncoder.Decoder;
using BCnEncoder.Shared;
using SkiaSharp;

namespace Aember.Setup.Icons;

[Obfuscation(Exclude = true, ApplyToMembers = true)]
internal sealed record MapIconRow(int Id, string Key, string Label, string Texture, int Priority);

internal static class UAsset
{
    public static string[] Names(byte[] uasset)
    {
        var at = 20;
        at += 4 + BitConverter.ToInt32(uasset, at) * 20;
        at += 4;
        (_, at) = FString(uasset, at);
        var count = BitConverter.ToInt32(uasset, at + 4);
        at = BitConverter.ToInt32(uasset, at + 8);
        var names = new string[count];
        for (var i = 0; i < count; i++)
        {
            (names[i], at) = FString(uasset, at);
            at += 4;
        }
        return names;
    }

    public static (byte[] Rgba, int Width, int Height, string Format) Texture(byte[] uexp, byte[]? ubulk)
    {
        var at = uexp.AsSpan().IndexOf("PF_"u8);
        if (at < 4) throw new InvalidDataException("no pixel format");
        var (format, p) = FString(uexp, at - 4);
        var mips = BitConverter.ToInt32(uexp, p + 4);
        p += 8;
        for (var i = 0; i < mips; i++)
        {
            var flags = BitConverter.ToUInt32(uexp, p + 4);
            p += 8;
            long disk, offset;
            if ((flags & 0x2000) != 0)
            {
                disk = BitConverter.ToInt64(uexp, p + 8);
                offset = BitConverter.ToInt64(uexp, p + 16);
                p += 24;
            }
            else
            {
                disk = BitConverter.ToInt32(uexp, p + 4);
                offset = BitConverter.ToInt64(uexp, p + 8);
                p += 16;
            }
            byte[]? data = null;
            if ((flags & 0x100) != 0) data = ubulk is null ? null : ubulk[(int)offset..(int)(offset + disk)];
            else if ((flags & 0x21) == 0)
            {
                data = uexp[p..(p + (int)disk)];
                p += (int)disk;
            }
            var width = BitConverter.ToInt32(uexp, p);
            var height = BitConverter.ToInt32(uexp, p + 4);
            p += 12;
            if (data is { Length: > 0 }) return (Decode(format, data, width, height), width, height, format);
        }
        throw new InvalidDataException($"no mip with data ({format})");
    }

    static byte[] Decode(string format, byte[] data, int width, int height)
    {
        switch (format)
        {
            case "PF_BC7":
                var blockWidth = (width + 3) / 4 * 4;
                var blockHeight = (height + 3) / 4 * 4;
                var decoded = new BcDecoder().DecodeRaw(data, blockWidth, blockHeight, CompressionFormat.Bc7);
                return Crop(MemoryMarshal.AsBytes(decoded.AsSpan()).ToArray(), blockWidth, blockHeight, 0, 0, width, height);
            case "PF_B8G8R8A8":
                var rgba = data[..(width * height * 4)];
                for (var i = 0; i < rgba.Length; i += 4) (rgba[i], rgba[i + 2]) = (rgba[i + 2], rgba[i]);
                return rgba;
            default:
                throw new InvalidDataException($"format {format}");
        }
    }

    public static byte[] Crop(byte[] rgba, int width, int height, int x, int y, int w, int h)
    {
        var cut = new byte[w * h * 4];
        for (var row = 0; row < h; row++)
        {
            var sourceY = y + row;
            if (sourceY < 0 || sourceY >= height) continue;
            var from = Math.Max(x, 0);
            var to = Math.Min(x + w, width);
            if (to <= from) continue;
            rgba.AsSpan((sourceY * width + from) * 4, (to - from) * 4).CopyTo(cut.AsSpan((row * w + from - x) * 4));
        }
        return cut;
    }

    public static (float X, float Y) Vector2D(byte[] uexp, string[] names, string property)
    {
        var name = Array.IndexOf(names, property);
        var structName = Array.IndexOf(names, "StructProperty");
        if (name < 0 || structName < 0) throw new InvalidDataException($"no {property}");
        Span<byte> tag = stackalloc byte[16];
        BitConverter.TryWriteBytes(tag, name);
        BitConverter.TryWriteBytes(tag[8..], structName);
        var at = uexp.AsSpan().IndexOf(tag);
        if (at < 0 || at + 57 > uexp.Length) throw new InvalidDataException($"no {property}");
        return (BitConverter.ToSingle(uexp, at + 49), BitConverter.ToSingle(uexp, at + 53));
    }

    public static List<MapIconRow> MapIconConfig(byte[] data)
    {
        List<MapIconRow> rows = [];
        var at = 8;
        while (at < data.Length)
        {
            var id = BitConverter.ToInt32(data, at);
            var (key, next) = FString(data, at + 8);
            var (label, afterLabel) = FString(data, next);
            var (texture, afterTexture) = FString(data, afterLabel);
            rows.Add(new MapIconRow(id, key, label, texture, BitConverter.ToInt32(data, afterTexture)));
            at = afterTexture + 5;
        }
        return rows;
    }

    public static void WritePng(string path, byte[] rgba, int width, int height)
    {
        using var image = SKImage.FromPixelCopy(new SKImageInfo(width, height, SKColorType.Rgba8888, SKAlphaType.Unpremul), rgba);
        using var png = image.Encode(SKEncodedImageFormat.Png, 100);
        File.WriteAllBytes(path, png.ToArray());
    }

    static (string Text, int Next) FString(byte[] data, int at)
    {
        var length = BitConverter.ToInt32(data, at);
        at += 4;
        if (length < 0) return (Encoding.Unicode.GetString(data, at, -length * 2).TrimEnd('\0'), at - length * 2);
        return (Encoding.UTF8.GetString(data, at, length).TrimEnd('\0'), at + length);
    }
}
