using System.Text;
using EmberHUD.Host.Memory;

namespace EmberHUD.Host.Reading;

internal sealed class LocText(IReadBackend memory, nint moduleBase)
{
    private readonly Dictionary<string, string> _cache = [];
    private byte _language = byte.MaxValue;

    internal bool TryReadLanguage(out byte language) => memory.TryRead(moduleBase + Offsets.LanguageRva, out language);

    public string? Get(string key)
    {
        if (!memory.TryRead(moduleBase + Offsets.LanguageRva, out byte language)) return null;
        if (language != _language)
        {
            _cache.Clear();
            _language = language;
        }
        if (_cache.TryGetValue(key, out var cached)) return cached;
        if (!TryFind(language, key, out var text)) return null;
        _cache[key] = text;
        return text;
    }

    internal bool TryGet(string key, out string? text)
    {
        text = null;
        if (!TryReadLanguage(out var language)) return false;
        if (language != _language)
        {
            _cache.Clear();
            _language = language;
        }
        if (_cache.TryGetValue(key, out var cached))
        {
            text = cached;
            return true;
        }
        if (!TryLanguageMap(language, out var map)) return false;

        var bytes = Encoding.UTF8.GetBytes(key);
        var hash = Hash(bytes);
        var found = memory.FindHashNode(map + Offsets.HashBucketCount, map, Offsets.MaxLocBuckets, hash, Offsets.LocNodeNext,
            candidate => memory.TryRead(candidate + Offsets.LocNodeHash, out ulong at) && at == hash && KeyMatches(candidate, bytes), out var node);
        if (found == HashFind.Absent) return true;
        if (found == HashFind.Unreadable || !memory.TryReadPtr(node + Offsets.LocNodeValue, out var value) ||
            !TryUtf8(value + Offsets.LocValueText, value + Offsets.LocValueLength, out var read))
            return false;
        _cache[key] = read;
        text = read;
        return true;
    }

    public ((string Key, string Text)[] Matches, bool Truncated) Find(string needle, int max)
    {
        List<(string, string)> found = [];
        if (max <= 0 || !memory.TryRead(moduleBase + Offsets.LanguageRva, out byte language) || !TryLanguageMap(language, out var map))
            return ([], false);

        HashSet<nint> seen = [];
        var whole = memory.TryWalkBuckets(map + Offsets.HashBucketCount, map, Offsets.MaxLocBuckets, Offsets.LocNodeSize, Offsets.LocNodeNext, Offsets.MaxLocScan, (node, bytes) =>
        {
            if (!seen.Add(node))
                return true;

            var value = (nint)BitConverter.ToInt64(bytes[Offsets.LocNodeValue..]);
            if (value != 0 && TryUtf8(value + Offsets.LocValueText, value + Offsets.LocValueLength, out var text) && text.Contains(needle, StringComparison.OrdinalIgnoreCase) &&
                TryUtf8(node + Offsets.LocNodeKey, node + Offsets.LocNodeKeyLength, out var key))
                found.Add((key, text));
            return found.Count < max;
        });
        return ([.. found], !whole && seen.Count >= Offsets.MaxLocScan);
    }

    private bool TryFind(byte language, string key, out string text)
    {
        text = "";
        return TryLanguageMap(language, out var map) && TryLookup(map, Encoding.UTF8.GetBytes(key), out text);
    }

    private bool TryLanguageMap(byte language, out nint map)
    {
        map = 0;
        if (!memory.TryFindHashNode(moduleBase + Offsets.LanguageBucketCountRva, moduleBase + Offsets.LanguageBucketsRva, Offsets.MaxBuckets, language, Offsets.LanguageNodeNext,
                candidate => memory.TryRead(candidate, out byte found) && found == language, out var node))
            return false;

        map = node + Offsets.LanguageNodeMap;
        return true;
    }

    private bool TryLookup(nint map, byte[] key, out string text)
    {
        text = "";
        var hash = Hash(key);
        return memory.TryFindHashNode(map + Offsets.HashBucketCount, map, Offsets.MaxLocBuckets, hash, Offsets.LocNodeNext,
                   candidate => memory.TryRead(candidate + Offsets.LocNodeHash, out ulong found) && found == hash && KeyMatches(candidate, key), out var node) &&
               memory.TryReadPtr(node + Offsets.LocNodeValue, out var value) &&
               TryUtf8(value + Offsets.LocValueText, value + Offsets.LocValueLength, out text);
    }

    private bool KeyMatches(nint node, byte[] key)
    {
        if (!memory.TryRead(node + Offsets.LocNodeKeyLength, out int length) || length != key.Length ||
            !memory.TryReadPtr(node + Offsets.LocNodeKey, out var at) || !memory.TryReadArray(at, length, out byte[] bytes))
            return false;
        return bytes.AsSpan().SequenceEqual(key);
    }

    internal bool TryUtf8(nint pointerAt, nint lengthAt, out string text)
    {
        text = "";
        if (!memory.TryReadPtr(pointerAt, out var at) || at == 0 || !memory.TryRead(lengthAt, out int length) ||
            length <= 0 || length > Offsets.MaxLocText || !memory.TryReadArray(at, length, out byte[] bytes))
            return false;
        text = Encoding.UTF8.GetString(bytes);
        return true;
    }

    internal static uint Hash(ReadOnlySpan<byte> data)
    {
        var length = data.Length;
        var hash = (uint)length;
        var i = 0;
        for (var n = length >> 2; n > 0; n--, i += 4)
        {
            hash += BitConverter.ToUInt16(data[i..]);
            var tmp = ((uint)BitConverter.ToUInt16(data[(i + 2)..]) << 11) ^ hash;
            hash = (hash << 16) ^ tmp;
            hash += hash >> 11;
        }

        switch (length & 3)
        {
            case 3:
                hash += BitConverter.ToUInt16(data[i..]);
                hash ^= hash << 16;
                hash ^= (uint)((sbyte)data[i + 2] << 18);
                hash += hash >> 11;
                break;
            case 2:
                hash += BitConverter.ToUInt16(data[i..]);
                hash ^= hash << 11;
                hash += hash >> 17;
                break;
            case 1:
                hash += (uint)(sbyte)data[i];
                hash ^= hash << 10;
                hash += hash >> 1;
                break;
        }

        hash ^= hash << 3;
        hash += hash >> 5;
        hash ^= hash << 4;
        hash += hash >> 17;
        hash ^= hash << 25;
        hash += hash >> 6;
        return hash;
    }
}
