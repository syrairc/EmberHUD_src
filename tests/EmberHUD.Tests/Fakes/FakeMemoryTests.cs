using EmberHUD.Host.Memory;

namespace EmberHUD.Tests;

public class FakeMemoryTests
{
    [Fact]
    public void RegionsMappedOutOfOrderAreAllReadable()
    {
        var memory = new FakeMemory();
        nint[] bases = [0x9000, 0x1000, 0x5000, 0x3000];
        foreach (var at in bases)
        {
            memory.Map(at, 16);
            memory.Write(at, at);
        }

        foreach (var at in bases)
        {
            Assert.True(memory.TryRead(at, out nint value));
            Assert.Equal(at, value);
        }
    }

    [Fact]
    public void AReadCrossingTheEndOfARegionFailsEvenWhenTheNextRegionIsAdjacent()
    {
        var memory = new FakeMemory();
        memory.Map(0x1000, 16);
        memory.Map(0x1010, 16);

        Assert.True(memory.TryRead(0x1008, out long _));
        Assert.False(memory.TryRead(0x100C, out long _));
        Assert.False(memory.TryRead(0x0FFC, out int _));
    }

    [Fact]
    public void MappingTheSameRegionTwiceIsANoOpButAnyOtherOverlapThrows()
    {
        var memory = new FakeMemory();
        memory.Map(0x1000, 16);
        memory.Write(0x1000, 7);

        memory.Map(0x1000, 16);

        Assert.True(memory.TryRead(0x1000, out int kept));
        Assert.Equal(7, kept);
        Assert.Throws<InvalidOperationException>(() => memory.Map(0x1008, 16));
        Assert.Throws<InvalidOperationException>(() => memory.Map(0x0FF8, 16));
    }

    [Fact]
    public void ConcurrentReadsAreAllRecorded()
    {
        var memory = new FakeMemory();
        memory.Map(0x1000, 16);

        Parallel.For(0, 4, _ =>
        {
            for (var i = 0; i < 5000; i++) memory.TryRead(0x1000, out int _);
        });

        Assert.Equal(20000, memory.Reads.Count);
    }
}
