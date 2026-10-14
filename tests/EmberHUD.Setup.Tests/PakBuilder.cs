using System.IO.Compression;
using System.Security.Cryptography;
using System.Text;

namespace EmberHUD.Setup.Tests;

internal sealed record PakSource(string Path, byte[] Data, string? Method = null, bool Encrypted = false, int BlockSize = 65536, byte[][]? Compressed = null);

internal static class PakBuilder
{
    public static readonly byte[] Key = [.. Enumerable.Range(1, 32).Select(i => (byte)i)];
    static readonly string[] Methods = ["Zlib", "Oodle"];

    public static byte[] Build(params PakSource[] files) => Build(Key, 0x6B2A56B8, 12, files);

    public static byte[] Build(byte[] key, uint magic, int version, params PakSource[] files)
    {
        using var aes = Aes.Create();
        aes.Key = key;
        byte[] Encrypt(byte[] data) => aes.EncryptEcb(Pad(data), PaddingMode.None);

        var pak = new MemoryStream();
        var encoded = new BinaryWriter(new MemoryStream());
        var listed = new List<(string Folder, string Name, int Location)>();
        foreach (var file in files)
        {
            var compressed = file.Method is null ? null : file.Compressed ?? [.. file.Data.Chunk(file.BlockSize).Select(Zlib)];
            var blockCount = compressed?.Length ?? 0;
            var offset = pak.Position;
            pak.Write(new byte[8 + 1 + 8 + 8 + 4 + 20 + (compressed is null ? 0 : 4 + 16 * blockCount) + 1 + 4]);
            foreach (var block in compressed ?? [file.Data]) pak.Write(file.Encrypted ? Encrypt(block) : block);

            var method = file.Method is null ? 0u : (uint)Array.IndexOf(Methods, file.Method) + 1;
            var location = (int)encoded.BaseStream.Position;
            var sizeBits = file.BlockSize % 2048 == 0 && file.BlockSize >> 11 < 0x3F ? (uint)(file.BlockSize >> 11) : 0x3Fu;
            encoded.Write(sizeBits | (uint)blockCount << 6 | (file.Encrypted ? 1u << 22 : 0) | method << 23 | 7u << 29);
            if (sizeBits == 0x3F) encoded.Write((uint)file.BlockSize);
            encoded.Write((uint)offset);
            encoded.Write((uint)file.Data.Length);
            if (compressed is not null) encoded.Write((uint)compressed.Sum(block => block.Length));
            if (blockCount > 0 && !(blockCount == 1 && !file.Encrypted)) foreach (var block in compressed!) encoded.Write((uint)block.Length);

            var slash = file.Path.LastIndexOf('/') + 1;
            listed.Add((file.Path[..slash], file.Path[slash..], location));
        }

        var directory = new BinaryWriter(new MemoryStream());
        var folders = listed.GroupBy(entry => entry.Folder).ToList();
        directory.Write(folders.Count);
        foreach (var folder in folders)
        {
            AssetBuilder.FString(directory, folder.Key);
            directory.Write(folder.Count());
            foreach (var entry in folder)
            {
                AssetBuilder.FString(directory, entry.Name);
                directory.Write(entry.Location);
            }
        }
        var directoryBlob = Encrypt(((MemoryStream)directory.BaseStream).ToArray());
        var directoryOffset = pak.Position;
        pak.Write(directoryBlob);

        var index = new BinaryWriter(new MemoryStream());
        AssetBuilder.FString(index, "../../../");
        index.Write(files.Length);
        index.Write(0L);
        index.Write(0);
        index.Write(1);
        index.Write(directoryOffset);
        index.Write((long)directoryBlob.Length);
        index.Write(new byte[20]);
        var encodedBytes = ((MemoryStream)encoded.BaseStream).ToArray();
        index.Write(encodedBytes.Length);
        index.Write(encodedBytes);
        index.Write(0);
        var indexBlob = Encrypt(((MemoryStream)index.BaseStream).ToArray());
        var indexOffset = pak.Position;
        pak.Write(indexBlob);

        var footer = new byte[222];
        footer[16] = 1;
        BitConverter.TryWriteBytes(footer.AsSpan(17), magic);
        BitConverter.TryWriteBytes(footer.AsSpan(21), version);
        footer[25] = 0x5A;
        BitConverter.TryWriteBytes(footer.AsSpan(26), indexOffset);
        BitConverter.TryWriteBytes(footer.AsSpan(34), (long)indexBlob.Length);
        for (var i = 0; i < Methods.Length; i++) Encoding.ASCII.GetBytes(Methods[i]).CopyTo(footer, 62 + i * 32);
        pak.Write(footer);
        return pak.ToArray();
    }

    static byte[] Pad(byte[] data) => data.Length % 16 == 0 ? data : [.. data, .. new byte[16 - data.Length % 16]];

    static byte[] Zlib(byte[] chunk)
    {
        var output = new MemoryStream();
        using (var zlib = new ZLibStream(output, CompressionLevel.Optimal, leaveOpen: true)) zlib.Write(chunk);
        return output.ToArray();
    }
}
