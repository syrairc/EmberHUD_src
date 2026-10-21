using EmberHUD.Host;
using EmberHUD.Launcher;

namespace EmberHUD.Assets.Tests;

public class PakFileTests
{
    static readonly byte[] Random10k = [.. new Random(7).GetItems<byte>([.. Enumerable.Range(0, 256).Select(i => (byte)i)], 10_000)];

    static PakFile Open(TempDir temp, byte[] pak, byte[]? key = null) => PakFile.Open(temp.File("test.pak", pak), key ?? PakBuilder.Key);

    [Fact]
    public void OffsetsCarryThePakKey() => Assert.Equal(32, Convert.FromHexString(Offsets.PakAesKey).Length);

    [Fact]
    public void ListsEveryEntryWithItsFullPath()
    {
        using var temp = new TempDir();
        using var pak = Open(temp, PakBuilder.Build(new PakSource("UE_game/Content/A/one.bin", [1, 2, 3]), new PakSource("UE_game/Content/B/two.bin", [4], "Zlib")));
        Assert.Equal(["UE_game/Content/A/one.bin", "UE_game/Content/B/two.bin"], pak.Entries.Select(entry => entry.Path));
        Assert.Equal("Zlib", pak.Methods[0]);
        Assert.Equal("Oodle", pak.Methods[1]);
    }

    [Theory]
    [InlineData(null, false, 65536)]
    [InlineData(null, true, 65536)]
    [InlineData("Zlib", false, 65536)]
    [InlineData("Zlib", true, 65536)]
    [InlineData("Zlib", false, 4096)]
    [InlineData("Zlib", true, 4096)]
    public void ReadsStoredAndZlibEntriesPlainOrEncrypted(string? method, bool encrypted, int blockSize)
    {
        using var temp = new TempDir();
        byte[] odd = [.. Random10k.Take(37)];
        using var pak = Open(temp, PakBuilder.Build(new PakSource("UE_game/x.bin", Random10k, method, encrypted, blockSize), new PakSource("UE_game/odd.bin", odd, method, encrypted, blockSize)));
        Assert.Equal(Random10k, pak.Read(pak.Entries[0]));
        Assert.Equal(odd, pak.Read(pak.Entries[1]));
    }

    [Theory]
    [InlineData(0x5A6F12E1u, 12)]
    [InlineData(0x6B2A56B8u, 11)]
    public void WrongMagicOrVersionSaysTheFormatChanged(uint magic, int version)
    {
        using var temp = new TempDir();
        var error = Assert.Throws<InvalidDataException>(() => Open(temp, PakBuilder.Build(PakBuilder.Key, magic, version, new PakSource("UE_game/x.bin", [1]))));
        Assert.Contains("pak format changed", error.Message);
        Assert.Contains("test.pak", error.Message);
    }

    [Fact]
    public void WrongKeyIsAClearError()
    {
        using var temp = new TempDir();
        var error = Assert.Throws<InvalidDataException>(() => Open(temp, PakBuilder.Build(new PakSource("UE_game/x.bin", [1])), [.. new byte[32]]));
        Assert.Contains("pak key changed", error.Message);
    }

    [Fact]
    public void TooSmallForAFooterIsAClearError()
    {
        using var temp = new TempDir();
        Assert.Throws<InvalidDataException>(() => Open(temp, new byte[10]));
    }

    [Fact]
    public void UnknownCompressionIsAClearError()
    {
        using var temp = new TempDir();
        using var pak = Open(temp, PakBuilder.Build(new PakSource("UE_game/x.bin", [1, 2], "Oodle", Compressed: [[9, 9]])));
        Assert.Throws<InvalidDataException>(() => pak.Read(pak.Entries[0]));
    }

    [Fact]
    public void EveryRealPakOpensWhenTheGameIsSet()
    {
        var game = RealGame.Install();
        var key = Convert.FromHexString(Offsets.PakAesKey);
        foreach (var path in game.Paks())
        {
            using var pak = PakFile.Open(path, key);
            Assert.NotEmpty(pak.Entries);
        }
    }
}
