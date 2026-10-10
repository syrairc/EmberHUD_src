using System.Security.Cryptography;
using TorchlightSDK.Host;

namespace OffsetGen.Tests;

public class OffsetGenTests
{
    [Fact]
    public void ParsesHexDecimalCommentsAndBlankLines()
    {
        var entries = LayoutParser.Parse("# note\n\nA = 0x10\nB = 12\n  C=0X1F  \n");
        Assert.Equal([("A", 16), ("B", 12), ("C", 31)], entries);
    }

    [Theory]
    [InlineData("A = 1\nA = 2")]
    [InlineData("A 1")]
    [InlineData("A = banana")]
    [InlineData("1A = 3")]
    [InlineData("A = 0x1FFFFFFFF")]
    public void RejectsBadLines(string text) => Assert.Throws<FormatException>(() => LayoutParser.Parse(text));

    [Fact]
    public void EmitsOneAccessorPerNameWithItsIndex()
    {
        var source = OffsetEmitter.Emit(["Alpha", "Beta"], [1, 2, 3]);
        Assert.Contains("internal static int Alpha => T[0];", source);
        Assert.Contains("internal static int Beta => T[1];", source);
        Assert.Contains("0x01, 0x02, 0x03", source);
        Assert.Contains("namespace TorchlightSDK.Host;", source);
    }

    [Fact]
    public void PackSealOpenUnpackRoundTrips()
    {
        var key = RandomNumberGenerator.GetBytes(32);
        var box = OffsetTable.Seal(OffsetTable.Pack([5, -1, 0x69331B8]), key);
        Assert.Equal([5, -1, 0x69331B8], OffsetTable.Unpack(OffsetTable.Open(box, key)));
        box[^1] ^= 1;
        Assert.ThrowsAny<CryptographicException>(() => OffsetTable.Open(box, key));
    }
}
