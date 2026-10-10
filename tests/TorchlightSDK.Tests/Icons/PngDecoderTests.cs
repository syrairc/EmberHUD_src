using TorchlightSDK.Host.Icons;

namespace TorchlightSDK.Tests;

public sealed class PngDecoderTests
{
    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    [InlineData(4)]
    public void EveryFilterTypeRoundTrips(byte filter)
    {
        var pixels = PngFixture.Gradient(5, 4);
        var png = PngFixture.Rgba(5, 4, pixels, [filter]);

        Assert.True(PngDecoder.TryDecode(png, out var image, out var failure), failure);
        Assert.Equal(5, image.Width);
        Assert.Equal(4, image.Height);
        Assert.Equal(pixels, image.Rgba);
        Assert.Equal("", failure);
    }

    [Fact]
    public void FiltersMayChangeFromRowToRow()
    {
        var pixels = PngFixture.Gradient(7, 6);
        var png = PngFixture.Rgba(7, 6, pixels, [0, 1, 2, 3, 4, 2]);

        Assert.True(PngDecoder.TryDecode(png, out var image, out var failure), failure);
        Assert.Equal(pixels, image.Rgba);
    }

    [Fact]
    public void ARealisticIconSizeDecodes()
    {
        var pixels = PngFixture.Gradient(80, 80);

        Assert.True(PngDecoder.TryDecode(PngFixture.Rgba(80, 80, pixels, [4, 1, 2]), out var image, out var failure), failure);
        Assert.Equal(pixels, image.Rgba);
    }

    [Fact]
    public void ANonPngIsRefused()
    {
        Assert.False(PngDecoder.TryDecode([1, 2, 3], out _, out var failure));
        Assert.Contains("PNG", failure);
        Assert.False(PngDecoder.TryDecode(new byte[40], out _, out _));
    }

    [Theory]
    [InlineData(16, 6, 0)]
    [InlineData(8, 3, 0)]
    [InlineData(8, 2, 0)]
    [InlineData(8, 6, 1)]
    public void OnlyEightBitRgbaNonInterlacedIsAccepted(byte bitDepth, byte colorType, byte interlace)
    {
        var png = PngFixture.Rgba(4, 4, PngFixture.Gradient(4, 4), [0], bitDepth, colorType, interlace);

        Assert.False(PngDecoder.TryDecode(png, out var image, out var failure));
        Assert.NotEqual("", failure);
        Assert.Equal(default(DecodedImage), image);
    }

    [Fact]
    public void ATruncatedFileIsRefusedWithoutThrowing()
    {
        var png = PngFixture.Rgba(8, 8, PngFixture.Gradient(8, 8), [0]);

        for (var length = 0; length < png.Length - 16; length += 7)
            Assert.False(PngDecoder.TryDecode(png.AsSpan(0, length), out _, out _), $"length {length}");
    }

    [Fact]
    public void CorruptCompressedDataIsRefusedWithoutThrowing()
    {
        var png = PngFixture.Rgba(8, 8, PngFixture.Gradient(8, 8), [0]);
        var idat = png.AsSpan().IndexOf("IDAT"u8) + 4;
        png[idat] = 0xFF;
        png[idat + 1] = 0xFF;

        Assert.False(PngDecoder.TryDecode(png, out _, out var failure));
        Assert.NotEqual("", failure);
    }

    [Fact]
    public void AbsurdOrZeroDimensionsAreRefused()
    {
        foreach (var (width, height) in new[] { (0, 4), (4, 0), (100000, 4), (4, 100000) })
        {
            var png = PngFixture.Rgba(1, 1, [1, 2, 3, 4], [0]);
            var header = png.AsSpan().IndexOf("IHDR"u8) + 4;
            System.Buffers.Binary.BinaryPrimitives.WriteUInt32BigEndian(png.AsSpan(header), (uint)width);
            System.Buffers.Binary.BinaryPrimitives.WriteUInt32BigEndian(png.AsSpan(header + 4), (uint)height);

            Assert.False(PngDecoder.TryDecode(png, out _, out var failure), $"{width}x{height}");
            Assert.Contains("size", failure);
        }
    }

    [Fact]
    public void MutatedPngsNeverThrow()
    {
        var random = new Random(1234);
        var bases = new[]
        {
            PngFixture.Rgba(8, 8, PngFixture.Gradient(8, 8), [0, 1, 2, 3, 4]),
            PngFixture.Rgba(40, 40, PngFixture.Gradient(40, 40), [4, 1, 2]),
        };

        foreach (var basePng in bases)
        {
            var dataStart = basePng.AsSpan().IndexOf("IDAT"u8) + 4;
            var dataEnd = basePng.Length - 16;
            for (var iteration = 0; iteration < 30000; iteration++)
            {
                var png = (byte[])basePng.Clone();
                for (var mutations = random.Next(1, 4); mutations > 0; mutations--)
                    png[dataStart + random.Next(dataEnd - dataStart)] = (byte)random.Next(256);

                if (!PngDecoder.TryDecode(png, out _, out var failure)) Assert.NotEqual("", failure);
            }
        }
    }
}
