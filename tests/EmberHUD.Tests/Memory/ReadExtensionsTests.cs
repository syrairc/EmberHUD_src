using EmberHUD.Host.Memory;

namespace EmberHUD.Tests.Memory;

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
    public void NegativeCountFailsAndZeroCountSucceeds()
    {
        var memory = new FakeMemory();
        memory.Map(0x1000, 16);
        Assert.False(memory.TryReadArray(0x1000, -1, out int[] _));
        Assert.True(memory.TryReadArray(0x1000, 0, out int[] empty));
        Assert.Empty(empty);
    }
}
