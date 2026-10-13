using System.Buffers.Binary;
using System.IO.Compression;
using System.Text;

namespace EmberHUD.Tests;

internal static class PngFixture
{
    public static byte[] Gradient(int width, int height) => [.. Enumerable.Range(0, width * height * 4).Select(i => (byte)(i * 37 + i / 7))];

    public static byte[] Rgba(int width, int height, byte[] rgba, byte[] rowFilters, byte bitDepth = 8, byte colorType = 6, byte interlace = 0)
    {
        var stride = width * 4;
        var raw = new MemoryStream();
        for (var y = 0; y < height; y++)
        {
            var filter = rowFilters[y % rowFilters.Length];
            raw.WriteByte(filter);
            for (var x = 0; x < stride; x++)
            {
                var left = x >= 4 ? rgba[y * stride + x - 4] : 0;
                var up = y > 0 ? rgba[(y - 1) * stride + x] : 0;
                var upLeft = x >= 4 && y > 0 ? rgba[(y - 1) * stride + x - 4] : 0;
                var predicted = filter switch { 0 => 0, 1 => left, 2 => up, 3 => (left + up) >> 1, _ => Paeth(left, up, upLeft) };
                raw.WriteByte((byte)(rgba[y * stride + x] - predicted));
            }
        }

        var deflated = new MemoryStream();
        using (var zlib = new ZLibStream(deflated, CompressionLevel.Fastest, leaveOpen: true)) zlib.Write(raw.ToArray());

        var header = new byte[13];
        BinaryPrimitives.WriteUInt32BigEndian(header, (uint)width);
        BinaryPrimitives.WriteUInt32BigEndian(header.AsSpan(4), (uint)height);
        header[8] = bitDepth;
        header[9] = colorType;
        header[12] = interlace;

        var file = new MemoryStream();
        file.Write([137, 80, 78, 71, 13, 10, 26, 10]);
        Chunk(file, "IHDR", header);
        Chunk(file, "IDAT", deflated.ToArray());
        Chunk(file, "IEND", []);
        return file.ToArray();
    }

    static void Chunk(Stream file, string type, byte[] data)
    {
        var length = new byte[4];
        BinaryPrimitives.WriteUInt32BigEndian(length, (uint)data.Length);
        file.Write(length);
        file.Write(Encoding.ASCII.GetBytes(type));
        file.Write(data);
        file.Write(new byte[4]);
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
