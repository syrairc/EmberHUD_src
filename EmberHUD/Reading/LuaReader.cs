using System.Globalization;
using System.Runtime.InteropServices;
using System.Text;
using EmberHUD.Host.Memory;

namespace EmberHUD.Host.Reading;

internal readonly record struct LuaTable(nint Address);

internal readonly record struct LuaEntryRow(string Key, string Type, string Value);

internal sealed class LuaReader(IReadBackend memory, nint moduleBase)
{
    private readonly Dictionary<nint, uint> _keyHashes = [];
    private uint _seed;

    public static uint Hash(uint seed, string text)
    {
        var bytes = Encoding.UTF8.GetBytes(text);
        var h = seed ^ (uint)bytes.Length;
        for (var i = bytes.Length; i >= 1; i--) h ^= (h << 5) + (h >> 2) + bytes[i - 1];
        return h;
    }

    public bool TryOpen(out LuaTable globals)
    {
        globals = default;
        _keyHashes.Clear();
        if (Offsets.LuaGlobalRva == 0 ||
            !memory.TryReadPtr(moduleBase + Offsets.LuaGlobalRva, out var manager) || manager == 0 ||
            !memory.TryReadPtr(manager + Offsets.LuaMgrState, out var state) || state == 0 ||
            !memory.TryReadPtr(state + Offsets.LuaStateGlobal, out var global) || global == 0 ||
            !memory.TryRead(global + Offsets.LuaGlobalSeed, out _seed) ||
            !TryReadValue(global + Offsets.LuaGlobalRegistry, out var registry, out var registryTag) || registryTag != Offsets.LuaTagTable ||
            !TryReadHeader(new LuaTable((nint)registry), out var header) || header.ArraySize < Offsets.LuaRegistryGlobals ||
            !TryReadValue(header.Array + (Offsets.LuaRegistryGlobals - 1) * Offsets.LuaTValueSize, out var globalsAddress, out var globalsTag) ||
            globalsTag != Offsets.LuaTagTable || globalsAddress == 0)
            return false;

        globals = new LuaTable((nint)globalsAddress);
        return true;
    }

    public bool TryGetTable(LuaTable table, string key, out LuaTable value)
    {
        value = default;
        if (!TryFind(table, key, out var raw, out var tag) || tag != Offsets.LuaTagTable || raw == 0) return false;
        value = new LuaTable((nint)raw);
        return true;
    }

    public bool TryGetInt(LuaTable table, string key, out long value)
    {
        value = 0;
        if (!TryFind(table, key, out var raw, out var tag) || tag != Offsets.LuaTagInt) return false;
        value = raw;
        return true;
    }

    public bool TryGetBool(LuaTable table, string key, out bool value)
    {
        value = false;
        if (!TryFind(table, key, out var raw, out var tag) || tag != Offsets.LuaTagBool) return false;
        value = (int)raw != 0;
        return true;
    }

    public bool TryGetString(LuaTable table, string key, out string value)
    {
        value = "";
        if (!TryFind(table, key, out var raw, out var tag) || (tag != Offsets.LuaTagShortString && tag != Offsets.LuaTagLongString)) return false;
        return TryReadString((nint)raw, out value);
    }

    public bool TryGetStringCut(LuaTable table, string key, out string value)
    {
        value = "";
        if (!TryFind(table, key, out var raw, out var tag) || !IsString(tag)) return false;
        return TryReadString((nint)raw, out value, true);
    }

    public bool TryGetFloatAt(LuaTable table, long index, out double value)
    {
        value = 0;
        if (!TryReadHeader(table, out var header)) return false;
        long raw;
        uint tag;
        if (index >= 1 && index <= header.ArraySize)
        {
            if (!TryReadValue(header.Array + (nint)(index - 1) * Offsets.LuaTValueSize, out raw, out tag)) return false;
        }
        else
        {
            if (!TryReadNodes(header, out var nodes)) return false;
            var at = -1;
            for (var i = 0; i < nodes.Length && at < 0; i += Offsets.LuaNodeSize)
                if (NodeKeyTag(nodes, i) == Offsets.LuaTagInt && MemoryMarshal.Read<long>(nodes.AsSpan(i + Offsets.LuaNodeKey)) == index) at = i;
            if (at < 0) return false;
            raw = MemoryMarshal.Read<long>(nodes.AsSpan(at + Offsets.LuaNodeValue));
            tag = NodeTag(nodes, at);
        }

        if (tag == Offsets.LuaTagFloat) value = BitConverter.Int64BitsToDouble(raw);
        else if (tag == Offsets.LuaTagInt) value = raw;
        else if (!IsString(tag) || !TryReadString((nint)raw, out var text) || !double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out value)) return false;
        return !double.IsNaN(value) && !double.IsInfinity(value);
    }

    public bool TryGetTableAt(LuaTable table, long index, out LuaTable value)
    {
        value = default;
        if (!TryReadHeader(table, out var header)) return false;

        if (index >= 1 && index <= header.ArraySize)
        {
            if (!TryReadValue(header.Array + (nint)(index - 1) * Offsets.LuaTValueSize, out var raw, out var tag) || tag != Offsets.LuaTagTable || raw == 0) return false;
            value = new LuaTable((nint)raw);
            return true;
        }

        if (!TryReadNodes(header, out var nodes)) return false;
        for (var i = 0; i < nodes.Length; i += Offsets.LuaNodeSize)
        {
            if (NodeKeyTag(nodes, i) != Offsets.LuaTagInt || MemoryMarshal.Read<long>(nodes.AsSpan(i + Offsets.LuaNodeKey)) != index) continue;
            if (NodeTag(nodes, i) != Offsets.LuaTagTable) return false;
            var raw = MemoryMarshal.Read<long>(nodes.AsSpan(i + Offsets.LuaNodeValue));
            if (raw == 0) return false;
            value = new LuaTable((nint)raw);
            return true;
        }

        return false;
    }

    public bool TryGetIntAt(LuaTable table, long index, out long value)
    {
        value = 0;
        if (!TryReadHeader(table, out var header)) return false;

        if (index >= 1 && index <= header.ArraySize)
        {
            if (!TryReadValue(header.Array + (nint)(index - 1) * Offsets.LuaTValueSize, out var raw, out var tag) || tag != Offsets.LuaTagInt) return false;
            value = raw;
            return true;
        }

        if (!TryReadNodes(header, out var nodes)) return false;
        for (var i = 0; i < nodes.Length; i += Offsets.LuaNodeSize)
        {
            if (NodeKeyTag(nodes, i) != Offsets.LuaTagInt || MemoryMarshal.Read<long>(nodes.AsSpan(i + Offsets.LuaNodeKey)) != index) continue;
            if (NodeTag(nodes, i) != Offsets.LuaTagInt) return false;
            value = MemoryMarshal.Read<long>(nodes.AsSpan(i + Offsets.LuaNodeValue));
            return true;
        }

        return false;
    }

    public bool TryGetBytesAt(LuaTable table, long index, out byte[] value)
    {
        value = [];
        if (!TryReadHeader(table, out var header)) return false;

        if (index >= 1 && index <= header.ArraySize)
        {
            return TryReadValue(header.Array + (nint)(index - 1) * Offsets.LuaTValueSize, out var raw, out var tag) && IsString(tag) && TryReadBytes((nint)raw, out value);
        }

        if (!TryReadNodes(header, out var nodes)) return false;
        for (var i = 0; i < nodes.Length; i += Offsets.LuaNodeSize)
        {
            if (NodeKeyTag(nodes, i) != Offsets.LuaTagInt || MemoryMarshal.Read<long>(nodes.AsSpan(i + Offsets.LuaNodeKey)) != index) continue;
            return IsString(NodeTag(nodes, i)) && TryReadBytes((nint)MemoryMarshal.Read<long>(nodes.AsSpan(i + Offsets.LuaNodeValue)), out value);
        }

        return false;
    }

    public int[] AffixIds(LuaTable parent, string key) =>
        TryGetTable(parent, key, out var table)
            ? [.. IntKeyedTables(table).OrderBy(r => r.Key).Take(Offsets.MaxGroundAffixes)
                .Select(r => (ok: TryGetInt(r.Value, Offsets.LuaGroundAffixId, out var id) && id is >= int.MinValue and <= int.MaxValue, id))
                .Where(r => r.ok).Select(r => (int)r.id)]
            : [];

    public IEnumerable<(long Key, LuaTable Value)> IntKeyedTables(LuaTable table)
    {
        if (!TryReadHeader(table, out var header)) yield break;

        if (header.ArraySize > 0 && memory.TryReadArray(header.Array, (int)header.ArraySize * Offsets.LuaTValueSize, out byte[] array))
        {
            for (var i = 0; i < header.ArraySize; i++)
            {
                var at = i * Offsets.LuaTValueSize;
                var raw = MemoryMarshal.Read<long>(array.AsSpan(at));
                if (MemoryMarshal.Read<uint>(array.AsSpan(at + Offsets.LuaNodeTag)) == Offsets.LuaTagTable && raw != 0) yield return (i + 1, new LuaTable((nint)raw));
            }
        }

        if (!TryReadNodes(header, out var nodes)) yield break;
        for (var i = 0; i < nodes.Length; i += Offsets.LuaNodeSize)
        {
            if (NodeKeyTag(nodes, i) != Offsets.LuaTagInt || NodeTag(nodes, i) != Offsets.LuaTagTable) continue;
            var raw = MemoryMarshal.Read<long>(nodes.AsSpan(i + Offsets.LuaNodeValue));
            if (raw != 0) yield return (MemoryMarshal.Read<long>(nodes.AsSpan(i + Offsets.LuaNodeKey)), new LuaTable((nint)raw));
        }
    }

    public bool TryResolve(LuaTable root, string path, out LuaTable table, out string failedAt)
    {
        table = default;
        failedAt = "";
        if (!TryReadHeader(root, out _)) return false;

        var current = root;
        foreach (var segment in path.Length == 0 ? [] : path.Split('.'))
        {
            var found = segment == "@meta" ? TryGetMetatable(current, out var next)
                : long.TryParse(segment, NumberStyles.None, CultureInfo.InvariantCulture, out var index) ? TryGetTableAt(current, index, out next)
                : TryGetTable(current, segment, out next);
            if (!found)
            {
                failedAt = segment;
                return false;
            }

            current = next;
        }

        table = current;
        return true;
    }

    public LuaEntryRow[] Entries(LuaTable table, int max, int skip, string filter, out int total)
    {
        total = 0;
        if (!TryReadHeader(table, out var header)) return [];

        var rows = new List<LuaEntryRow>();
        var count = 0;
        skip = Math.Max(skip, 0);
        void Add(string key, long raw, uint tag)
        {
            if (!key.Contains(filter, StringComparison.OrdinalIgnoreCase)) return;
            if (count++ >= skip && rows.Count < max) rows.Add(Describe(key, raw, tag));
        }

        if (header.ArraySize > 0 && memory.TryReadArray(header.Array, (int)header.ArraySize * Offsets.LuaTValueSize, out byte[] array))
        {
            for (var i = 0; i < header.ArraySize; i++)
            {
                var at = i * Offsets.LuaTValueSize;
                var tag = MemoryMarshal.Read<uint>(array.AsSpan(at + Offsets.LuaNodeTag));
                if (tag != 0) Add($"[{i + 1}]", MemoryMarshal.Read<long>(array.AsSpan(at)), tag);
            }
        }

        if (!TryReadNodes(header, out var nodes))
        {
            total = count;
            return [.. rows];
        }

        for (var i = 0; i < nodes.Length; i += Offsets.LuaNodeSize)
        {
            var tag = NodeTag(nodes, i);
            var keyTag = NodeKeyTag(nodes, i);
            if (tag == 0 && keyTag == 0) continue;
            var keyRaw = MemoryMarshal.Read<long>(nodes.AsSpan(i + Offsets.LuaNodeKey));
            var key = keyTag == Offsets.LuaTagInt ? $"[{keyRaw}]"
                : (keyTag == Offsets.LuaTagShortString || keyTag == Offsets.LuaTagLongString) && TryReadString((nint)keyRaw, out var text, true) ? Clean(text)
                : $"<tag 0x{keyTag:X2}>";
            Add(key, MemoryMarshal.Read<long>(nodes.AsSpan(i + Offsets.LuaNodeValue)), tag);
        }

        total = count;
        return [.. rows];
    }

    private bool TryGetMetatable(LuaTable table, out LuaTable value)
    {
        value = default;
        if (!memory.TryReadPtr(table.Address + Offsets.LuaTableMetatable, out var meta) || meta == 0) return false;
        value = new LuaTable(meta);
        return true;
    }

    private LuaEntryRow Describe(string key, long raw, uint tag)
    {
        if (tag == Offsets.LuaTagShortString || tag == Offsets.LuaTagLongString)
            return new LuaEntryRow(key, "string", TryReadString((nint)raw, out var text, true) ? Clean(text) : "");
        if (tag == 0) return new LuaEntryRow(key, "nil", "");
        if (tag == Offsets.LuaTagInt) return new LuaEntryRow(key, "int", raw.ToString(CultureInfo.InvariantCulture));
        if (tag == Offsets.LuaTagFloat) return new LuaEntryRow(key, "float", BitConverter.Int64BitsToDouble(raw).ToString("R", CultureInfo.InvariantCulture));
        if (tag == Offsets.LuaTagBool) return new LuaEntryRow(key, "bool", (int)raw != 0 ? "true" : "false");
        var type = tag == Offsets.LuaTagTable ? "table" : tag == Offsets.LuaTagFunction ? "function" : $"tag 0x{tag:X2}";
        return new LuaEntryRow(key, type, $"0x{raw:X}");
    }

    private static string Clean(string text) => string.Create(text.Length, text, (span, source) =>
    {
        for (var i = 0; i < span.Length; i++) span[i] = char.IsControl(source[i]) ? ' ' : source[i];
    });

    private readonly record struct Header(int LogSize, uint ArraySize, nint Array, nint Node);

    private bool TryReadHeader(LuaTable table, out Header header)
    {
        header = default;
        if (table.Address == 0 || !memory.TryReadArray(table.Address, 32, out byte[] bytes)) return false;
        var logSize = bytes[Offsets.LuaTableLogSize];
        var arraySize = MemoryMarshal.Read<uint>(bytes.AsSpan(Offsets.LuaTableArraySize));
        if (arraySize > Offsets.MaxLuaArray || logSize > Offsets.MaxLuaLogSize) return false;
        header = new Header(logSize, arraySize, (nint)MemoryMarshal.Read<long>(bytes.AsSpan(Offsets.LuaTableArray)), (nint)MemoryMarshal.Read<long>(bytes.AsSpan(Offsets.LuaTableNode)));
        return true;
    }

    private bool TryReadValue(nint at, out long value, out uint tag)
    {
        tag = 0;
        return memory.TryRead(at, out value) && memory.TryRead(at + Offsets.LuaNodeTag, out tag);
    }

    private bool TryReadNodes(Header header, out byte[] nodes)
    {
        nodes = [];
        return header.Node != 0 && memory.TryReadArray(header.Node, (1 << header.LogSize) * Offsets.LuaNodeSize, out nodes);
    }

    private static uint NodeTag(byte[] nodes, int at) => MemoryMarshal.Read<uint>(nodes.AsSpan(at + Offsets.LuaNodeTag));

    private static uint NodeKeyTag(byte[] nodes, int at) => MemoryMarshal.Read<uint>(nodes.AsSpan(at + Offsets.LuaNodeKeyTag));

    private bool TryFind(LuaTable table, string key, out long value, out uint tag)
    {
        value = 0;
        tag = 0;
        if (!TryReadHeader(table, out var header) || header.Node == 0) return false;

        var wanted = Encoding.UTF8.GetBytes(key);
        var hash = Hash(_seed, key);
        var mainAt = header.Node + (nint)(hash & ((1u << header.LogSize) - 1)) * Offsets.LuaNodeSize;
        if (memory.TryReadArray(mainAt, Offsets.LuaNodeSize, out byte[] main) && IsKey(main, 0, hash, wanted))
            return Take(main, 0, out value, out tag);

        if (!TryReadNodes(header, out var nodes)) return false;
        for (var i = 0; i < nodes.Length; i += Offsets.LuaNodeSize)
            if (IsKey(nodes, i, hash, wanted)) return Take(nodes, i, out value, out tag);

        return false;
    }

    private static bool Take(byte[] nodes, int at, out long value, out uint tag)
    {
        value = MemoryMarshal.Read<long>(nodes.AsSpan(at + Offsets.LuaNodeValue));
        tag = NodeTag(nodes, at);
        return true;
    }

    private bool IsKey(byte[] nodes, int at, uint hash, byte[] wanted)
    {
        var keyTag = NodeKeyTag(nodes, at);
        if (keyTag != Offsets.LuaTagShortString && keyTag != Offsets.LuaTagLongString) return false;
        var keyAddress = (nint)MemoryMarshal.Read<long>(nodes.AsSpan(at + Offsets.LuaNodeKey));
        if (keyAddress == 0) return false;

        if (!_keyHashes.TryGetValue(keyAddress, out var keyHash))
        {
            if (!memory.TryRead(keyAddress + Offsets.LuaStringHash, out keyHash)) return false;
            _keyHashes[keyAddress] = keyHash;
        }

        if (keyHash != hash) return false;
        return memory.TryRead(keyAddress + Offsets.LuaStringLength, out int length) && length == wanted.Length &&
               memory.TryReadArray(keyAddress + Offsets.LuaStringChars, length, out byte[] chars) && chars.AsSpan().SequenceEqual(wanted);
    }

    private static bool IsString(uint tag) => tag == Offsets.LuaTagShortString || tag == Offsets.LuaTagLongString;

    private bool TryReadBytes(nint address, out byte[] value)
    {
        value = [];
        return address != 0 && memory.TryRead(address + Offsets.LuaStringLength, out int length) && length >= 0 && length <= Offsets.MaxTarotBlob &&
               memory.TryReadArray(address + Offsets.LuaStringChars, length, out value);
    }

    private bool TryReadString(nint address, out string value, bool cut = false)
    {
        value = "";
        if (address == 0 || !memory.TryRead(address + Offsets.LuaStringLength, out int length) || length < 0 || (length > Offsets.MaxLuaString && !cut) ||
            !memory.TryReadArray(address + Offsets.LuaStringChars, Math.Min(length, Offsets.MaxLuaString), out byte[] chars))
            return false;

        value = Encoding.UTF8.GetString(chars);
        return true;
    }
}
