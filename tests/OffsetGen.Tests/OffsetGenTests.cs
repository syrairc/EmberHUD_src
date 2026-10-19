using System.Security.Cryptography;
using EmberHUD.Host;

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
    [InlineData("A = 0x80000000")]
    [InlineData("A = 0xFFFFFFFF")]
    public void RejectsBadLines(string text) => Assert.Throws<FormatException>(() => LayoutParser.Parse(text));

    [Fact]
    public void EmitsOneAccessorPerNameWithItsIndex()
    {
        var source = OffsetEmitter.Emit(["Alpha", "Beta"], [1, 2, 3], []);
        Assert.Contains("internal static int Alpha => T[0];", source);
        Assert.Contains("internal static int Beta => T[1];", source);
        Assert.Contains("0x01, 0x02, 0x03", source);
        Assert.Contains("namespace EmberHUD.Host;", source);
    }

    [Fact]
    public void PackSealOpenUnpackRoundTrips()
    {
        var key = RandomNumberGenerator.GetBytes(32);
        var box = OffsetEmitter.Seal(OffsetEmitter.Pack([5, -1, 0x69331B8], []), key);
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
        var box = OffsetEmitter.Seal(OffsetEmitter.Pack([7, 8], ["MiniMap_C", "Rotate", "Default__"]), key);
        var plain = OffsetTable.Open(box, key);
        Assert.Equal([7, 8], OffsetTable.Unpack(plain));
        Assert.Equal(["MiniMap_C", "Rotate", "Default__"], OffsetTable.UnpackStrings(plain));
        Assert.Empty(OffsetTable.UnpackStrings(OffsetEmitter.Pack([1], [])));
    }

    const string TwoBuilds = """
        A = 1
        B = 0x10
        [build 100]
        XRva = 0x1000
        YRva = 0x2000
        [build 250]
        YRva = 0x2500
        XRva = 0x1500
        """;

    [Fact]
    public void BaseParsingStopsAtTheFirstBuildHeader()
    {
        Assert.Equal([("A", 1), ("B", 16)], LayoutParser.Parse(TwoBuilds));
        Assert.Empty(LayoutParser.ParseStrings(TwoBuilds));
    }

    [Fact]
    public void ParseBuildsOrdersEveryBlockByTheFirstBuildsKeys()
    {
        var table = LayoutParser.ParseBuilds(TwoBuilds, ["A", "B"]);
        Assert.Equal(["XRva", "YRva"], table.Names);
        Assert.Equal([100L, 250L], table.Sizes);
        Assert.Equal([0x1000, 0x2000, 0x1500, 0x2500], table.Values);
    }

    [Theory]
    [InlineData("A = 1")]
    [InlineData("A = 1\n[build 100]")]
    public void ABuildSectionIsRequiredAndMustHoldKeys(string text) => Assert.Throws<FormatException>(() => LayoutParser.ParseBuilds(text, ["A"]));

    [Theory]
    [InlineData("A = 1\n[build 100]\nXRva = 1\n[build 250]\nXRva = 2\nYRva = 3")]
    [InlineData("A = 1\n[build 100]\nXRva = 1\nYRva = 3\n[build 250]\nXRva = 2")]
    [InlineData("A = 1\n[build 100]\nXRva = 1\n[build 250]\nYRva = 2")]
    public void BuildsMustDefineTheSameKeys(string text) => Assert.Throws<FormatException>(() => LayoutParser.ParseBuilds(text, ["A"]));

    [Fact]
    public void AKeyInTheBaseCannotAlsoBeInABuild() =>
        Assert.Throws<FormatException>(() => LayoutParser.ParseBuilds("A = 1\n[build 100]\nA = 1\nXRva = 1", ["A"]));

    [Fact]
    public void AnyKeyCanDifferPerBuildNotOnlyRvas()
    {
        var table = LayoutParser.ParseBuilds("A = 1\n[build 100]\nXRva = 1\nFieldOffset = 8\n[build 250]\nFieldOffset = 16\nXRva = 2", ["A"]);
        Assert.Equal(["XRva", "FieldOffset"], table.Names);
        Assert.Equal([1, 8, 2, 16], table.Values);
    }

    [Theory]
    [InlineData("A = 1\n[build x]\nXRva = 1")]
    [InlineData("A = 1\n[build 0]\nXRva = 1")]
    [InlineData("A = 1\n[build 100\nXRva = 1")]
    [InlineData("A = 1\n[build 100]\nXRva = 1\n[build 100]\nXRva = 1")]
    [InlineData("A = 1\n[build 100]\nXRva = \"text\"")]
    public void RejectsBadBuildHeadersAndValues(string text) => Assert.Throws<FormatException>(() => LayoutParser.ParseBuilds(text, ["A"]));

    [Fact]
    public void EmitsPerBuildAccessorsIndexedByTheSelectedBuild()
    {
        var source = OffsetEmitter.Emit(["Alpha", "Beta"], [1], [], ["XRva", "YRva"], [100, 250]);
        Assert.Contains("internal static int Alpha => T[0];", source);
        Assert.Contains("internal static int XRva => T[2 + Build * 2];", source);
        Assert.Contains("internal static int YRva => T[3 + Build * 2];", source);
        Assert.Contains("internal static readonly long[] BuildSizes = [100, 250];", source);
    }

    private const string SigPattern = "48 8B C8 8B D6 E8 ? ? ? ? C7 43 04 00 00 00 00 48";

    [Fact]
    public void SigsSectionIsKeptOutOfTheLastBuild()
    {
        var text = $"A = 1\n[build 100]\nKeyRva = 1\nOtherRva = 2\n[sigs]\n# note\n\nKeyRva = {SigPattern} | 2 8\nOtherRva = @KeyRva + 0x28\n";
        var table = LayoutParser.ParseBuilds(text, ["A"]);
        Assert.Equal(["KeyRva", "OtherRva"], table.Names);
        Assert.Equal([$"S|KeyRva|{SigPattern}|2|8", "R|OtherRva|KeyRva|40"], LayoutParser.ParseSigs(text, table.Names));
    }

    [Fact]
    public void SigsAllowNegativeDeltasAndRepeatedKeys()
    {
        var text = $"[sigs]\nKeyRva = {SigPattern} | 2 8\nKeyRva = {SigPattern} | 0 4\nOtherRva = @KeyRva - 0xC\n";
        var lines = LayoutParser.ParseSigs(text, ["KeyRva", "OtherRva"]);
        Assert.Equal(3, lines.Count);
        Assert.Equal("R|OtherRva|KeyRva|-12", lines[2]);
    }

    [Theory]
    [InlineData("NopeRva = 48 8B C8 8B D6 E8 ? ? ? ? C7 43 04 00 00 00 00 48 | 2 8")]
    [InlineData("KeyRva = 48 8B C8 8B D6 E8 ? ? ? ? C7 43 04 00 00 00 00 ZZ | 2 8")]
    [InlineData("KeyRva = 48 8B C8 8B D6 E8 ? ? ? ? C7 43 04 00 00 00 00 48 | 2 19")]
    [InlineData("KeyRva = 48 8B C8 8B D6 E8 ? ? ? ? C7 43 04 00 00 00 00 48 | 4 7")]
    [InlineData("KeyRva = 48 8B C8 8B D6 E8 ? ? ? ? C7 43 04 | 2 8")]
    [InlineData("KeyRva = 48 8B C8 8B D6 E8 ? ? ? ? C7 43 04 00 00 00 00 48 | 2147483647 8")]
    [InlineData("KeyRva = @KeyRva + 0x28")]
    [InlineData("OtherRva = @")]
    [InlineData("OtherRva = @NopeRva + 4")]
    public void SigsRejectBadLines(string line) =>
        Assert.Throws<FormatException>(() => LayoutParser.ParseSigs($"[sigs]\n{line}\n", ["KeyRva", "OtherRva"]));

    [Fact]
    public void EmitWritesBuildKeysAndSigLines()
    {
        var source = OffsetEmitter.Emit(["Alpha", "Beta"], [1], ["Name"], ["A", "B"], [100], ["S|A|00|0|4"]);
        Assert.Contains("internal static readonly string[] BuildKeys = [\"A\", \"B\"];", source);
        Assert.Contains("internal const int PerBuildStart = 2;", source);
        Assert.Contains("internal static string[] SigLines => S[1..];", source);
    }
}
