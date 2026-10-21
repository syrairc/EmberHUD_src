using System.IO.Compression;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using Microsoft.Win32.SafeHandles;

namespace EmberHUD.Assets;

internal sealed record PakEntry(string Path, long Offset, long Size, long RawSize, int Method, bool Encrypted, int BlockSize, (long Start, long End)[] Blocks)
{
    public static long HeaderBytes(int method, int blocks) => 8 + 1 + 8 + 8 + 4 + 20 + (method != 0 ? 4 + 16 * blocks : 0) + 1 + 4;

    public long HeaderSize => HeaderBytes(Method, Blocks.Length);
}

internal sealed class PakFile : IDisposable
{
    readonly SafeFileHandle _file;
    readonly Aes _aes;

    PakFile(string path, SafeFileHandle file, Aes aes)
    {
        Path = path;
        _file = file;
        _aes = aes;
    }

    public string Path { get; }

    public string[] Methods { get; private set; } = [];

    public List<PakEntry> Entries { get; } = [];

    string Name => System.IO.Path.GetFileName(Path);

    public static PakFile Open(string path, byte[] key)
    {
        var aes = Aes.Create();
        aes.Key = key;
        var pak = new PakFile(path, File.OpenHandle(path), aes);
        try
        {
            pak.ReadIndex();
            return pak;
        }
        catch
        {
            pak.Dispose();
            throw;
        }
    }

    void ReadIndex()
    {
        var length = RandomAccess.GetLength(_file);
        if (length < 222) throw new InvalidDataException($"{Name} - too small for a pak footer - game patched?");
        var footer = new Cursor(ReadAt(length - 222, 222), 16);
        var encryptedIndex = footer.U8() != 0;
        var magic = footer.U32();
        var version = footer.I32();
        if (magic != 0x6B2A56B8 || version != 12) throw new InvalidDataException($"{Name} - pak format changed - game patched? (magic 0x{magic:X8}, version {version})");
        footer.Take(1);
        var indexOffset = footer.I64();
        var indexSize = footer.I64();
        footer.Take(20);
        Methods = [.. Enumerable.Range(0, 5).Select(_ => Encoding.ASCII.GetString(footer.Take(32)).Split('\0')[0])];
        try
        {
            var index = new Cursor(Blob(indexOffset, indexSize, encryptedIndex));
            var mount = index.FString();
            var count = index.I32();
            index.Take(8);
            if (index.I32() != 0) index.Take(36);
            (long Offset, long Size)? fullDirectory = index.I32() != 0 ? (index.I64(), index.I64()) : null;
            if (fullDirectory is not null) index.Take(20);
            var encoded = index.Take(index.I32()).ToArray();
            var nonEncoded = new List<PakEntry>();
            for (var i = index.I32(); i > 0; i--) nonEncoded.Add(ReadEntry(index));
            if (fullDirectory is not { } directoryAt) throw new InvalidDataException($"{Name} - no full directory index");

            var directory = new Cursor(Blob(directoryAt.Offset, directoryAt.Size, encryptedIndex));
            for (var folders = directory.I32(); folders > 0; folders--)
            {
                var folder = directory.FString();
                for (var files = directory.I32(); files > 0; files--)
                {
                    var file = directory.FString();
                    var location = directory.I32();
                    var entry = location >= 0 ? DecodeEntry(new Cursor(encoded, location)) : nonEncoded[-location - 1];
                    Entries.Add(entry with { Path = (mount + folder + file).Replace("../../../", "") });
                }
            }
            if (Entries.Count != count) throw new InvalidDataException($"{Name} - {Entries.Count} entries, index says {count}");
        }
        catch (Exception ex) when (ex is not InvalidDataException)
        {
            throw new InvalidDataException($"{Name} - index did not decode - pak key changed? ({ex.Message})", ex);
        }
    }

    static PakEntry DecodeEntry(Cursor cursor)
    {
        var bits = cursor.U32();
        var blockSize = (int)(bits & 0x3F) << 11;
        if ((bits & 0x3F) == 0x3F) blockSize = (int)cursor.U32();
        var blockCount = (int)(bits >> 6 & 0xFFFF);
        var encrypted = (bits & 1u << 22) != 0;
        var method = (int)(bits >> 23 & 0x3F);
        long offset = (bits & 1u << 31) != 0 ? cursor.U32() : cursor.I64();
        long rawSize = (bits & 1u << 30) != 0 ? cursor.U32() : cursor.I64();
        var size = method != 0 ? (bits & 1u << 29) != 0 ? cursor.U32() : cursor.I64() : rawSize;
        var blocks = new (long Start, long End)[blockCount];
        long start = PakEntry.HeaderBytes(method, blockCount);
        if (blockCount == 1 && !encrypted) blocks[0] = (start, start + size);
        else
        {
            for (var i = 0; i < blockCount; i++)
            {
                var length = cursor.U32();
                blocks[i] = (start, start + length);
                start += encrypted ? (length + 15) & ~15L : length;
            }
        }
        return new PakEntry("", offset, size, rawSize, method, encrypted, blockSize, blocks);
    }

    static PakEntry ReadEntry(Cursor cursor)
    {
        var offset = cursor.I64();
        cursor.U8();
        var size = cursor.I64();
        var rawSize = cursor.I64();
        var method = (int)cursor.U32();
        cursor.Take(20);
        var blocks = new (long Start, long End)[method != 0 ? cursor.U32() : 0];
        for (var i = 0; i < blocks.Length; i++) blocks[i] = (cursor.I64(), cursor.I64());
        var encrypted = (cursor.U8() & 3) == 3;
        return new PakEntry("", offset, size, rawSize, method, encrypted, (int)cursor.U32(), blocks);
    }

    public byte[] Read(PakEntry entry)
    {
        if (entry.Method == 0)
        {
            var stored = ReadAt(entry.Offset + entry.HeaderSize, (int)(entry.Encrypted ? Align(entry.Size) : entry.Size));
            return (entry.Encrypted ? Decrypt(stored) : stored)[..(int)entry.Size];
        }
        var method = entry.Method <= Methods.Length ? Methods[entry.Method - 1] : $"#{entry.Method}";
        var output = new byte[entry.RawSize];
        var at = 0;
        foreach (var (start, end) in entry.Blocks)
        {
            var length = (int)(end - start);
            var block = ReadAt(entry.Offset + start, entry.Encrypted ? (int)Align(length) : length);
            if (entry.Encrypted) block = Decrypt(block);
            var want = (int)Math.Min(entry.BlockSize, entry.RawSize - at);
            Decompress(method, block.AsSpan(0, length), output.AsSpan(at, want), entry.Path);
            at += want;
        }
        if (at != entry.RawSize) throw new InvalidDataException($"{entry.Path} - blocks gave {at} bytes, expected {entry.RawSize}");
        return output;
    }

    static void Decompress(string method, ReadOnlySpan<byte> source, Span<byte> target, string path)
    {
        if (method == "Oodle")
        {
            try
            {
                Oodle.Decompress(source, target);
            }
            catch (InvalidDataException ex)
            {
                throw new InvalidDataException($"{path} - {ex.Message}", ex);
            }
            return;
        }
        if (method != "Zlib") throw new InvalidDataException($"{path} - compression method '{method}' not supported");
        try
        {
            using var zlib = new ZLibStream(new MemoryStream(source.ToArray()), CompressionMode.Decompress);
            zlib.ReadExactly(target);
            if (zlib.ReadByte() != -1) throw new InvalidDataException($"{path} - zlib block is longer than {target.Length} bytes");
        }
        catch (IOException ex)
        {
            throw new InvalidDataException($"{path} - zlib block did not decode - {ex.Message}", ex);
        }
    }

    static long Align(long size) => (size + 15) & ~15L;

    byte[] Blob(long offset, long size, bool encrypted)
    {
        var data = ReadAt(offset, checked((int)size));
        return encrypted ? Decrypt(data) : data;
    }

    byte[] Decrypt(byte[] data) => _aes.DecryptEcb(data, PaddingMode.None);

    byte[] ReadAt(long offset, int count)
    {
        var buffer = new byte[count];
        if (offset < 0 || RandomAccess.Read(_file, buffer, offset) != count) throw new InvalidDataException($"{Name} - truncated at {offset} (+{count})");
        return buffer;
    }

    public void Dispose()
    {
        _file.Dispose();
        _aes.Dispose();
    }
}

internal sealed class Cursor(byte[] data, int at = 0)
{
    public int At { get; set; } = at;

    public bool AtEnd => At >= data.Length;

    public ReadOnlySpan<byte> Take(int count)
    {
        var span = data.AsSpan(At, count);
        At += count;
        return span;
    }

    public byte U8() => Take(1)[0];

    public int I32() => BitConverter.ToInt32(Take(4));

    public uint U32() => BitConverter.ToUInt32(Take(4));

    public long I64() => BitConverter.ToInt64(Take(8));

    public float F32() => BitConverter.ToSingle(Take(4));

    public string FString()
    {
        var length = I32();
        return (length switch
        {
            0 => "",
            < 0 => Encoding.Unicode.GetString(Take(checked(-length * 2))),
            _ => Encoding.UTF8.GetString(Take(length)),
        }).TrimEnd('\0');
    }
}

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
