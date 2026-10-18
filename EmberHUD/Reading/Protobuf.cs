namespace EmberHUD.Host.Reading;

internal readonly record struct ProtoField(int Number, long Varint, byte[] Bytes);

internal static class Protobuf
{
    public static bool TryParse(ReadOnlySpan<byte> data, out ProtoField[] fields)
    {
        var list = new List<ProtoField>();
        fields = [];
        var at = 0;
        while (at < data.Length)
        {
            if (!TryReadVarint(data, ref at, out var key)) return false;
            var number = (int)(key >> 3);
            switch ((int)(key & 7))
            {
                case 0:
                    if (!TryReadVarint(data, ref at, out var varint)) return false;
                    list.Add(new ProtoField(number, (long)varint, []));
                    break;
                case 2:
                    if (!TryReadVarint(data, ref at, out var length) || length > (ulong)(data.Length - at)) return false;
                    list.Add(new ProtoField(number, 0, data.Slice(at, (int)length).ToArray()));
                    at += (int)length;
                    break;
                default:
                    return false;
            }
        }

        fields = [.. list];
        return true;
    }

    private static bool TryReadVarint(ReadOnlySpan<byte> data, ref int at, out ulong value)
    {
        value = 0;
        for (var shift = 0; shift < 70 && at < data.Length; shift += 7)
        {
            var b = data[at++];
            value |= (ulong)(b & 0x7F) << shift;
            if ((b & 0x80) == 0) return true;
        }

        return false;
    }
}
