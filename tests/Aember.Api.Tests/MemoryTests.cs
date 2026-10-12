using Aember.Testing;

namespace Aember.Api.Tests;

public sealed class MemoryTests : IDisposable
{
    public void Dispose() => TestMemory.Clear();

    [Fact]
    public void DetachedReadsFailAndGlobalsAreEmpty()
    {
        TestMemory.Clear();

        Assert.False(Memory.Attached);
        Assert.False(Memory.TryRead(0x10000, out int _));
        Assert.Equal(0, Memory.Read(0x10000, new byte[4]));
        Assert.Empty(Memory.Globals);
        Assert.Equal(0, Memory.ModuleBase);
    }

    [Fact]
    public void WrittenValuesReadBackTyped()
    {
        var at = TestMemory.Alloc(16);
        TestMemory.Write(at + 4, 0x12345678);
        TestMemory.Write(at + 8, 1.5f);

        Assert.True(Memory.Attached);
        Assert.True(Memory.TryRead(at + 4, out int whole));
        Assert.Equal(0x12345678, whole);
        Assert.True(Memory.TryRead(at + 8, out float half));
        Assert.Equal(1.5f, half);
    }

    [Fact]
    public void AReadPastTheEndFailsWhole()
    {
        var at = TestMemory.Alloc(8);

        Assert.False(Memory.TryRead(at + 4, out long _));
        Assert.Equal(0, Memory.Read(at + 4, new byte[8]));
        Assert.Equal(4, Memory.Read(at + 4, new byte[4]));
    }

    [Fact]
    public void TheNullAddressNeverReads()
    {
        TestMemory.Alloc(8);

        Assert.False(Memory.TryRead(0, out int _));
    }

    [Fact]
    public void GlobalsAreTheNamesTheHostPublished()
    {
        TestMemory.Global("FightMgr", 0x1234);

        Assert.Equal((nint)0x1234, Memory.Globals["FightMgr"]);
    }

    [Fact]
    public void EverySourceChangeBumpsTheGeneration()
    {
        var before = Memory.Generation;
        TestMemory.Alloc(4);

        Assert.NotEqual(before, Memory.Generation);
    }
}
