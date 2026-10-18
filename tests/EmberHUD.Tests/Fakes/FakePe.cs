using System.Text;

namespace EmberHUD.Tests;

internal static class FakePe
{
    private const int PeOffset = 0x40;
    private const int OptionalSize = 240;
    private const int HeadersSize = 0x400;
    private const uint ExecuteFlag = 0x20000000;

    public static byte[] Build(params (string Name, int Rva, byte[] Data, bool Executable)[] sections)
    {
        var file = new List<byte>(new byte[HeadersSize]);
        var raw = new int[sections.Length];
        for (var i = 0; i < sections.Length; i++)
        {
            raw[i] = file.Count;
            file.AddRange(sections[i].Data);
            file.AddRange(new byte[(0x200 - file.Count % 0x200) % 0x200]);
        }

        var bytes = file.ToArray();
        var sizeOfImage = sections.Length == 0 ? 0x1000 : (sections.Max(s => s.Rva + s.Data.Length) + 0xFFF) & ~0xFFF;
        BitConverter.GetBytes((ushort)0x5A4D).CopyTo(bytes, 0);
        BitConverter.GetBytes(PeOffset).CopyTo(bytes, 0x3C);
        BitConverter.GetBytes(0x00004550).CopyTo(bytes, PeOffset);
        BitConverter.GetBytes((ushort)0x8664).CopyTo(bytes, PeOffset + 4);
        BitConverter.GetBytes((ushort)sections.Length).CopyTo(bytes, PeOffset + 6);
        BitConverter.GetBytes((ushort)OptionalSize).CopyTo(bytes, PeOffset + 20);
        var optional = PeOffset + 24;
        BitConverter.GetBytes((ushort)0x20B).CopyTo(bytes, optional);
        BitConverter.GetBytes(sizeOfImage).CopyTo(bytes, optional + 56);

        for (var i = 0; i < sections.Length; i++)
        {
            var header = optional + OptionalSize + i * 40;
            Encoding.ASCII.GetBytes(sections[i].Name).Take(8).ToArray().CopyTo(bytes, header);
            BitConverter.GetBytes(sections[i].Data.Length).CopyTo(bytes, header + 8);
            BitConverter.GetBytes(sections[i].Rva).CopyTo(bytes, header + 12);
            BitConverter.GetBytes((sections[i].Data.Length + 0x1FF) & ~0x1FF).CopyTo(bytes, header + 16);
            BitConverter.GetBytes(raw[i]).CopyTo(bytes, header + 20);
            BitConverter.GetBytes(sections[i].Executable ? ExecuteFlag : 0x40000040u).CopyTo(bytes, header + 36);
        }

        return bytes;
    }
}
