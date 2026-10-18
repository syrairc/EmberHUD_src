namespace EmberHUD.Host;

internal sealed class PeImage
{
    private const uint ExecuteFlag = 0x20000000;
    private const int SectionHeaderSize = 40;

    private readonly byte[] _file;
    private readonly Section[] _sections;

    private PeImage(byte[] file, int sizeOfImage, Section[] sections)
    {
        _file = file;
        SizeOfImage = sizeOfImage;
        _sections = sections;
    }

    public int SizeOfImage { get; }

    public static bool TryParse(byte[] file, out PeImage image)
    {
        image = null!;
        if (file is null || file.Length < 0x40 || file[0] != 'M' || file[1] != 'Z')
        {
            return false;
        }

        long pe = BitConverter.ToInt32(file, 0x3C);
        if (pe < 0 || pe + 24 > file.Length || BitConverter.ToUInt32(file, (int)pe) != 0x00004550)
        {
            return false;
        }

        int count = BitConverter.ToUInt16(file, (int)pe + 6);
        int optionalSize = BitConverter.ToUInt16(file, (int)pe + 20);
        var optional = pe + 24;
        if (optionalSize < 60 || optional + optionalSize > file.Length || BitConverter.ToUInt16(file, (int)optional) != 0x20B)
        {
            return false;
        }

        var table = optional + optionalSize;
        if (table + (long)count * SectionHeaderSize > file.Length)
        {
            return false;
        }

        var sections = new Section[count];
        for (var i = 0; i < count; i++)
        {
            var header = (int)table + i * SectionHeaderSize;
            var virtualSize = BitConverter.ToInt32(file, header + 8);
            var rva = BitConverter.ToInt32(file, header + 12);
            var rawSize = BitConverter.ToInt32(file, header + 16);
            var rawPointer = BitConverter.ToInt32(file, header + 20);
            var flags = BitConverter.ToUInt32(file, header + 36);
            var size = Math.Min(virtualSize, rawSize);
            if (virtualSize < 0 || rva < 0 || rawSize < 0 || rawPointer < 0 || (long)rawPointer + rawSize > file.Length || (long)rva + size > int.MaxValue)
            {
                return false;
            }

            sections[i] = new Section(rva, size, rawPointer, (flags & ExecuteFlag) != 0);
        }

        image = new PeImage(file, BitConverter.ToInt32(file, (int)optional + 56), sections);
        return true;
    }

    public int RvaOf(int fileOffset)
    {
        foreach (var section in _sections)
        {
            if (fileOffset >= section.RawPointer && fileOffset - section.RawPointer < section.Size)
            {
                return section.Rva + (fileOffset - section.RawPointer);
            }
        }

        return -1;
    }

    public int FileOffsetOf(int rva)
    {
        foreach (var section in _sections)
        {
            if (rva >= section.Rva && rva - section.Rva < section.Size)
            {
                return section.RawPointer + (rva - section.Rva);
            }
        }

        return -1;
    }

    public IReadOnlyList<int> Find(string pattern, int limit)
    {
        var hits = new List<int>();
        var tokens = pattern.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        var bytes = new int[tokens.Length];
        for (var i = 0; i < tokens.Length; i++)
        {
            if (tokens[i] == "?")
            {
                bytes[i] = -1;
            }
            else if (!int.TryParse(tokens[i], System.Globalization.NumberStyles.AllowHexSpecifier, null, out bytes[i]) || bytes[i] > 0xFF)
            {
                return hits;
            }
        }

        var anchor = Array.FindIndex(bytes, b => b >= 0);
        if (anchor < 0 || limit <= 0)
        {
            return hits;
        }

        foreach (var section in _sections.Where(s => s.Executable))
        {
            var span = new ReadOnlySpan<byte>(_file, section.RawPointer, section.Size);
            var from = 0;
            while (from <= span.Length - bytes.Length + anchor)
            {
                var found = span[from..].IndexOf((byte)bytes[anchor]);
                if (found < 0)
                {
                    break;
                }

                var start = from + found - anchor;
                from += found + 1;
                if (start < 0 || start + bytes.Length > span.Length || !Matches(span, start, bytes))
                {
                    continue;
                }

                hits.Add(section.RawPointer + start);
                if (hits.Count >= limit)
                {
                    return hits;
                }
            }
        }

        return hits;
    }

    private static bool Matches(ReadOnlySpan<byte> span, int start, int[] bytes)
    {
        for (var i = 0; i < bytes.Length; i++)
        {
            if (bytes[i] >= 0 && span[start + i] != bytes[i])
            {
                return false;
            }
        }

        return true;
    }

    private readonly record struct Section(int Rva, int Size, int RawPointer, bool Executable);
}
