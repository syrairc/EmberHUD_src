using EmberHUD.Host;

namespace EmberHUD.Tests;

public class PeImageTests
{
    private static readonly byte[] Code = [0x90, 0x48, 0x8B, 0x05, 0x11, 0x22, 0x33, 0x44, 0x90, 0x48, 0x8B, 0x05, 0x55, 0x66, 0x77, 0x88];

    private static byte[] TwoSections() => FakePe.Build((".text", 0x1000, Code, true), (".rdata", 0x3000, Code, false));

    [Fact]
    public void ParsesSectionsAndMapsOffsets()
    {
        Assert.True(PeImage.TryParse(TwoSections(), out var image));
        Assert.Equal(0x4000, image.SizeOfImage);
        Assert.Equal(0x1005, image.RvaOf(0x405));
        Assert.Equal(0x405, image.FileOffsetOf(0x1005));
        Assert.Equal(0x3002, image.RvaOf(0x602));
        Assert.Equal(0x602, image.FileOffsetOf(0x3002));
        Assert.Equal(-1, image.RvaOf(0x10));
        Assert.Equal(-1, image.FileOffsetOf(0x2000));
        Assert.Equal(-1, image.FileOffsetOf(0x1000 + Code.Length));
        Assert.Equal(-1, image.RvaOf(-1));
    }

    [Fact]
    public void FindMatchesWildcardsInExecutableSectionsOnly()
    {
        Assert.True(PeImage.TryParse(TwoSections(), out var image));
        var hits = image.Find("48 8B 05 ? ? ? ?", 10);
        Assert.Equal([0x401, 0x409], hits);
    }

    [Fact]
    public void FindStopsAtLimit()
    {
        Assert.True(PeImage.TryParse(TwoSections(), out var image));
        Assert.Equal([0x401], image.Find("48 8B 05 ? ? ? ?", 1));
    }

    [Fact]
    public void FindHandlesLeadingWildcardsAndMisses()
    {
        Assert.True(PeImage.TryParse(TwoSections(), out var image));
        Assert.Equal([0x401], image.Find("? 8B 05 ? ? ? ? 90", 10));
        Assert.Empty(image.Find("DE AD BE EF", 10));
        Assert.Empty(image.Find("? ?", 10));
    }

    [Fact]
    public void TryParseRejectsGarbage()
    {
        foreach (var file in Garbage())
        {
            Assert.False(PeImage.TryParse(file, out _));
        }
    }

    private static IEnumerable<byte[]> Garbage()
    {
        yield return [];
        yield return new byte[10];
        yield return [0x4D, 0x5A];

        var manySections = TwoSections();
        BitConverter.GetBytes((ushort)99).CopyTo(manySections, 0x40 + 6);
        yield return manySections[..0x200];

        var badLfanew = TwoSections();
        BitConverter.GetBytes(int.MaxValue).CopyTo(badLfanew, 0x3C);
        yield return badLfanew;

        var negativeLfanew = TwoSections();
        BitConverter.GetBytes(-4).CopyTo(negativeLfanew, 0x3C);
        yield return negativeLfanew;

        var badMagic = TwoSections();
        BitConverter.GetBytes((ushort)0x10B).CopyTo(badMagic, 0x40 + 24);
        yield return badMagic;

        var rawPastEnd = TwoSections();
        yield return rawPastEnd[..0x500];

        var overflow = TwoSections();
        BitConverter.GetBytes(int.MaxValue).CopyTo(overflow, 0x40 + 24 + 240 + 20);
        yield return overflow;
    }
}
