using EmberHUD.Host.Reading;

namespace EmberHUD.Tests.Reading;

public class ProtobufTests
{
    [Fact]
    public void ParsesVarintAndLengthDelimited()
    {
        Assert.True(Protobuf.TryParse([0x0A, 0x03, 0x10, 0x90, 0x4E], out var outer));
        var field = Assert.Single(outer);
        Assert.Equal(1, field.Number);
        Assert.Equal<byte[]>([0x10, 0x90, 0x4E], field.Bytes);

        Assert.True(Protobuf.TryParse(field.Bytes, out var inner));
        var varint = Assert.Single(inner);
        Assert.Equal((2, 10000L), (varint.Number, varint.Varint));
        Assert.Empty(varint.Bytes);
    }

    [Fact]
    public void RejectsTruncatedInput()
    {
        Assert.False(Protobuf.TryParse([0x0A, 0x05, 0x10], out _));
        Assert.False(Protobuf.TryParse([0x10, 0x90], out _));
    }

    [Fact]
    public void RejectsFixed64()
    {
        Assert.False(Protobuf.TryParse([0x09, 0, 0, 0, 0, 0, 0, 0, 0], out _));
    }

    [Fact]
    public void RejectsAnOverlongVarint()
    {
        Assert.False(Protobuf.TryParse([0x10, 0x80, 0x80, 0x80, 0x80, 0x80, 0x80, 0x80, 0x80, 0x80, 0x80, 0x01], out _));
    }

    [Fact]
    public void EmptyInputIsNoFields()
    {
        Assert.True(Protobuf.TryParse([], out var fields));
        Assert.Empty(fields);
    }
}
