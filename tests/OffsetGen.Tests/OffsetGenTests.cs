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

    [Fact]
    public void ParsesQuotedStringEntriesSeparatelyFromNumbers()
    {
        var text = """
            A = 5
            ClassName = "MiniMap_C"
            # x
            B = 0x2
            Param = "Rotate"
            """;
        Assert.Equal([("A", 5), ("B", 2)], LayoutParser.Parse(text));
        Assert.Equal([("ClassName", "MiniMap_C"), ("Param", "Rotate")], LayoutParser.ParseStrings(text));
    }

    [Theory]
    [InlineData("A = \"x\"\nA = \"y\"")]
    [InlineData("A = 1\nA = \"y\"")]
    [InlineData("A = \"unterminated")]
    [InlineData("A = \"\"")]
    [InlineData("1A = \"x\"")]
    public void RejectsBadStringLines(string text) => Assert.Throws<FormatException>(() => LayoutParser.ParseStrings(text));

    [Fact]
    public void EmitsStringAccessorsAfterTheNumbers()
    {
        var source = OffsetEmitter.Emit(["Alpha"], [1, 2, 3], ["Name", "Other"]);
        Assert.Contains("internal static int Alpha => T[0];", source);
        Assert.Contains("internal static string Name => S[0];", source);
        Assert.Contains("internal static string Other => S[1];", source);
    }

    [Fact]
    public void StringsRoundTripThroughPackSealOpen()
    {
        var key = RandomNumberGenerator.GetBytes(32);
        var box = OffsetTable.Seal(OffsetTable.Pack([7, 8], ["MiniMap_C", "Rotate", "Default__"]), key);
        var plain = OffsetTable.Open(box, key);
        Assert.Equal([7, 8], OffsetTable.Unpack(plain));
        Assert.Equal(["MiniMap_C", "Rotate", "Default__"], OffsetTable.UnpackStrings(plain));
        Assert.Empty(OffsetTable.UnpackStrings(OffsetTable.Pack([1])));
    }
}
