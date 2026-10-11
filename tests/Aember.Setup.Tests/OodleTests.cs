using Aember.Host;
using Aember.Launcher;
using Aember.Setup.Pak;

namespace Aember.Setup.Tests;

public class OodleTests
{
    static readonly byte[] Text = File.ReadAllBytes(Path.Combine(AppContext.BaseDirectory, "Fixtures", "kraken.txt"));
    static readonly byte[] Stream = File.ReadAllBytes(Path.Combine(AppContext.BaseDirectory, "Fixtures", "kraken.bin"));

    [Fact]
    public void DecodesTheKrakenFixture()
    {
        Assert.True(Stream.Length < Text.Length);
        var output = new byte[Text.Length];
        Oodle.Decompress(Stream, output);
        Assert.Equal(Text, output);
    }

    [Fact]
    public void AWrongSizeIsAnError() => Assert.Throws<InvalidDataException>(() => Oodle.Decompress(Stream, new byte[Text.Length - 1]));

    [Fact]
    public void ATruncatedStreamIsAnError() => Assert.Throws<InvalidDataException>(() => Oodle.Decompress(Stream.AsSpan(0, Stream.Length / 2), new byte[Text.Length]));

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    public void AnEmptyOrOneByteBlockIsAnError(int length) => Assert.Throws<InvalidDataException>(() => Oodle.Decompress(new byte[length], new byte[Text.Length]));

    [Fact]
    public void PakReadsAnOodleEntry()
    {
        using var temp = new TempDir();
        using var pak = PakFile.Open(temp.File("o.pak", PakBuilder.Build("../../../", new PakSource("UE_game/o.txt", Text, "Oodle", BlockSize: Text.Length, Compressed: [Stream]))), PakBuilder.Key);
        Assert.Equal(Text, pak.Read(pak.Entries[0]));
    }

    [Fact]
    public void PakReadsAnEncryptedOodleEntry()
    {
        using var temp = new TempDir();
        using var pak = PakFile.Open(temp.File("o.pak", PakBuilder.Build("../../../", new PakSource("UE_game/o.txt", Text, "Oodle", Encrypted: true, BlockSize: Text.Length, Compressed: [Stream]))), PakBuilder.Key);
        Assert.Equal(Text, pak.Read(pak.Entries[0]));
    }

    [Fact]
    public void EveryEntryOfTheSmallestRealPakReadsWhenTheGameIsSet()
    {
        if (Environment.GetEnvironmentVariable("TLI_GAME_DIR") is not { } folder || GameInstall.At(folder) is not { } game) return;
        using var pak = PakFile.Open(game.Paks().First(path => path.EndsWith("pakchunk999-WindowsNoEditor.pak", StringComparison.OrdinalIgnoreCase)), Convert.FromHexString(Offsets.PakAesKey));
        Assert.All(pak.Entries, entry => Assert.Equal(entry.RawSize, pak.Read(entry).Length));
    }
}
