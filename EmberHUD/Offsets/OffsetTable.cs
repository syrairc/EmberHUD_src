using System.Security.Cryptography;

namespace EmberHUD.Host;

internal static class OffsetTable
{
    private const int NonceSize = 12;
    private const int TagSize = 16;

    public static byte[] Pack(IReadOnlyList<int> ints, IReadOnlyList<string>? strings = null)
    {
        using var stream = new MemoryStream();
        using var writer = new BinaryWriter(stream);
        writer.Write(ints.Count);
        foreach (var value in ints) writer.Write(value);
        writer.Write(strings?.Count ?? 0);
        foreach (var text in strings ?? [])
        {
            var bytes = System.Text.Encoding.UTF8.GetBytes(text);
            writer.Write(bytes.Length);
            writer.Write(bytes);
        }

        return stream.ToArray();
    }

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
