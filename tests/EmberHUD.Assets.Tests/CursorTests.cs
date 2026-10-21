using System.Text;

namespace EmberHUD.Assets.Tests;

public class CursorTests
{
    [Fact]
    public void FStringReadsNarrowAsUtf8WideAsUtf16AndEmptyAsEmpty()
    {
        var narrow = Encoding.UTF8.GetBytes("首领\0");
        byte[] data = [.. BitConverter.GetBytes(narrow.Length), .. narrow, .. BitConverter.GetBytes(-3), .. Encoding.Unicode.GetBytes("蛋a\0"), .. BitConverter.GetBytes(0)];
        var cursor = new Cursor(data);
        Assert.Equal("首领", cursor.FString());
        Assert.Equal("蛋a", cursor.FString());
        Assert.Equal("", cursor.FString());
        Assert.True(cursor.AtEnd);
    }

    [Fact]
    public void ReadsLittleEndianNumbersFromAnOffset()
    {
        var cursor = new Cursor([9, 9, 1, 0, 0, 0, 2, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0x80, 0x3F], 2);
        Assert.Equal(1, cursor.I32());
        Assert.Equal(2L, cursor.I64());
        Assert.Equal(1f, cursor.F32());
        Assert.True(cursor.AtEnd);
    }

    [Fact]
    public void AtCanJumpAndTakingPastTheEndThrows()
    {
        var cursor = new Cursor([1, 2, 3, 4]) { At = 3 };
        Assert.Equal(4, cursor.U8());
        Assert.ThrowsAny<ArgumentException>(() => cursor.Take(1));
    }
}
