using System.Collections.Concurrent;
using System.Globalization;
using System.Text;

namespace Aember;

public sealed record UProp(string Name, string Type, int Offset, int Size, int ArrayDim, byte BoolMask, nint Struct, UProp? Element);

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

public static class Ue
{
    const int MaxSuperDepth = 64;
    const int MaxFields = 4096;
    const int MaxStringChars = 512;
    static readonly CultureInfo Invariant = CultureInfo.InvariantCulture;
    static readonly Lock Gate = new();
    static volatile Cache _cache = new(-1);

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
               Memory.TryRead(cls + l.ObjectClass, out nint meta) && meta != 0 &&
               ReadName(meta + l.ObjectName, cache) == l.ClassClassName &&
               ReadName(at + l.ObjectName, cache) is not null;
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
            "ByteProperty" => v[0].ToString(Invariant),
            "Int8Property" => ((sbyte)v[0]).ToString(Invariant),
            "Int16Property" => BitConverter.ToInt16(v).ToString(Invariant),
            "UInt16Property" => BitConverter.ToUInt16(v).ToString(Invariant),
            "IntProperty" => BitConverter.ToInt32(v).ToString(Invariant),
            "UInt32Property" => BitConverter.ToUInt32(v).ToString(Invariant),
            "Int64Property" => BitConverter.ToInt64(v).ToString(Invariant),
            "UInt64Property" => BitConverter.ToUInt64(v).ToString(Invariant),
            "FloatProperty" => BitConverter.ToSingle(v).ToString("0.###", Invariant),
            "DoubleProperty" => BitConverter.ToDouble(v).ToString("0.###", Invariant),
            "EnumProperty" => (prop.Size switch { 1 => (long)v[0], 2 => BitConverter.ToInt16(v), 4 => BitConverter.ToInt32(v), _ => BitConverter.ToInt64(v) }).ToString(Invariant),
            "NameProperty" => NameOf(BitConverter.ToInt32(v), BitConverter.ToInt32(v[4..]), Current()) ?? "??",
            "StrProperty" => FString((nint)BitConverter.ToInt64(v), BitConverter.ToInt32(v[l.ArrayNum..])),
            "ObjectProperty" or "ClassProperty" or "SoftObjectProperty" or "LazyObjectProperty" => Pointer((nint)BitConverter.ToInt64(v)),
            "ArrayProperty" or "MapProperty" or "SetProperty" => $"[{BitConverter.ToInt32(v[l.ArrayNum..])}]",
            "StructProperty" => Struct(prop.Struct)?.Name ?? "struct",
            _ => prop.Type,
        };
    }

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
            if (Prop(l, field, 0, cache) is { } prop) own.Add(prop);
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
        var element = type == "ArrayProperty" && inner != 0 ? Prop(l, inner, depth + 1, cache) : null;
        return new UProp(name, type, offset, size, dim, mask, type == "StructProperty" ? inner : 0, element);
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
    }
}
