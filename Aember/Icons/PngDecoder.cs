using System.Buffers.Binary;
using System.IO.Compression;

namespace Aember.Host.Icons;

internal readonly record struct DecodedImage(int Width, int Height, byte[] Rgba);

internal static class PngDecoder
{
    const int MaxSide = 4096;

    static ReadOnlySpan<byte> Signature => [137, 80, 78, 71, 13, 10, 26, 10];

    public static bool TryDecode(ReadOnlySpan<byte> png, out DecodedImage image, out string failure)
    {
        image = default;
        failure = "";
        if (png.Length < Signature.Length || !png[..Signature.Length].SequenceEqual(Signature)) return Refuse("not a PNG", out failure);

        var width = 0;
        var height = 0;
        var header = false;
        using var compressed = new MemoryStream();
        var offset = Signature.Length;
        while (offset + 8 <= png.Length)
        {
            var length = BinaryPrimitives.ReadUInt32BigEndian(png[offset..]);
            var type = png.Slice(offset + 4, 4);
            var start = offset + 8;
            if (start + (long)length > png.Length) return Refuse("truncated chunk", out failure);
            var data = png.Slice(start, (int)length);

            if (type.SequenceEqual("IHDR"u8))
            {
                if (data.Length != 13) return Refuse("bad header", out failure);
                var wide = BinaryPrimitives.ReadUInt32BigEndian(data);
                var tall = BinaryPrimitives.ReadUInt32BigEndian(data[4..]);
                if (wide is 0 or > MaxSide || tall is 0 or > MaxSide) return Refuse($"size {wide}x{tall} is outside 1..{MaxSide}", out failure);
                if (data[8] != 8 || data[9] != 6) return Refuse($"bit depth {data[8]} colour type {data[9]}, only 8-bit RGBA is supported", out failure);
                if (data[10] != 0 || data[11] != 0 || data[12] != 0) return Refuse("interlaced or non-standard PNG", out failure);
                width = (int)wide;
                height = (int)tall;
                header = true;
            }
            else if (type.SequenceEqual("IDAT"u8)) compressed.Write(data);
            else if (type.SequenceEqual("IEND"u8)) break;

            offset = start + (int)length + 4;
        }

        if (!header || compressed.Length == 0) return Refuse("no image data", out failure);

        var stride = width * 4;
        var raw = new byte[(stride + 1) * height];
        compressed.Position = 0;
        try
        {
            using var zlib = new ZLibStream(compressed, CompressionMode.Decompress);
            zlib.ReadExactly(raw);
        }
        catch (Exception ex) when (ex is InvalidDataException or IOException)
        {
            return Refuse($"bad compressed data - {ex.Message}", out failure);
        }

        var rgba = new byte[stride * height];
        var none = new byte[stride];
        for (var y = 0; y < height; y++)
        {
            var filter = raw[y * (stride + 1)];
            if (filter > 4) return Refuse($"unknown filter type {filter}", out failure);
            var source = raw.AsSpan(y * (stride + 1) + 1, stride);
            var row = rgba.AsSpan(y * stride, stride);
            var above = y > 0 ? rgba.AsSpan((y - 1) * stride, stride) : none;
            for (var x = 0; x < stride; x++)
            {
                var left = x >= 4 ? row[x - 4] : 0;
                var up = above[x];
                var upLeft = x >= 4 ? above[x - 4] : 0;
                var predicted = filter switch { 0 => 0, 1 => left, 2 => up, 3 => (left + up) >> 1, _ => Paeth(left, up, upLeft) };
                row[x] = (byte)(source[x] + predicted);
            }
        }

        image = new DecodedImage(width, height, rgba);
        return true;
    }

    static bool Refuse(string reason, out string failure)
    {
        failure = reason;
        return false;
    }

    static int Paeth(int left, int up, int upLeft)
    {
        var estimate = left + up - upLeft;
        var toLeft = Math.Abs(estimate - left);
        var toUp = Math.Abs(estimate - up);
        var toUpLeft = Math.Abs(estimate - upLeft);
        return toLeft <= toUp && toLeft <= toUpLeft ? left : toUp <= toUpLeft ? up : upLeft;
    }
}
