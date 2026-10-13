using System.Text;

namespace EmberHUD.Testing;

public sealed record TestProp(string Name, string Type, int Offset, int Size = 4, byte BoolMask = 0, nint Struct = 0, TestProp? Element = null, nint Enum = 0, TestProp? Key = null, TestProp? Value = null, int Stride = 0);

public sealed class TestUe
{
    internal static readonly UeLayout Layout = new(
        ObjectIndex: 0x08, ObjectClass: 0x18, ObjectName: 0x20, ObjectOuter: 0x28,
        StructSuper: 0x48, StructChildProperties: 0x58, StructSize: 0x60,
        FieldClass: 0x10, FieldNext: 0x18, FieldName: 0x20, FieldClassName: 0x00,
        PropertyArrayDim: 0x30, PropertyElementSize: 0x34, PropertyOffset: 0x40, PropertyInner: 0x48, BoolByteOffset: 0x50, BoolFieldMask: 0x51,
        ObjectsNum: 0x10, ObjectsPerChunk: 4, ObjectItemSize: 0x10, ObjectItemFlags: 0x08, PendingKillFlag: 0x4, MaxObjectChunks: 64,
        NameBlocks: 0x08, NameBlockSize: 4096, NameEntryStride: 2, NameLengthShift: 6, MaxNameBlocks: 4, MaxNameLength: 128,
        ArrayNum: 8, ClassClassName: "Class", DefaultPrefix: "Default__",
        ClassDefaultObject: 0x68, EnumNames: 0x50, EnumNameStride: 16, EnumPropertyEnum: 0x58, BytePropertyEnum: 0x60,
        MapPropertyValue: 0x68, MapElementSize: 0x70, SetElementSize: 0x78,
        SparseFlagsInline: 0x10, SparseFlagsSecondary: 0x20, SparseNumBits: 0x28, SparseInlineBits: 128,
        ObjectItemSerial: 0x0C, WeakSerial: 4, SoftPathName: 0x10);

    readonly nint _nameBlock;
    readonly nint _array;
    readonly nint _chunkTable;
    readonly Dictionary<string, int> _names = [];
    readonly Dictionary<string, nint> _fieldClasses = [];
    nint _scriptStructClass;
    int _nameCursor;
    int _count;

    public TestUe()
    {
        TestMemory.Clear();
        var pool = TestMemory.Alloc(Layout.NameBlocks + nint.Size * Layout.MaxNameBlocks);
        _nameBlock = TestMemory.Alloc(Layout.NameBlockSize * Layout.NameEntryStride);
        TestMemory.Write(pool + Layout.NameBlocks, _nameBlock);
        _array = TestMemory.Alloc(0x20);
        _chunkTable = TestMemory.Alloc(nint.Size * Layout.MaxObjectChunks);
        TestMemory.Write(_array, _chunkTable);
        TestMemory.Use(Layout, pool, _array);
        Name("None");
        MetaClass = Struct(Layout.ClassClassName, 0, 0, 0x80, []);
        TestMemory.Write(MetaClass + Layout.ObjectClass, MetaClass);
    }

    public nint MetaClass { get; }

    public nint SparseArray(int slots, int stride, int[] free, Action<nint, int> fill)
    {
        var at = TestMemory.Alloc(0x50);
        var data = TestMemory.Alloc(Math.Max(1, slots) * stride);
        var words = new int[Math.Max(1, (slots + 31) / 32)];
        for (var slot = 0; slot < slots; slot++)
        {
            if (free.Contains(slot)) continue;
            fill(data + slot * stride, slot);
            words[slot / 32] |= 1 << slot % 32;
        }
        var inline = slots <= Layout.SparseInlineBits;
        var flags = inline ? at + Layout.SparseFlagsInline : TestMemory.Alloc(words.Length * 4);
        for (var i = 0; i < words.Length; i++) TestMemory.Write(flags + i * 4, words[i]);
        if (!inline) TestMemory.Write(at + Layout.SparseFlagsSecondary, flags);
        TestMemory.Write(at, data);
        TestMemory.Write(at + Layout.ArrayNum, slots);
        TestMemory.Write(at + Layout.SparseNumBits, slots);
        return at;
    }

    public int Name(string text) => AddName(text, false);

    public int WideName(string text) => AddName(text, true);

    public nint Class(string name, nint super = 0, int size = 0x40, params TestProp[] props) => Struct(name, MetaClass, super, size, props);

    public nint BlueprintClass(string name, nint generatedClass, nint super) => Struct(name, generatedClass, super, 0x40, []);

    public nint ScriptStruct(string name, int size, params TestProp[] props)
    {
        if (_scriptStructClass == 0) _scriptStructClass = Class("ScriptStruct");
        return Struct(name, _scriptStructClass, 0, size, props);
    }

    public nint Object(string name, nint cls, nint outer = 0, bool pendingKill = false)
    {
        Memory.TryRead(cls + Layout.StructSize, out int size);
        var at = TestMemory.Alloc(Math.Clamp(size, 0x30, 0x1000));
        Header(at, name, cls, outer);
        Register(at, pendingKill);
        return at;
    }

    nint _enumClass;

    public nint Enum(string name, params (string Name, long Value)[] values)
    {
        if (_enumClass == 0) _enumClass = Class("Enum");
        var at = TestMemory.Alloc(0x80);
        Header(at, name, _enumClass, 0);
        var names = TestMemory.Alloc(Math.Max(16, values.Length * Layout.EnumNameStride));
        for (var i = 0; i < values.Length; i++)
        {
            TestMemory.Write(names + i * Layout.EnumNameStride, Name($"{name}::{values[i].Name}"));
            TestMemory.Write(names + i * Layout.EnumNameStride + 8, values[i].Value);
        }
        TestMemory.Write(at + Layout.EnumNames, names);
        TestMemory.Write(at + Layout.EnumNames + Layout.ArrayNum, values.Length);
        Register(at, false);
        return at;
    }

    public (int Index, int Serial) Weak(nint obj)
    {
        Memory.TryRead(obj + Layout.ObjectIndex, out int index);
        return (index, index + 1000);
    }

    public nint DefaultFor(nint cls)
    {
        var name = Ue.ReadName(cls + Layout.ObjectName) ?? "Thing";
        var cdo = Object($"{Layout.DefaultPrefix}{name}", cls);
        TestMemory.Write(cls + Layout.ClassDefaultObject, cdo);
        return cdo;
    }

    public void Null() => Register(0, false);

    public nint FString(string text)
    {
        var bytes = Encoding.Unicode.GetBytes(text + "\0");
        var at = TestMemory.Alloc(bytes.Length);
        TestMemory.WriteBytes(at, bytes);
        return at;
    }

    int AddName(string text, bool wide)
    {
        if (_names.TryGetValue(text, out var known)) return known;
        var bytes = wide ? Encoding.Unicode.GetBytes(text) : Encoding.Latin1.GetBytes(text);
        var index = _nameCursor;
        var entry = _nameBlock + index * Layout.NameEntryStride;
        TestMemory.Write(entry, (ushort)((text.Length << Layout.NameLengthShift) | (wide ? 1 : 0)));
        TestMemory.WriteBytes(entry + sizeof(ushort), bytes);
        _nameCursor += (sizeof(ushort) + bytes.Length + Layout.NameEntryStride - 1) / Layout.NameEntryStride;
        return _names[text] = index;
    }

    nint Struct(string name, nint cls, nint super, int size, TestProp[] props)
    {
        var at = TestMemory.Alloc(0x80);
        Header(at, name, cls, 0);
        TestMemory.Write(at + Layout.StructSuper, super);
        TestMemory.Write(at + Layout.StructSize, size);
        nint first = 0;
        nint previous = 0;
        foreach (var prop in props)
        {
            var field = Field(prop);
            if (previous == 0) first = field;
            else TestMemory.Write(previous + Layout.FieldNext, field);
            previous = field;
        }
        TestMemory.Write(at + Layout.StructChildProperties, first);
        Register(at, false);
        return at;
    }

    nint Field(TestProp prop)
    {
        var field = TestMemory.Alloc(0x80);
        if (!_fieldClasses.TryGetValue(prop.Type, out var fieldClass))
        {
            fieldClass = _fieldClasses[prop.Type] = TestMemory.Alloc(0x10);
            TestMemory.Write(fieldClass + Layout.FieldClassName, Name(prop.Type));
        }
        TestMemory.Write(field + Layout.FieldClass, fieldClass);
        TestMemory.Write(field + Layout.FieldName, Name(prop.Name));
        TestMemory.Write(field + Layout.PropertyArrayDim, 1);
        TestMemory.Write(field + Layout.PropertyElementSize, prop.Size);
        TestMemory.Write(field + Layout.PropertyOffset, prop.Offset);
        if (prop.Struct != 0) TestMemory.Write(field + Layout.PropertyInner, prop.Struct);
        if (prop.Element is { } element) TestMemory.Write(field + Layout.PropertyInner, Field(element));
        if (prop.Key is { } key) TestMemory.Write(field + Layout.PropertyInner, Field(key));
        if (prop.Value is { } value) TestMemory.Write(field + Layout.MapPropertyValue, Field(value));
        if (prop.Enum != 0) TestMemory.Write(field + (prop.Type == "EnumProperty" ? Layout.EnumPropertyEnum : Layout.BytePropertyEnum), prop.Enum);
        if (prop.Stride != 0) TestMemory.Write(field + (prop.Type == "MapProperty" ? Layout.MapElementSize : Layout.SetElementSize), prop.Stride);
        if (prop.BoolMask != 0) TestMemory.Write(field + Layout.BoolFieldMask, prop.BoolMask);
        return field;
    }

    void Header(nint at, string name, nint cls, nint outer)
    {
        TestMemory.Write(at + Layout.ObjectIndex, _count);
        TestMemory.Write(at + Layout.ObjectClass, cls);
        TestMemory.Write(at + Layout.ObjectName, Name(name));
        TestMemory.Write(at + Layout.ObjectOuter, outer);
    }

    void Register(nint obj, bool pendingKill)
    {
        var slot = _chunkTable + _count / Layout.ObjectsPerChunk * nint.Size;
        if (_count % Layout.ObjectsPerChunk == 0) TestMemory.Write(slot, TestMemory.Alloc(Layout.ObjectsPerChunk * Layout.ObjectItemSize));
        Memory.TryRead(slot, out nint items);
        var item = items + _count % Layout.ObjectsPerChunk * Layout.ObjectItemSize;
        TestMemory.Write(item, obj);
        TestMemory.Write(item + Layout.ObjectItemSerial, _count + 1000);
        TestMemory.Write(item + Layout.ObjectItemFlags, pendingKill ? Layout.PendingKillFlag : 0);
        _count++;
        TestMemory.Write(_array + Layout.ObjectsNum, _count);
    }
}
