using System.Security.Cryptography;

namespace EmberHUD.Host;

internal static class OffsetTable
{
    internal const int NonceSize = 12;
    internal const int TagSize = 16;

    public static int[] Unpack(byte[] packed)
    {
        var ints = new int[BitConverter.ToInt32(packed)];
        for (var i = 0; i < ints.Length; i++) ints[i] = BitConverter.ToInt32(packed, sizeof(int) * (i + 1));
        return ints;
    }

    public static string[] UnpackStrings(byte[] packed)
    {
        var at = sizeof(int) * (BitConverter.ToInt32(packed) + 1);
        var strings = new string[BitConverter.ToInt32(packed, at)];
        at += sizeof(int);
        for (var i = 0; i < strings.Length; i++)
        {
            var length = BitConverter.ToInt32(packed, at);
            strings[i] = System.Text.Encoding.UTF8.GetString(packed, at + sizeof(int), length);
            at += sizeof(int) + length;
        }

        return strings;
    }

    public static byte[] Open(ReadOnlySpan<byte> box, ReadOnlySpan<byte> key)
    {
        if (box.Length < NonceSize + TagSize) throw new CryptographicException("table too short");
        var plain = new byte[box.Length - NonceSize - TagSize];
        using var aes = new AesGcm(key, TagSize);
        aes.Decrypt(box[..NonceSize], box[(NonceSize + TagSize)..], box.Slice(NonceSize, TagSize), plain);
        return plain;
    }
}
