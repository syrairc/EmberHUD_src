using System.Runtime.InteropServices;

namespace EmberHUD.Setup.Pak;

internal static partial class Oodle
{
    [LibraryImport("ooz", EntryPoint = "ooz_decompress")]
    private static unsafe partial int Native(byte* source, nint sourceLength, byte* target, nint targetLength);

    public static unsafe void Decompress(ReadOnlySpan<byte> source, Span<byte> target)
    {
        if (source.Length < 2) throw new InvalidDataException("oodle block is empty");
        var buffer = new byte[target.Length + 64];
        int written;
        fixed (byte* from = source)
        fixed (byte* to = buffer)
            written = Native(from, source.Length, to, target.Length);
        if (written != target.Length) throw new InvalidDataException(written < 0 ? "oodle block did not decode" : $"oodle block decoded to {written} bytes, expected {target.Length}");
        buffer.AsSpan(0, target.Length).CopyTo(target);
    }
}
