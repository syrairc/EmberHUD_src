using EmberHUD.Host;
using EmberHUD.Host.Memory;

namespace EmberHUD.Tests;

public class ReadExtensionsTests
{
    [Fact]
    public void ReadsValuesAndArraysThatWereWritten()
    {
        var memory = new FakeMemory();
        memory.Map(0x1000, 64);
        memory.Write(0x1000, 1234);
        memory.Write(0x1008, 2.5);
        memory.WriteArray(0x1010, new[] { 7, 8, 9 });
        Assert.True(memory.TryRead(0x1000, out int i));
        Assert.Equal(1234, i);
        Assert.True(memory.TryRead(0x1008, out double d));
        Assert.Equal(2.5, d);
        Assert.True(memory.TryReadArray(0x1010, 3, out int[] values));
        Assert.Equal([7, 8, 9], values);
    }

    [Fact]
    public void ReadOutsideAMappedRegionFails()
    {
        var memory = new FakeMemory();
        memory.Map(0x1000, 16);
        Assert.False(memory.TryRead(0x0FFC, out int _));
        Assert.False(memory.TryRead(0x100D, out int _));
        Assert.False(memory.TryReadArray(0x1000, 5, out int[] _));
    }

    [Fact]
    public void NullPointerIsAFailureButZeroIntegerIsNot()
    {
        var memory = new FakeMemory();
        memory.Map(0x1000, 16);
        memory.Write(0x1000, 0L);
        Assert.False(memory.TryReadPtr(0x1000, out _));
        Assert.True(memory.TryRead(0x1000, out long zero));
        Assert.Equal(0L, zero);
    }

    [Fact]
    public void ASpanReadFillsTheSpanAndAnUnmappedReadFails()
    {
        var memory = new FakeMemory();
        memory.Map(0x1000, 0x40);
        memory.WriteArray(0x1000, new[] { 1.5, -2.5, 3.5 });
        Span<double> into = stackalloc double[3];

        Assert.True(memory.TryRead(0x1000, into));
        Assert.Equal([1.5, -2.5, 3.5], into.ToArray());
        Assert.False(memory.TryRead(0x1000 + 0x38, into));
    }

    [Fact]
    public void NegativeCountFailsAndZeroCountSucceeds()
    {
        var memory = new FakeMemory();
        memory.Map(0x1000, 16);
        Assert.False(memory.TryReadArray(0x1000, -1, out int[] _));
        Assert.True(memory.TryReadArray(0x1000, 0, out int[] empty));
        Assert.Empty(empty);
    }

    private static FakeMemory GObjects(params (uint Index, nint Object, int Serial)[] items)
    {
        var memory = new FakeMemory();
        var chunks = unchecked((nint)0x10_0000_0000L);
        memory.Map(FakeWorld.Module + Offsets.GObjectsGlobalRva, 8);
        memory.Write(FakeWorld.Module + Offsets.GObjectsGlobalRva, chunks);
        memory.Map(chunks, (Offsets.MaxGObjectChunks + 1) * 8);
        foreach (var (index, obj, serial) in items)
        {
            var chunk = unchecked((nint)0x20_0000_0000L) + (int)(index / Offsets.GObjectsPerChunk) * 0x100_0000;
            var item = chunk + (int)(index % Offsets.GObjectsPerChunk) * Offsets.GObjectsItemSize;
            memory.Write(chunks + (int)(index / Offsets.GObjectsPerChunk) * 8, chunk);
            memory.Map(item, Offsets.GObjectsItemSize);
            memory.Write(item + Offsets.GObjectsItemObject, obj);
            memory.Write(item + Offsets.GObjectsItemSerial, serial);
        }

        return memory;
    }

    [Fact]
    public void AGObjectAndItsSerialAreReadFromAnyChunk()
    {
        var later = (uint)(3 * Offsets.GObjectsPerChunk + 7);
        var memory = GObjects((5, 0x3000, 11), (later, 0x4000, 12));

        Assert.True(memory.TryReadGObject(FakeWorld.Module, 5, out var obj, out var serial));
        Assert.Equal((0x3000, 11), (obj, serial));
        Assert.True(memory.TryReadGObject(FakeWorld.Module, later, out obj, out serial));
        Assert.Equal((0x4000, 12), (obj, serial));
    }

    [Fact]
    public void AGObjectPastTheChunkCapIsRejectedEvenWhenItsChunkReads()
    {
        var lastChunk = (uint)((Offsets.MaxGObjectChunks - 1) * Offsets.GObjectsPerChunk);
        var pastCap = (uint)(Offsets.MaxGObjectChunks * Offsets.GObjectsPerChunk);
        var memory = GObjects((lastChunk, 0x3000, 1), (pastCap, 0x4000, 1));

        Assert.True(memory.TryReadGObject(FakeWorld.Module, lastChunk, out _, out _));
        Assert.False(memory.TryReadGObject(FakeWorld.Module, pastCap, out _, out _));
        Assert.False(memory.TryReadGObject(FakeWorld.Module, uint.MaxValue, out _, out _));
    }

    [Fact]
    public void ANullGObjectOrAMissingChunkFails()
    {
        var memory = GObjects((5, 0, 1));

        Assert.False(memory.TryReadGObject(FakeWorld.Module, 5, out _, out _));
        Assert.False(memory.TryReadGObject(FakeWorld.Module, 6, out _, out _));
        Assert.False(memory.TryReadGObject(FakeWorld.Module, (uint)Offsets.GObjectsPerChunk, out _, out _));
    }
}
