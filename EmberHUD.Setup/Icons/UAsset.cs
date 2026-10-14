using System.Runtime.InteropServices;
using BCnEncoder.Decoder;
using BCnEncoder.Shared;
using EmberHUD.Setup.Pak;
using SkiaSharp;

namespace EmberHUD.Setup.Icons;

internal sealed record MapIconRow(int Id, string Key, string Label, string Texture, int Priority);

internal static class UAsset
{
    public static string[] Names(byte[] uasset)
    {
        var cursor = new Cursor(uasset, 20);
        cursor.Take(cursor.I32() * 20);
        cursor.Take(4);
        cursor.FString();
        cursor.Take(4);
        var count = cursor.I32();
        cursor.At = cursor.I32();
        var names = new string[count];
        for (var i = 0; i < count; i++)
        {
            names[i] = cursor.FString();
            cursor.Take(4);
        }
        return names;
    }

    public static (byte[] Rgba, int Width, int Height, string Format) Texture(byte[] uexp, byte[]? ubulk)
    {
        var at = uexp.AsSpan().IndexOf("PF_"u8);
        if (at < 4) throw new InvalidDataException("no pixel format");
        var cursor = new Cursor(uexp, at - 4);
        var format = cursor.FString();
        cursor.Take(4);
        var mips = cursor.I32();
        for (var i = 0; i < mips; i++)
        {
            cursor.Take(4);
            var flags = cursor.U32();
            long disk, offset;
            if ((flags & 0x2000) != 0)
            {
                cursor.Take(8);
                disk = cursor.I64();
                offset = cursor.I64();
            }
            else
            {
                cursor.Take(4);
                disk = cursor.I32();
                offset = cursor.I64();
            }
            byte[]? data = null;
            if ((flags & 0x100) != 0) data = ubulk is null ? null : ubulk[(int)offset..(int)(offset + disk)];
            else if ((flags & 0x21) == 0) data = cursor.Take((int)disk).ToArray();
            var width = cursor.I32();
            var height = cursor.I32();
            cursor.Take(4);
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
        var cursor = new Cursor(uexp, at + 49);
        return (cursor.F32(), cursor.F32());
    }

    public static List<MapIconRow> MapIconConfig(byte[] data)
    {
        List<MapIconRow> rows = [];
        var cursor = new Cursor(data, 8);
        while (!cursor.AtEnd)
        {
            var id = cursor.I32();
            cursor.Take(4);
            var key = cursor.FString();
            var label = cursor.FString();
            var texture = cursor.FString();
            rows.Add(new MapIconRow(id, key, label, texture, cursor.I32()));
            cursor.At += 1;
        }
        return rows;
    }

    public static void WritePng(string path, byte[] rgba, int width, int height)
    {
        using var image = SKImage.FromPixelCopy(new SKImageInfo(width, height, SKColorType.Rgba8888, SKAlphaType.Unpremul), rgba);
        using var png = image.Encode(SKEncodedImageFormat.Png, 100);
        File.WriteAllBytes(path, png.ToArray());
    }
}
