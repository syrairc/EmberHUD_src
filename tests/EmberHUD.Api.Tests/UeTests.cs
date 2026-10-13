using EmberHUD.Testing;

namespace EmberHUD.Api.Tests;

public sealed class UeTests : IDisposable
{
    public void Dispose() => TestMemory.Clear();

    [Fact]
    public void NamesResolveNarrowAndWide()
    {
        var ue = new TestUe();
        var narrow = ue.Name("RootComponent");
        var wide = ue.WideName("Zoné");

        Assert.Equal("RootComponent", Ue.Name(narrow));
        Assert.Equal("Zoné", Ue.Name(wide));
        Assert.Null(Ue.Name(999_999));
    }

    [Fact]
    public void NothingResolvesWhileDetached()
    {
        TestMemory.Clear();

        Assert.Null(Ue.Name(0));
        Assert.Empty(Ue.Objects());
        Assert.False(Ue.TryObject(0x10000, out _));
    }

    [Fact]
    public void ObjectsWalkEveryChunkAndSkipNullAndPendingKill()
    {
        var ue = new TestUe();
        var actor = ue.Class("Actor");
        var objects = Enumerable.Range(0, 6).Select(i => ue.Object($"A{i}", actor)).ToArray();
        ue.Null();
        var dead = ue.Object("Dead", actor, pendingKill: true);

        var all = Ue.Objects().Select(o => o.Address).ToArray();

        Assert.All(objects, o => Assert.Contains(o, all));
        Assert.DoesNotContain(dead, all);
        Assert.DoesNotContain((nint)0, all);
        Assert.Contains(actor, all);
        Assert.Equal(8, all.Length);
    }

    [Fact]
    public void TryObjectAcceptsRealObjectsAndRejectsGarbage()
    {
        var ue = new TestUe();
        var thing = ue.Object("Thing", ue.Class("Actor"));
        var junk = TestMemory.Alloc(0x80);
        for (var at = 0; at < 0x80; at += 8) TestMemory.Write(junk + at, junk);

        Assert.True(Ue.TryObject(thing, out var obj));
        Assert.Equal("Thing", obj.Name);
        Assert.False(Ue.TryObject(junk, out _));
        Assert.False(Ue.TryObject(0, out _));
        Assert.False(Ue.TryObject(0x7FFF_0000, out _));
    }

    [Fact]
    public void TryObjectAcceptsBlueprintGeneratedObjects()
    {
        var ue = new TestUe();
        var bpClassClass = ue.Class("WidgetBlueprintGeneratedClass", ue.Class("BlueprintGeneratedClass", ue.MetaClass));
        var userWidget = ue.Class("UserWidget");
        var host = TestMemory.Alloc(0x80);
        TestMemory.Write(host + TestUe.Layout.ObjectClass, bpClassClass);
        TestMemory.Write(host + TestUe.Layout.ObjectName, ue.Name("UIPageHost_C"));
        TestMemory.Write(host + TestUe.Layout.StructSuper, userWidget);
        var page = ue.Object("UIPageHost_C_1", host);

        Assert.True(Ue.TryObject(page, out var obj));
        Assert.True(obj.IsA("UserWidget"));
    }

    [Fact]
    public void UObjKnowsItsNameClassOuterAndAncestry()
    {
        var ue = new TestUe();
        var root = ue.Class("Object");
        var actor = ue.Class("Actor", root);
        var pawn = ue.Class("Pawn", actor);
        var level = ue.Object("PersistentLevel", ue.Class("Level"));
        var hero = new UObj(ue.Object("Hero", pawn, level));

        Assert.Equal("Hero", hero.Name);
        Assert.Equal("Pawn", hero.Class?.Name);
        Assert.Equal(pawn, hero.ClassAddress);
        Assert.Equal(level, hero.Outer?.Address);
        Assert.Equal("Pawn PersistentLevel.Hero", hero.FullName);
        Assert.True(hero.IsA("Actor"));
        Assert.False(hero.IsA("Level"));
        Assert.False(hero.IsDefault);
        Assert.True(new UObj(ue.Object("Default__Pawn", pawn)).IsDefault);
        Assert.True(hero.Index > 0);
    }

    [Fact]
    public void StructInfoCarriesNameSuperAndSize()
    {
        var ue = new TestUe();
        var actor = ue.Class("Actor", 0, 0x40);
        var info = Ue.Struct(ue.Class("Pawn", actor, 0x60));

        Assert.Equal("Pawn", info?.Name);
        Assert.Equal("Actor", info?.Super?.Name);
        Assert.Equal(0x60, info?.Size);
        Assert.Null(Ue.Struct(0));
    }

    [Fact]
    public void CachesDropWhenMemoryIsReattached()
    {
        var first = new TestUe();
        var nameIndex = first.Name("Old");
        var oldClass = first.Class("Pawn");
        Assert.Equal("Old", Ue.Name(nameIndex));
        Assert.Equal("Pawn", Ue.Struct(oldClass)?.Name);

        var second = new TestUe();
        var newIndex = second.Name("New");
        var newClass = second.Class("Hero");

        Assert.Equal(nameIndex, newIndex);
        Assert.Equal(oldClass, newClass);
        Assert.Equal("New", Ue.Name(newIndex));
        Assert.Equal("Hero", Ue.Struct(newClass)?.Name);
    }

    [Fact]
    public void NameNumberAppendsASuffix()
    {
        var ue = new TestUe();
        var index = ue.Name("Thing");
        var at = TestMemory.Alloc(8);
        TestMemory.Write(at, index);

        Assert.Equal("Thing", Ue.ReadName(at));
        TestMemory.Write(at + 4, 1);
        Assert.Equal("Thing_0", Ue.ReadName(at));
        TestMemory.Write(at + 4, 3);
        Assert.Equal("Thing_2", Ue.ReadName(at));
    }

    [Fact]
    public void AFailedSuperReadIsNotCached()
    {
        var ue = new TestUe();
        var junk = TestMemory.Alloc(0x80);
        TestMemory.Write(junk + TestUe.Layout.ObjectName, 999_999);
        var pawn = ue.Class("Pawn", junk);

        Assert.Null(Ue.Struct(pawn));

        TestMemory.Write(junk + TestUe.Layout.ObjectName, ue.Name("Actor"));

        var info = Ue.Struct(pawn);
        Assert.Equal("Pawn", info?.Name);
        Assert.Equal("Actor", info?.Super?.Name);
    }

    [Fact]
    public void PropertiesIncludeInheritedOnesByOffset()
    {
        var ue = new TestUe();
        var actor = ue.Class("Actor", 0, 0x40, new TestProp("RootComponent", "ObjectProperty", 0x30, 8));
        var pawn = ue.Class("Pawn", actor, 0x60, new TestProp("Controller", "ObjectProperty", 0x48, 8), new TestProp("Speed", "FloatProperty", 0x40));

        var info = Ue.Struct(pawn)!;

        Assert.Equal(new[] { "RootComponent", "Speed", "Controller" }, info.Properties.Select(p => p.Name));
        Assert.Equal(0x40, info.Property("Speed")?.Offset);
        Assert.Equal("FloatProperty", info.Property("Speed")?.Type);
        Assert.Equal(8, info.Property("Controller")?.Size);
        Assert.Null(info.Property("Nope"));
    }

    [Fact]
    public void BoolStructAndArrayPropsCarryTheirExtras()
    {
        var ue = new TestUe();
        var vector = ue.ScriptStruct("Vector", 12, new TestProp("X", "FloatProperty", 0), new TestProp("Y", "FloatProperty", 4), new TestProp("Z", "FloatProperty", 8));
        var cls = ue.Class("Thing", 0, 0x60,
            new TestProp("bHidden", "BoolProperty", 0x30, 1, BoolMask: 0x04),
            new TestProp("Location", "StructProperty", 0x34, 12, Struct: vector),
            new TestProp("Tags", "ArrayProperty", 0x40, 16, Element: new TestProp("Tags", "NameProperty", 0, 8)));

        var info = Ue.Struct(cls)!;

        Assert.Equal(0x04, info.Property("bHidden")!.BoolMask);
        Assert.Equal("Vector", Ue.Struct(info.Property("Location")!.Struct)?.Name);
        Assert.Equal(3, Ue.Struct(vector)!.Properties.Length);
        Assert.Equal("NameProperty", info.Property("Tags")!.Element?.Type);
    }

    [Fact]
    public void FormatShowsEveryKind()
    {
        var ue = new TestUe();
        var target = ue.Object("Target", ue.Class("Actor"));
        var vector = ue.ScriptStruct("Vector", 12);
        var text = ue.FString("hello");
        var bytes = new byte[0x60];
        void Put<T>(int at, T value) where T : unmanaged => System.Runtime.InteropServices.MemoryMarshal.Write(bytes.AsSpan(at), in value);
        Put(0x00, 1.5f);
        Put(0x04, -7);
        Put(0x08, 2.25);
        Put(0x10, (byte)0x05);
        Put(0x11, (byte)200);
        Put(0x18, ue.Name("Hero"));
        Put(0x1C, 3);
        Put(0x20, target);
        Put(0x30, text);
        Put(0x38, 6);
        Put(0x40, (nint)0x1234);
        Put(0x48, 3);
        UProp P(string type, int offset, int size, byte mask = 0, nint structAt = 0) => new("p", type, offset, size, 1, mask, structAt, null);

        Assert.Equal("1.5", Ue.Format(bytes, P("FloatProperty", 0x00, 4)));
        Assert.Equal("-7", Ue.Format(bytes, P("IntProperty", 0x04, 4)));
        Assert.Equal("2.25", Ue.Format(bytes, P("DoubleProperty", 0x08, 8)));
        Assert.Equal("true", Ue.Format(bytes, P("BoolProperty", 0x10, 1, 0x04)));
        Assert.Equal("false", Ue.Format(bytes, P("BoolProperty", 0x10, 1, 0x02)));
        Assert.Equal("200", Ue.Format(bytes, P("ByteProperty", 0x11, 1)));
        Assert.Equal("Hero_2", Ue.Format(bytes, P("NameProperty", 0x18, 8)));
        Assert.Equal("Actor Target", Ue.Format(bytes, P("ObjectProperty", 0x20, 8)));
        Assert.Equal("null", Ue.Format(bytes, P("ObjectProperty", 0x28, 8)));
        Assert.Equal("\"hello\"", Ue.Format(bytes, P("StrProperty", 0x30, 16)));
        Assert.Equal("0x1234", Ue.Format(bytes, P("ObjectProperty", 0x40, 8)));
        Assert.Equal("[3]", Ue.Format(bytes, P("ArrayProperty", 0x40, 16)));
        Assert.Equal("Vector", Ue.Format(bytes, P("StructProperty", 0x00, 12, structAt: vector)));
        Assert.Equal("DelegateProperty", Ue.Format(bytes, P("DelegateProperty", 0x00, 16)));
    }

    [Fact]
    public void FormatOutOfRangeIsUnknown()
    {
        new TestUe();
        var bytes = new byte[0x10];

        Assert.Equal("??", Ue.Format(bytes, new UProp("p", "Int64Property", 0x0C, 8, 1, 0, 0, null)));
        Assert.Equal("??", Ue.Format(bytes, new UProp("p", "IntProperty", -4, 4, 1, 0, 0, null)));
        Assert.Equal("??", Ue.Format(bytes, new UProp("p", "Int64Property", int.MaxValue - 2, 8, 1, 0, 0, null)));
        Assert.Equal("??", Ue.Format(bytes, new UProp("p", "Int64Property", 0x04, int.MaxValue, 1, 0, 0, null)));
        TestMemory.Clear();
        Assert.Equal("??", Ue.Format(bytes, new UProp("p", "IntProperty", 0, 4, 1, 0, 0, null)));
    }

    [Fact]
    public void FormatGarbageOffsetsAndSizesNeverOverflow()
    {
        new TestUe();
        var bytes = new byte[0x400];

        Assert.Equal("??", Ue.Format(bytes, new UProp("p", "IntProperty", 0x7FFFFFF0, 0x100, 1, 0, 0, null)));
        Assert.Equal("??", Ue.Format(bytes, new UProp("p", "IntProperty", 0x500, 0x7FFFFF00, 1, 0, 0, null)));
        Assert.Equal("??", Ue.Format(bytes, new UProp("p", "IntProperty", int.MaxValue, int.MaxValue, 1, 0, 0, null)));
    }

    [Fact]
    public void PropertiesCarryTheirDeclaringClass()
    {
        var ue = new TestUe();
        var actor = ue.Class("Actor", 0, 0x40, new TestProp("Tags", "IntProperty", 0x30));
        var pawn = ue.Class("Pawn", actor, 0x50, new TestProp("Health", "IntProperty", 0x40));

        var info = Ue.Struct(pawn)!;

        Assert.Equal("Actor", info.Property("Tags")!.Owner);
        Assert.Equal("Pawn", info.Property("Health")!.Owner);
    }

    [Fact]
    public void MapAndSetPropertiesKeepKeyValueElementAndStride()
    {
        var ue = new TestUe();
        var cls = ue.Class("Holder", 0, 0x80,
            new TestProp("Parts", "MapProperty", 0x30, 0x50, Key: new TestProp("Parts", "IntProperty", 0, 4), Value: new TestProp("Parts", "ObjectProperty", 8, 8), Stride: 0x18),
            new TestProp("Ids", "SetProperty", 0x60, 0x50, Element: new TestProp("Ids", "IntProperty", 0, 4), Stride: 0x10));

        var info = Ue.Struct(cls)!;
        var parts = info.Property("Parts")!;
        var ids = info.Property("Ids")!;

        Assert.Equal("IntProperty", parts.Key!.Type);
        Assert.Equal(8, parts.Value!.Offset);
        Assert.Equal(0x18, parts.Stride);
        Assert.Equal("IntProperty", ids.Element!.Type);
        Assert.Equal(0x10, ids.Stride);
    }

    [Fact]
    public void EnumValuesShowTheirNameAndFallBackToTheNumber()
    {
        var ue = new TestUe();
        var rarity = ue.Enum("ERarity", ("Normal", 0), ("Magic", 1), ("Rare", 3));
        var cls = ue.Class("Thing", 0, 0x40,
            new TestProp("Rarity", "EnumProperty", 0x30, 1, Enum: rarity),
            new TestProp("Tier", "ByteProperty", 0x31, 1, Enum: rarity),
            new TestProp("Plain", "ByteProperty", 0x32, 1));
        var info = Ue.Struct(cls)!;
        var bytes = new byte[0x40];
        bytes[0x30] = 3;
        bytes[0x31] = 9;
        bytes[0x32] = 3;

        Assert.Equal("Rare (3)", Ue.Format(bytes, info.Property("Rarity")!));
        Assert.Equal("9", Ue.Format(bytes, info.Property("Tier")!));
        Assert.Equal("3", Ue.Format(bytes, info.Property("Plain")!));
        Assert.Equal("Magic", Ue.EnumName(rarity, 1));
        Assert.Null(Ue.EnumName(0, 1));
    }

    [Fact]
    public void AnEnumWithAGarbageNameCountResolvesNothing()
    {
        var ue = new TestUe();
        var broken = ue.Enum("EBroken", ("A", 0));
        TestMemory.Write(broken + TestUe.Layout.EnumNames + TestUe.Layout.ArrayNum, 1 << 30);

        Assert.Null(Ue.EnumName(broken, 0));
    }

    static UProp IntMap(int stride = 0x18) => new("Map", "MapProperty", 0, 0x50, 1, 0, 0, null, Key: new UProp("Map", "IntProperty", 0, 4, 1, 0, 0, null), Value: new UProp("Map", "IntProperty", 8, 4, 1, 0, 0, null), Stride: stride);

    static (int Key, int Value)[] Pairs(UEntry[] entries) =>
        [.. entries.Select(e => (Memory.TryRead(e.Address + e.Key.Offset, out int k) ? k : -1, Memory.TryRead(e.Address + e.Value!.Offset, out int v) ? v : -1))];

    [Fact]
    public void MapEntriesSkipFreeSlots()
    {
        var ue = new TestUe();
        var map = ue.SparseArray(5, 0x18, [1, 3], (element, slot) =>
        {
            TestMemory.Write(element, 100 + slot);
            TestMemory.Write(element + 8, 200 + slot);
        });

        Assert.Equal([(100, 200), (102, 202), (104, 204)], Pairs(Ue.Entries(map, IntMap(), 100)));
    }

    [Fact]
    public void MapEntriesReadTheSecondaryBitArrayAndHonourMax()
    {
        var ue = new TestUe();
        int[] free = [1, 9, 31, 32, 150];
        var map = ue.SparseArray(200, 0x18, free, (element, slot) =>
        {
            TestMemory.Write(element, slot);
            TestMemory.Write(element + 8, slot * 2);
        });

        var all = Pairs(Ue.Entries(map, IntMap(), 1000));
        var some = Pairs(Ue.Entries(map, IntMap(), 7));

        Assert.Equal(Enumerable.Range(0, 200).Except(free), all.Select(p => p.Key));
        Assert.Equal((199, 398), all[^1]);
        Assert.Equal([0, 2, 3, 4, 5, 6, 7], some.Select(p => p.Key));
    }

    [Fact]
    public void SetEntriesUseTheElementProp()
    {
        var ue = new TestUe();
        var set = ue.SparseArray(3, 0x10, [], (element, slot) => TestMemory.Write(element, 7 + slot));
        var prop = new UProp("Ids", "SetProperty", 0, 0x50, 1, 0, 0, new UProp("Ids", "IntProperty", 0, 4, 1, 0, 0, null), Stride: 0x10);

        var entries = Ue.Entries(set, prop, 10);

        Assert.Equal(3, entries.Length);
        Assert.All(entries, e => Assert.Null(e.Value));
        Assert.True(Memory.TryRead(entries[2].Address, out int last));
        Assert.Equal(9, last);
    }

    [Fact]
    public void GarbageMapsGiveNoEntries()
    {
        var ue = new TestUe();
        var map = ue.SparseArray(4, 0x18, [], (_, _) => { });

        TestMemory.Write(map + TestUe.Layout.ArrayNum, 1 << 30);
        Assert.Empty(Ue.Entries(map, IntMap(), 100));

        TestMemory.Write(map + TestUe.Layout.ArrayNum, 4);
        Assert.Empty(Ue.Entries(map, IntMap(0), 100));
        Assert.Empty(Ue.Entries(map, IntMap(), 0));
        Assert.Empty(Ue.Entries(0x10, IntMap(), 100));

        TestMemory.Write(map + TestUe.Layout.ArrayNum, 200);
        TestMemory.Write(map + TestUe.Layout.SparseNumBits, 200);
        Assert.Empty(Ue.Entries(map, IntMap(), 100));
        Assert.Empty(Ue.Entries(map, new UProp("x", "IntProperty", 0, 4, 1, 0, 0, null), 100));
    }

    [Fact]
    public void WeakPointersResolveOnlyWhileTheSerialMatches()
    {
        var ue = new TestUe();
        var actor = ue.Class("Actor", 0, 0x40, new TestProp("Parent", "WeakObjectProperty", 0x30, 8));
        var hero = ue.Object("Hero", actor);
        var info = Ue.Struct(actor)!;
        var bytes = new byte[0x40];
        var (index, serial) = ue.Weak(hero);

        BitConverter.GetBytes(index).CopyTo(bytes, 0x30);
        BitConverter.GetBytes(serial).CopyTo(bytes, 0x34);
        Assert.Equal("Actor Hero", Ue.Format(bytes, info.Property("Parent")!));

        BitConverter.GetBytes(serial + 1).CopyTo(bytes, 0x34);
        Assert.Equal("stale", Ue.Format(bytes, info.Property("Parent")!));

        BitConverter.GetBytes(-1).CopyTo(bytes, 0x30);
        Assert.Equal("null", Ue.Format(bytes, info.Property("Parent")!));

        BitConverter.GetBytes(1_000_000).CopyTo(bytes, 0x30);
        Assert.Equal("stale", Ue.Format(bytes, info.Property("Parent")!));
        Assert.False(Ue.TryWeak(-5, 1, out _));

        BitConverter.GetBytes(0L).CopyTo(bytes, 0x30);
        Assert.Equal("null", Ue.Format(bytes, info.Property("Parent")!));
        Assert.False(Ue.TryWeak(0, 0, out _));
    }

    [Fact]
    public void SoftPointersShowTheirAssetPath()
    {
        var ue = new TestUe();
        var cls = ue.Class("Holder", 0, 0x60, new TestProp("Icon", "SoftObjectProperty", 0x30, 0x28));
        var info = Ue.Struct(cls)!;
        var bytes = new byte[0x60];

        Assert.Equal("null", Ue.Format(bytes, info.Property("Icon")!));
        BitConverter.GetBytes(ue.Name("/Game/Icons/Boss")).CopyTo(bytes, 0x30 + TestUe.Layout.SoftPathName);
        Assert.Equal("/Game/Icons/Boss", Ue.Format(bytes, info.Property("Icon")!));

        var hero = ue.Object("Hero", cls);
        var (index, serial) = ue.Weak(hero);
        BitConverter.GetBytes(index).CopyTo(bytes, 0x30);
        BitConverter.GetBytes(serial).CopyTo(bytes, 0x30 + TestUe.Layout.WeakSerial);
        Assert.Equal("Holder Hero", Ue.Format(bytes, info.Property("Icon")!));

        BitConverter.GetBytes(serial + 1).CopyTo(bytes, 0x30 + TestUe.Layout.WeakSerial);
        Assert.Equal("/Game/Icons/Boss", Ue.Format(bytes, info.Property("Icon")!));

        BitConverter.GetBytes(4000).CopyTo(bytes, 0x30 + TestUe.Layout.SoftPathName);
        Assert.Equal("??", Ue.Format(bytes, info.Property("Icon")!));
    }

    [Fact]
    public void LazyPointersFormatLikeWeakOnes()
    {
        var ue = new TestUe();
        var cls = ue.Class("Holder", 0, 0x40, new TestProp("Lazy", "LazyObjectProperty", 0x30, 0x1C));
        var hero = ue.Object("Hero", cls);
        var info = Ue.Struct(cls)!;
        var bytes = new byte[0x50];

        Assert.Equal("null", Ue.Format(bytes, info.Property("Lazy")!));
        var (index, serial) = ue.Weak(hero);
        BitConverter.GetBytes(index).CopyTo(bytes, 0x30);
        BitConverter.GetBytes(serial).CopyTo(bytes, 0x30 + TestUe.Layout.WeakSerial);
        Assert.Equal("Holder Hero", Ue.Format(bytes, info.Property("Lazy")!));
    }

    [Fact]
    public void TheDefaultObjectComesFromTheClass()
    {
        var ue = new TestUe();
        var actor = ue.Class("Actor");
        var info = Ue.Struct(actor)!;
        Assert.Equal((nint)0, Ue.DefaultObject(info));

        var cdo = ue.DefaultFor(actor);

        Assert.Equal(cdo, Ue.DefaultObject(info));
        TestMemory.Write(actor + TestUe.Layout.ClassDefaultObject, (nint)0x10);
        Assert.Equal((nint)0, Ue.DefaultObject(info));
    }
}
