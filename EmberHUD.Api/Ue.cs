using System.Collections.Concurrent;
using System.Globalization;
using System.Text;

namespace EmberHUD;

public sealed record UProp(string Name, string Type, int Offset, int Size, int ArrayDim, byte BoolMask, nint Struct, UProp? Element, string Owner = "", nint Enum = 0, UProp? Key = null, UProp? Value = null, int Stride = 0);

public sealed record UStructInfo(nint Address, string Name, UStructInfo? Super, int Size, UProp[] Properties)
{
    public UProp? Property(string name) => Properties.FirstOrDefault(p => p.Name == name);
}

public readonly record struct UObj(nint Address)
{
    public int Index => Ue.Layout is { } l && Memory.TryRead(Address + l.ObjectIndex, out int index) ? index : -1;

    public string Name => Ue.Layout is { } l ? Ue.ReadName(Address + l.ObjectName) ?? "?" : "?";

    public nint ClassAddress => Ue.Layout is { } l && Memory.TryRead(Address + l.ObjectClass, out nint cls) ? cls : 0;

    public UStructInfo? Class => Ue.Struct(ClassAddress);

    public UObj? Outer => Ue.Layout is { } l && Memory.TryRead(Address + l.ObjectOuter, out nint outer) && outer != 0 ? new UObj(outer) : null;

    public string FullName
    {
        get
        {
            List<string> path = [Name];
            var outer = Outer;
            for (var depth = 0; outer is { } o && depth < 16; depth++, outer = o.Outer) path.Insert(0, o.Name);
            return $"{Class?.Name ?? "?"} {string.Join('.', path)}";
        }
    }

    public bool IsDefault => Ue.Layout is { } l && Name.StartsWith(l.DefaultPrefix, StringComparison.Ordinal);

    public bool IsA(string className)
    {
        for (var info = Class; info is not null; info = info.Super)
            if (info.Name == className) return true;
        return false;
    }
}

public readonly record struct UEntry(nint Address, UProp Key, UProp? Value);

public static class Ue
{
    const int MaxSuperDepth = 64;
    const int MaxFields = 4096;
    const int MaxEnumNames = 4096;
    const int MaxStringChars = 512;
    const int MaxSparseSlots = 65536;
    const int MaxEntryStride = 4096;
    static readonly CultureInfo Invariant = CultureInfo.InvariantCulture;
    static readonly Lock Gate = new();
    static volatile Cache _cache = new(-1);

    public static UEntry[] Entries(nint at, UProp prop, int max)
    {
        var key = prop.Type switch { "MapProperty" => prop.Key, "SetProperty" => prop.Element, _ => null };
        if (Layout is not { } l || key is null || prop.Stride <= 0 || prop.Stride > MaxEntryStride || max <= 0 || prop.Type == "MapProperty" && prop.Value is null) return [];
        if (!Memory.TryRead(at, out nint data) || data == 0 ||
            !Memory.TryRead(at + l.ArrayNum, out int slots) || slots <= 0 || slots > MaxSparseSlots ||
            !Memory.TryRead(at + l.SparseNumBits, out int bits) || bits < slots)
            return [];
        var flagsAt = Memory.TryRead(at + l.SparseFlagsSecondary, out nint secondary) && secondary != 0 ? secondary : slots <= l.SparseInlineBits ? at + l.SparseFlagsInline : 0;
        var words = new byte[(slots + 31) / 32 * 4];
        if (flagsAt == 0 || Memory.Read(flagsAt, words) != words.Length) return [];
        List<UEntry> entries = [];
        for (var slot = 0; slot < slots && entries.Count < max; slot++)
            if (((words[slot >> 3] >> (slot & 7)) & 1) != 0) entries.Add(new UEntry(data + slot * prop.Stride, key, prop.Value));
        return [.. entries];
    }

    internal static UeLayout? Layout => Memory.Layout;

    static Cache Current()
    {
        var cache = _cache;
        var generation = Memory.Generation;
        if (cache.Generation == generation) return cache;
        lock (Gate)
        {
            if (_cache.Generation != generation) _cache = new Cache(generation);
            return _cache;
        }
    }

    public static string? Name(int index) => Name(index, Current());

    public static string? ReadName(nint at) => ReadName(at, Current());

    public static UObj[] Objects()
    {
        if (Layout is not { } l || !Memory.TryRead(Memory.ObjectArray, out nint chunks) || chunks == 0 ||
            !Memory.TryRead(Memory.ObjectArray + l.ObjectsNum, out int count) || count <= 0 || count > l.MaxObjectChunks * l.ObjectsPerChunk)
            return [];

        List<UObj> objects = new(count);
        var items = new byte[l.ObjectsPerChunk * l.ObjectItemSize];
        for (var chunk = 0; chunk * l.ObjectsPerChunk < count; chunk++)
        {
            var inChunk = Math.Min(l.ObjectsPerChunk, count - chunk * l.ObjectsPerChunk);
            var span = items.AsSpan(0, inChunk * l.ObjectItemSize);
            if (!Memory.TryRead(chunks + chunk * nint.Size, out nint start) || start == 0 || Memory.Read(start, span) != span.Length) continue;
            for (var i = 0; i < inChunk; i++)
            {
                var item = span[(i * l.ObjectItemSize)..];
                var obj = (nint)BitConverter.ToInt64(item);
                if (obj != 0 && (BitConverter.ToInt32(item[l.ObjectItemFlags..]) & l.PendingKillFlag) == 0) objects.Add(new UObj(obj));
            }
        }
        return [.. objects];
    }

    public static bool TryObject(nint at, out UObj obj)
    {
        obj = new UObj(at);
        if (Layout is not { } l || at == 0) return false;
        var cache = Current();
        return Memory.TryRead(at + l.ObjectClass, out nint cls) && cls != 0 &&
               Memory.TryRead(cls + l.ObjectClass, out nint meta) && IsClassClass(l, meta, cache) &&
               ReadName(at + l.ObjectName, cache) is not null;
    }

    public static bool TryWeak(int index, int serial, out UObj obj)
    {
        obj = default;
        if (Layout is not { } l || index < 0 || serial == 0 || !Memory.TryRead(Memory.ObjectArray, out nint chunks) || chunks == 0) return false;
        var chunk = index / l.ObjectsPerChunk;
        if (chunk >= l.MaxObjectChunks || !Memory.TryRead(chunks + chunk * nint.Size, out nint start) || start == 0) return false;
        var item = start + index % l.ObjectsPerChunk * l.ObjectItemSize;
        return Memory.TryRead(item, out nint at) && at != 0 &&
               Memory.TryRead(item + l.ObjectItemSerial, out int actual) && actual == serial &&
               TryObject(at, out obj);
    }

    public static nint DefaultObject(UStructInfo cls) =>
        Layout is { } l && Memory.TryRead(cls.Address + l.ClassDefaultObject, out nint at) && at != 0 && TryObject(at, out _) ? at : 0;

    static string SoftPath(ReadOnlySpan<byte> container, UProp prop, UeLayout l)
    {
        var at = prop.Offset + l.SoftPathName;
        if (at < 0 || at > container.Length - 8) return "??";
        if (TryWeak(BitConverter.ToInt32(container[prop.Offset..]), BitConverter.ToInt32(container[(prop.Offset + l.WeakSerial)..]), out var target)) return Pointer(target.Address);
        return NameOf(BitConverter.ToInt32(container[at..]), BitConverter.ToInt32(container[(at + 4)..]), Current()) switch { null => "??", "None" => "null", var path => path };
    }

    static bool IsClassClass(UeLayout l, nint meta, Cache cache)
    {
        for (var depth = 0; meta != 0 && depth < MaxSuperDepth; depth++)
        {
            if (ReadName(meta + l.ObjectName, cache) == l.ClassClassName) return true;
            if (!Memory.TryRead(meta + l.StructSuper, out meta)) return false;
        }
        return false;
    }

    public static UStructInfo? Struct(nint at) => Struct(at, 0, Current());

    public static string Format(ReadOnlySpan<byte> container, UProp prop)
    {
        if (Layout is not { } l || prop.Offset < 0 || prop.Size <= 0 || prop.Size > container.Length - prop.Offset) return "??";
        Span<byte> v = stackalloc byte[16];
        container.Slice(prop.Offset, Math.Min(prop.Size, v.Length)).CopyTo(v);
        return prop.Type switch
        {
            "BoolProperty" => (v[0] & prop.BoolMask) != 0 ? "true" : "false",
            "ByteProperty" => Enumerated(prop, v[0]),
            "Int8Property" => ((sbyte)v[0]).ToString(Invariant),
            "Int16Property" => BitConverter.ToInt16(v).ToString(Invariant),
            "UInt16Property" => BitConverter.ToUInt16(v).ToString(Invariant),
            "IntProperty" => BitConverter.ToInt32(v).ToString(Invariant),
            "UInt32Property" => BitConverter.ToUInt32(v).ToString(Invariant),
            "Int64Property" => BitConverter.ToInt64(v).ToString(Invariant),
            "UInt64Property" => BitConverter.ToUInt64(v).ToString(Invariant),
            "FloatProperty" => BitConverter.ToSingle(v).ToString("0.###", Invariant),
            "DoubleProperty" => BitConverter.ToDouble(v).ToString("0.###", Invariant),
            "EnumProperty" => Enumerated(prop, prop.Size switch { 1 => v[0], 2 => BitConverter.ToInt16(v), 4 => BitConverter.ToInt32(v), _ => BitConverter.ToInt64(v) }),
            "NameProperty" => NameOf(BitConverter.ToInt32(v), BitConverter.ToInt32(v[4..]), Current()) ?? "??",
            "StrProperty" => FString((nint)BitConverter.ToInt64(v), BitConverter.ToInt32(v[l.ArrayNum..])),
            "ObjectProperty" or "ClassProperty" => Pointer((nint)BitConverter.ToInt64(v)),
            "WeakObjectProperty" or "LazyObjectProperty" => BitConverter.ToInt32(v) < 0 || BitConverter.ToInt32(v[l.WeakSerial..]) == 0 ? "null" : TryWeak(BitConverter.ToInt32(v), BitConverter.ToInt32(v[l.WeakSerial..]), out var weak) ? Pointer(weak.Address) : "stale",
            "SoftObjectProperty" => SoftPath(container, prop, l),
            "ArrayProperty" or "MapProperty" or "SetProperty" => $"[{BitConverter.ToInt32(v[l.ArrayNum..])}]",
            "StructProperty" => Struct(prop.Struct)?.Name ?? "struct",
            _ => prop.Type,
        };
    }

    public static string? EnumName(nint enumAt, long value)
    {
        if (Layout is not { } l || enumAt == 0) return null;
        foreach (var (known, name) in EnumNames(l, enumAt, Current()))
            if (known == value) return name;
        return null;
    }

    static (long Value, string Name)[] EnumNames(UeLayout l, nint at, Cache cache)
    {
        if (cache.Enums.TryGetValue(at, out var cached)) return cached;
        if (!Memory.TryRead(at + l.EnumNames, out nint data) || data == 0 ||
            !Memory.TryRead(at + l.EnumNames + l.ArrayNum, out int count) || count <= 0 || count > MaxEnumNames)
            return [];
        var raw = new byte[count * l.EnumNameStride];
        if (Memory.Read(data, raw) != raw.Length) return [];
        List<(long, string)> names = [];
        for (var i = 0; i < count; i++)
        {
            var entry = raw.AsSpan(i * l.EnumNameStride);
            if (NameOf(BitConverter.ToInt32(entry), BitConverter.ToInt32(entry[4..]), cache) is not { } text) continue;
            var cut = text.LastIndexOf("::", StringComparison.Ordinal);
            names.Add((BitConverter.ToInt64(entry[8..]), cut < 0 ? text : text[(cut + 2)..]));
        }
        return cache.Enums[at] = [.. names];
    }

    static string Enumerated(UProp prop, long value) =>
        prop.Enum != 0 && EnumName(prop.Enum, value) is { } name ? $"{name} ({value.ToString(Invariant)})" : value.ToString(Invariant);

    static string? Name(int index, Cache cache)
    {
        if (Layout is not { } l || index < 0) return null;
        if (cache.Names.TryGetValue(index, out var cached)) return cached;
        var block = index / l.NameBlockSize;
        if (block >= l.MaxNameBlocks || !Memory.TryRead(Memory.NamePool + l.NameBlocks + block * nint.Size, out nint start) || start == 0) return null;
        var entry = start + (nint)(index % l.NameBlockSize) * l.NameEntryStride;
        if (!Memory.TryRead(entry, out ushort header)) return null;
        var wide = (header & 1) != 0;
        var length = header >> l.NameLengthShift;
        if (length == 0 || length > l.MaxNameLength) return null;
        var bytes = new byte[wide ? length * 2 : length];
        if (Memory.Read(entry + sizeof(ushort), bytes) != bytes.Length) return null;
        return cache.Names[index] = wide ? Encoding.Unicode.GetString(bytes) : Encoding.Latin1.GetString(bytes);
    }

    static string? ReadName(nint at, Cache cache) =>
        Memory.TryRead(at, out int index) && Memory.TryRead(at + sizeof(int), out int number) ? NameOf(index, number, cache) : null;

    static string? NameOf(int index, int number, Cache cache) =>
        Name(index, cache) is { } name ? number > 0 ? $"{name}_{number - 1}" : name : null;

    static UStructInfo? Struct(nint at, int depth, Cache cache)
    {
        if (Layout is not { } l || at == 0 || depth > MaxSuperDepth) return null;
        if (cache.Structs.TryGetValue(at, out var cached)) return cached;
        if (ReadName(at + l.ObjectName, cache) is not { } name || !Memory.TryRead(at + l.StructSize, out int size)) return null;
        if (!Memory.TryRead(at + l.StructSuper, out nint superAt)) return null;
        var super = superAt != 0 ? Struct(superAt, depth + 1, cache) : null;
        if (superAt != 0 && super is null) return null;
        List<UProp> own = [];
        Memory.TryRead(at + l.StructChildProperties, out nint field);
        for (var guard = 0; field != 0 && guard < MaxFields; guard++)
        {
            if (Prop(l, field, 0, cache) is { } prop) own.Add(prop with { Owner = name });
            if (!Memory.TryRead(field + l.FieldNext, out field)) break;
        }
        return cache.Structs[at] = new UStructInfo(at, name, super, size, [.. (super?.Properties ?? []).Concat(own).OrderBy(p => p.Offset)]);
    }

    static UProp? Prop(UeLayout l, nint field, int depth, Cache cache)
    {
        if (depth > MaxSuperDepth ||
            !Memory.TryRead(field + l.FieldClass, out nint fieldClass) || ReadName(fieldClass + l.FieldClassName, cache) is not { } type ||
            ReadName(field + l.FieldName, cache) is not { } name ||
            !Memory.TryRead(field + l.PropertyArrayDim, out int dim) ||
            !Memory.TryRead(field + l.PropertyElementSize, out int size) ||
            !Memory.TryRead(field + l.PropertyOffset, out int offset))
            return null;

        Memory.TryRead(field + l.PropertyInner, out nint inner);
        byte mask = 0;
        if (type == "BoolProperty")
        {
            mask = Memory.TryRead(field + l.BoolFieldMask, out byte fieldMask) && fieldMask != 0 ? fieldMask : byte.MaxValue;
            if (Memory.TryRead(field + l.BoolByteOffset, out byte byteOffset)) offset += byteOffset;
        }
        nint enumAt = 0;
        if (type == "EnumProperty") Memory.TryRead(field + l.EnumPropertyEnum, out enumAt);
        else if (type == "ByteProperty") Memory.TryRead(field + l.BytePropertyEnum, out enumAt);
        var element = type is "ArrayProperty" or "SetProperty" && inner != 0 ? Prop(l, inner, depth + 1, cache) : null;
        var key = type == "MapProperty" && inner != 0 ? Prop(l, inner, depth + 1, cache) : null;
        var value = type == "MapProperty" && Memory.TryRead(field + l.MapPropertyValue, out nint valueField) && valueField != 0 ? Prop(l, valueField, depth + 1, cache) : null;
        var stride = 0;
        if (type == "MapProperty") Memory.TryRead(field + l.MapElementSize, out stride);
        else if (type == "SetProperty") Memory.TryRead(field + l.SetElementSize, out stride);
        return new UProp(name, type, offset, size, dim, mask, type == "StructProperty" ? inner : 0, element, "", enumAt, key, value, stride);
    }

    static string Pointer(nint at) =>
        at == 0 ? "null" : TryObject(at, out var obj) ? $"{obj.Class?.Name ?? "?"} {obj.Name}" : $"0x{at:X}";

    static string FString(nint data, int count)
    {
        if (count <= 0) return "\"\"";
        var chars = Math.Min(count, MaxStringChars);
        var bytes = new byte[chars * 2];
        if (Memory.Read(data, bytes) != bytes.Length) return "??";
        var text = Encoding.Unicode.GetString(bytes).TrimEnd('\0');
        return count > MaxStringChars ? $"\"{text}...\"" : $"\"{text}\"";
    }

    sealed class Cache(int generation)
    {
        public int Generation { get; } = generation;
        public ConcurrentDictionary<int, string> Names { get; } = new();
        public ConcurrentDictionary<nint, UStructInfo> Structs { get; } = new();
        public ConcurrentDictionary<nint, (long Value, string Name)[]> Enums { get; } = new();
    }
}
