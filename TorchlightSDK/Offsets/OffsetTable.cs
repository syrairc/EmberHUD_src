using System.Security.Cryptography;

namespace TorchlightSDK.Host;

internal static class OffsetTable
{
    private const int NonceSize = 12;
    private const int TagSize = 16;

    public static byte[] Pack(IReadOnlyList<int> ints)
    {
        var bytes = new byte[sizeof(int) * (ints.Count + 1)];
        BitConverter.TryWriteBytes(bytes, ints.Count);
        for (var i = 0; i < ints.Count; i++) BitConverter.TryWriteBytes(bytes.AsSpan(sizeof(int) * (i + 1)), ints[i]);
        return bytes;
    }

    public static int[] Unpack(byte[] packed)
    {
        var ints = new int[BitConverter.ToInt32(packed)];
        for (var i = 0; i < ints.Length; i++) ints[i] = BitConverter.ToInt32(packed, sizeof(int) * (i + 1));
        return ints;
    }

    public static byte[] Seal(ReadOnlySpan<byte> plain, ReadOnlySpan<byte> key)
    {
        var box = new byte[NonceSize + TagSize + plain.Length];
        RandomNumberGenerator.Fill(box.AsSpan(0, NonceSize));
        using var aes = new AesGcm(key, TagSize);
        aes.Encrypt(box.AsSpan(0, NonceSize), plain, box.AsSpan(NonceSize + TagSize), box.AsSpan(NonceSize, TagSize));
        return box;
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
