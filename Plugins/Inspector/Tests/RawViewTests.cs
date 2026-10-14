using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using EmberHUD;
using EmberHUD.Testing;

namespace Inspector;

public sealed class RawViewTests
{
    [Test]
    public void AUObjectAndAHexBlockDrawFullyOpen()
    {
        var (_, hero, _, _) = Samples.ActorWorld();
        var logic = TestMemory.Alloc(0x200);
        TestMemory.Write(logic + 8, hero);
        TestMemory.Write(logic + 16, 1.5f);
        var raw = new RawView(new Snapshots(() => 0), Samples.NoPin) { OpenAll = true };

        Samples.Frame(() =>
        {
            raw.Node("object", hero, 0);
            raw.Node("hex", logic, 0);
        });
    }

    [Test]
    public void CyclesAndGarbageSizesStayBounded()
    {
        var ue = new TestUe();
        var huge = ue.Class("Huge", 0, int.MaxValue, new TestProp("Items", "ArrayProperty", 0x30, 16, Element: new TestProp("Items", "IntProperty", 0, 4)));
        var obj = ue.Object("Garbage", ue.Class("Small", 0, 0x40, new TestProp("Items", "ArrayProperty", 0x30, 16, Element: new TestProp("Items", "IntProperty", 0, 4))));
        TestMemory.Write(obj + 0x30, obj);
        TestMemory.Write(obj + 0x38, 1 << 30);
        var loop = TestMemory.Alloc(0x200);
        TestMemory.Write(loop, loop);
        var raw = new RawView(new Snapshots(() => 0), Samples.NoPin) { OpenAll = true };

        Samples.Frame(() =>
        {
            raw.Node("huge", ue.Object("Big", huge), 0);
            raw.Node("garbage", obj, 0);
            raw.Node("loop", loop, 0);
        });
    }

    [Test]
    public void GarbagePropertyOffsetsAndSizesDrawAsUnknown()
    {
        var ue = new TestUe();
        var cls = ue.Class("Odd", 0, 0x40,
            new TestProp("Far", "IntProperty", 0x7FFFFFF0, 0x100),
            new TestProp("Wide", "IntProperty", 0x500, 0x7FFFFF00),
            new TestProp("Items", "ArrayProperty", 0x7FFFFFF8, 16, Element: new TestProp("Items", "IntProperty", 0, 4)),
            new TestProp("Ptr", "ObjectProperty", 0x7FFFFFFC, 8));
        var obj = ue.Object("Odd", cls);
        var raw = new RawView(new Snapshots(() => 0), Samples.NoPin) { OpenAll = true };

        Samples.Frame(() => raw.Node("odd", obj, 0));
    }

    [Test]
    public void GameStringsWithFormatSpecifiersDrawWithoutCrashing()
    {
        var ue = new TestUe();
        var cls = ue.Class("Bad%s%n%d", 0, 0x40, new TestProp("P%s%n", "Odd%s%n%dProperty", 0x30, 4));
        var obj = ue.Object("Obj%s%n%d", cls);
        var raw = new RawView(new Snapshots(() => 0), Samples.NoPin) { OpenAll = true };

        Samples.Frame(() => raw.Node("fmt", obj, 0));
    }

    [Test]
    public void RawSectionsSayNotAttached()
    {
        TestMemory.Clear();
        var raw = new RawView(new Snapshots(() => 0), Samples.NoPin);

        Samples.Frame(() =>
        {
            raw.Globals();
            raw.Address();
            raw.Node("detached", 0x10000, 0);
        });
    }

    [Test]
    public void GlobalsDraw()
    {
        var (_, hero, _, _) = Samples.ActorWorld();
        TestMemory.Global("FightMgr", hero);
        var raw = new RawView(new Snapshots(() => 0), Samples.NoPin) { OpenAll = true };

        Samples.Frame(raw.Globals);
    }

    [Test]
    public void AGarbageElementSizeDrawsAsUnknown()
    {
        var ue = new TestUe();
        var cls = ue.Class("Odd", 0, 0x40, new TestProp("Items", "ArrayProperty", 0x30, 16, Element: new TestProp("Items", "IntProperty", 0, int.MaxValue / 2)));
        var obj = ue.Object("Odd", cls);
        var data = TestMemory.Alloc(0x40);
        TestMemory.Write(obj + 0x30, data);
        TestMemory.Write(obj + 0x38, 200);
        var raw = new RawView(new Snapshots(() => 0), Samples.NoPin) { OpenAll = true };

        Samples.Frame(() => raw.Node("odd", obj, 0));
    }

    [Test]
    public void HexRowsFormatEightBytesAndAreBuiltOncePerSnapshot()
    {
        var at = TestMemory.Alloc(0x20);
        TestMemory.Write(at, 0x0000000200000001L);
        var raw = new RawView(new Snapshots(() => 0), Samples.NoPin);
        var bytes = new byte[0x20];
        Memory.Read(at, bytes);

        Samples.Frame(() =>
        {
            var rows = raw.RowsOf(bytes);
            Check.Equal("+0,+8,+10,+18", string.Join(",", rows.Select(row => row.Offset)));
            Check.That(rows[0].Text.StartsWith("01 00 00 00 02 00 00 00   1 2   0 0   "), rows[0].Text);
            Check.That(ReferenceEquals(rows, raw.RowsOf(bytes)), "same snapshot, same rows");
            Check.That(!ReferenceEquals(rows, raw.RowsOf((byte[])bytes.Clone())), "a new snapshot rebuilds");
        });
    }

    [Test]
    public void PointerLabelsComeFromTheCacheNotFromMemoryOnTheNextFrame()
    {
        var (_, hero, _, _) = Samples.ActorWorld();
        var block = TestMemory.Alloc(0x10);
        TestMemory.Write(block, hero);
        var bytes = new byte[0x10];
        Memory.Read(block, bytes);
        var raw = new RawView(new Snapshots(() => 0), Samples.NoPin);
        HexRow[] first = [];

        Samples.Frame(() => first = raw.RowsOf(bytes));
        Check.That(first[0].Target is { } label && label.StartsWith("->") && label.Contains("Hero"), first[0].Target);

        TestMemory.Clear();
        Samples.Frame(() => Check.That(ReferenceEquals(first, raw.RowsOf(bytes)), "no rebuild, so no memory read"));
    }

    static (nint Monster, nint Child) World(bool withDefault = true)
    {
        var ue = new TestUe();
        var rarity = ue.Enum("ERarity", ("Normal", 0), ("Rare", 3));
        var vector = ue.ScriptStruct("Vector", 12, new TestProp("X", "FloatProperty", 0), new TestProp("Y", "FloatProperty", 4), new TestProp("Z", "FloatProperty", 8));
        var part = ue.Class("Part", 0, 0x40, new TestProp("Id", "IntProperty", 0x30));
        var actor = ue.Class("Actor", 0, 0x50, new TestProp("bHidden", "BoolProperty", 0x30, 1, BoolMask: 1), new TestProp("OnDestroyed", "MulticastSparseDelegateProperty", 0x31, 1));
        var monster = ue.Class("EMonster", actor, 0xC0,
            new TestProp("Rarity", "EnumProperty", 0x50, 1, Enum: rarity),
            new TestProp("Spot", "StructProperty", 0x54, 12, Struct: vector),
            new TestProp("Owner", "ObjectProperty", 0x60, 8),
            new TestProp("Parts", "MapProperty", 0x68, 0x50, Key: new TestProp("Parts", "IntProperty", 0, 4), Value: new TestProp("Parts", "ObjectProperty", 8, 8), Stride: 0x18));
        if (withDefault) ue.DefaultFor(monster);
        var child = ue.Object("Child", part);
        var hero = ue.Object("Hero", monster);
        TestMemory.Write(hero + 0x50, (byte)3);
        TestMemory.Write(hero + 0x54, new Vector3(1, 2, 3));
        TestMemory.Write(hero + 0x60, child);
        var map = ue.SparseArray(2, 0x18, [], (element, slot) =>
        {
            TestMemory.Write(element, slot);
            TestMemory.Write(element + 8, child);
        });
        var header = new byte[0x50];
        Memory.Read(map, header);
        TestMemory.WriteBytes(hero + 0x68, header);
        return (hero, child);
    }

    static void Draw(RawView view, nint address, bool link)
    {
        if (Ue.TryObject(address, out var obj) && obj.Class is { } cls) view.Object(address, obj, cls, 0, link);
    }

    [Test]
    public void AnObjectDrawsGroupedWithEnumsStructsAndMapEntries()
    {
        var (hero, _) = World();
        var raw = new RawView(new Snapshots(() => 0), Samples.NoPin);
        raw.OpenAll = true;

        Samples.Frame(() => Draw(raw, hero, false));
        Samples.Frame(() => Draw(raw, hero, true));
    }

    [Test]
    public void HideDefaultsAndSearchDrawWithoutThrowing()
    {
        var (hero, _) = World();
        var raw = new RawView(new Snapshots(() => 0), Samples.NoPin);
        raw.HideDefaults = true;
        raw.OpenAll = true;

        Samples.Frame(() =>
        {
            raw.Toolbar();
            Draw(raw, hero, true);
        });
        raw.Search = "rare";
        Samples.Frame(() => Draw(raw, hero, true));
        raw.Search = "2";
        Samples.Frame(() => Draw(raw, hero, true));
        Samples.Frame(() => Draw(raw, hero, false));
        raw.Search = "no such thing";
        Samples.Frame(() => Draw(raw, hero, true));
    }

    [Test]
    public void AMissingDefaultObjectStillShowsEverything()
    {
        var (hero, _) = World(withDefault: false);
        var raw = new RawView(new Snapshots(() => 0), Samples.NoPin);
        raw.HideDefaults = true;

        Samples.Frame(() => Draw(raw, hero, true));
    }

    [Test]
    public void LinkModeNeverNavigatesByItself()
    {
        var (hero, child) = World();
        nint went = 0;
        var raw = new RawView(new Snapshots(() => 0), Samples.NoPin);
        raw.Navigate = at => went = at;
        raw.OpenAll = true;

        Samples.Frame(() => Draw(raw, hero, true));
        Check.Equal((nint)0, went);
        Check.That(Ue.TryObject(child, out _), "Owner points at a live object, so the row drew as a link");
    }

    [Test]
    public void GroupsAreBuiltOncePerSnapshotAndRebuiltWhenTheInputsChange()
    {
        var (hero, _) = World();
        Ue.TryObject(hero, out var obj);
        var cls = obj.Class!;
        var bytes = new byte[cls.Size];
        Memory.Read(hero, bytes);
        var view = new RawView(new Snapshots(() => 0), Samples.NoPin);

        Samples.Frame(() =>
        {
            var first = view.Groups(cls, bytes, null);
            Check.That(ReferenceEquals(first, view.Groups(cls, bytes, null)), "same inputs, same groups");

            view.Search = "rare";
            var searched = view.Groups(cls, bytes, null);
            Check.That(!ReferenceEquals(first, searched), "search changed");

            view.Search = "";
            view.HideDefaults = true;
            var hiding = view.Groups(cls, bytes, null);
            Check.That(!ReferenceEquals(searched, hiding), "hide defaults changed");

            view.HideDefaults = false;
            Check.That(!ReferenceEquals(hiding, view.Groups(cls, (byte[])bytes.Clone(), null)), "a new snapshot");
        });
    }

    [Test]
    public void FormattedValuesComeFromTheCacheEvenWhenMemoryChanges()
    {
        var (hero, _) = World();
        Ue.TryObject(hero, out var obj);
        var cls = obj.Class!;
        var bytes = new byte[cls.Size];
        Memory.Read(hero, bytes);
        var view = new RawView(new Snapshots(() => 0), Samples.NoPin);
        List<PropGroup> first = [];

        Samples.Frame(() => first = view.Groups(cls, bytes, null));
        var owner = first.SelectMany(group => group.Rows).Single(row => row.Prop.Name == "Owner").Value;
        Check.That(owner != "??", owner);

        TestMemory.Clear();
        Samples.Frame(() =>
        {
            var again = view.Groups(cls, bytes, null);
            Check.That(ReferenceEquals(first, again), "no rebuild");
            Check.Equal(owner, again.SelectMany(group => group.Rows).Single(row => row.Prop.Name == "Owner").Value);
        });
    }

    static (UStructInfo Info, byte[] Bytes, byte[] Defaults) Thing()
    {
        var ue = new TestUe();
        var vector = ue.ScriptStruct("Vector", 12, new TestProp("X", "FloatProperty", 0), new TestProp("Y", "FloatProperty", 4), new TestProp("Z", "FloatProperty", 8));
        var actor = ue.Class("Actor", 0, 0x50,
            new TestProp("bHidden", "BoolProperty", 0x30, 1, BoolMask: 1),
            new TestProp("bLocked", "BoolProperty", 0x30, 1, BoolMask: 2),
            new TestProp("OnDestroyed", "MulticastSparseDelegateProperty", 0x31, 1));
        var monster = ue.Class("EMonster", actor, 0x70,
            new TestProp("Level", "IntProperty", 0x54),
            new TestProp("Spot", "StructProperty", 0x58, 12, Struct: vector));
        var bytes = new byte[0x70];
        var defaults = new byte[0x70];
        bytes[0x30] = 1;
        defaults[0x30] = 1;
        bytes[0x54] = 5;
        System.BitConverter.GetBytes(7.5f).CopyTo(bytes, 0x5C);
        return (Ue.Struct(monster)!, bytes, defaults);
    }

    [Test]
    public void GroupRowsCarryTheirOffsetLabelAndHeader()
    {
        var (info, bytes, _) = Thing();

        var groups = RawView.Group(info, bytes, null, false, "");

        Check.Equal("EMonster  (2)###groupEMonster", groups[0].Header);
        Check.Equal("Level", groups[0].Rows[0].Label);
        Check.Equal("+54", groups[0].Rows[0].Offset);
        Check.Equal("+58", groups[0].Rows[1].Offset);
        Check.Equal(0, (long)RawView.LinkTarget(groups[0].Rows[0], true));
    }

    [Test]
    public void AnObjectRowLinksOnlyWhileItsTargetIsAliveAndLinkIsOn()
    {
        var (hero, child) = World();
        Ue.TryObject(hero, out var obj);
        var cls = obj.Class!;
        var bytes = new byte[cls.Size];
        Memory.Read(hero, bytes);

        var owner = RawView.Group(cls, bytes, null, false, "").SelectMany(group => group.Rows).Single(row => row.Prop.Name == "Owner");

        Check.Equal(child, owner.Pointer);
        Check.Equal(child, RawView.LinkTarget(owner, true));
        Check.Equal(0, (long)RawView.LinkTarget(owner, false));

        TestMemory.Clear();
        Check.Equal(0, (long)RawView.LinkTarget(owner, true));
    }

    [Test]
    public void RowsGroupByDeclaringClassMostDerivedFirstAndDropDelegates()
    {
        var (info, bytes, _) = Thing();

        var groups = RawView.Group(info, bytes, null, false, "");

        Check.Equal("EMonster,Actor", string.Join(",", groups.Select(g => g.Owner)));
        Check.Equal("bHidden,bLocked", string.Join(",", groups[1].Rows.Select(r => r.Prop.Name)));
    }

    [Test]
    public void HideDefaultsKeepsOnlyPropsThatDifferFromTheDefaultObject()
    {
        var (info, bytes, defaults) = Thing();

        var groups = RawView.Group(info, bytes, defaults, true, "");

        Check.Equal("Level,Spot", string.Join(",", groups.Single().Rows.Select(r => r.Prop.Name)));
        Check.Equal("EMonster", groups.Single().Owner);
    }

    [Test]
    public void BoolBitsCompareTheirOwnBitOnly()
    {
        var (info, bytes, defaults) = Thing();
        bytes[0x30] = 3;

        var rows = RawView.Group(info, bytes, defaults, true, "").SelectMany(g => g.Rows).Select(r => r.Prop.Name);

        Check.That(rows.Contains("bLocked") && !rows.Contains("bHidden"), "only bLocked differs");
    }

    [Test]
    public void WithoutADefaultObjectEveryRowShows()
    {
        var (info, bytes, _) = Thing();

        var count = RawView.Group(info, bytes, null, true, "").Sum(g => g.Rows.Count);

        Check.Equal(4, count);
    }

    [Test]
    public void SearchMatchesNameTypeValueAndInlineStructMembers()
    {
        var (info, bytes, _) = Thing();

        Check.Equal("Level", string.Join(",", RawView.Group(info, bytes, null, false, "level").SelectMany(g => g.Rows).Select(r => r.Prop.Name)));
        Check.Equal("Level", string.Join(",", RawView.Group(info, bytes, null, false, "intprop").SelectMany(g => g.Rows).Select(r => r.Prop.Name)));
        Check.Equal("Spot", string.Join(",", RawView.Group(info, bytes, null, false, "7.5").SelectMany(g => g.Rows).Select(r => r.Prop.Name)));
        Check.Equal(0, RawView.Group(info, bytes, null, false, "zzz").Count);
    }

    [Test]
    public void ClassesWithoutPropsAndShortBuffersDoNotThrow()
    {
        var ue = new TestUe();
        var empty = Ue.Struct(ue.Class("Empty"))!;
        var (info, _, _) = Thing();

        Check.Equal(0, RawView.Group(empty, new byte[8], null, false, "").Count);
        Check.That(RawView.Group(info, new byte[4], new byte[2], true, "x").Count >= 0, "short buffers are fine");
    }

    [Test]
    public void StructRowsAreBuiltOncePerSnapshot()
    {
        var (hero, _) = World();
        Ue.TryObject(hero, out var obj);
        var spot = obj.Class!.Properties.Single(p => p.Name == "Spot");
        var info = Ue.Struct(spot.Struct)!;
        var bytes = new byte[info.Size];
        Memory.Read(hero + spot.Offset, bytes);
        var view = new RawView(new Snapshots(() => 0), Samples.NoPin);

        Samples.Frame(() =>
        {
            var first = view.StructRows(info, bytes);
            Check.Equal("X,Y,Z", string.Join(",", first.Select(row => row.Label)));
            Check.Equal("+4", first[1].Offset);
            Check.That(ReferenceEquals(first, view.StructRows(info, bytes)), "same snapshot, same rows");
            Check.That(!ReferenceEquals(first, view.StructRows(info, (byte[])bytes.Clone())), "a new snapshot rebuilds");
        });
    }

    [Test]
    public void ElementRowsShiftEachElementIntoTheBlockAndAreBuiltOncePerSnapshot()
    {
        var (ue, hero, _, _) = Samples.ActorWorld();
        Ue.TryObject(hero, out var obj);
        var tags = obj.Class!.Properties.Single(p => p.Name == "Tags");
        var data = TestMemory.Alloc(16);
        TestMemory.Write(data, ue.Name("Boss"));
        TestMemory.Write(data + 8, ue.Name("Elite"));
        var bytes = new byte[16];
        Memory.Read(data, bytes);
        var view = new RawView(new Snapshots(() => 0), Samples.NoPin);

        Samples.Frame(() =>
        {
            var rows = view.ElementRows(bytes, tags.Element!, 2);
            Check.Equal("0,8", string.Join(",", rows.Select(row => row.Prop.Offset)));
            Check.Equal("[0],[1]", string.Join(",", rows.Select(row => row.Offset)));
            Check.That(rows[1].Value.Contains("Elite"), rows[1].Value);
            Check.That(ReferenceEquals(rows, view.ElementRows(bytes, tags.Element!, 2)), "same snapshot, same rows");
        });
    }

    [Test]
    public void EntryRowsListTheMapEntriesWithTheirBlocksAndReuseThemPerSnapshot()
    {
        var (hero, _) = World();
        Ue.TryObject(hero, out var obj);
        var cls = obj.Class!;
        var parts = cls.Properties.Single(p => p.Name == "Parts");
        var bytes = new byte[cls.Size];
        Memory.Read(hero, bytes);
        var view = new RawView(new Snapshots(() => 0), Samples.NoPin);

        Samples.Frame(() =>
        {
            var rows = view.EntryRows(bytes, hero + parts.Offset, parts);
            Check.Equal(2, rows.Length);
            Check.Equal("[0] key,[1] key", string.Join(",", rows.Select(row => row.Key?.Label)));
            Check.Equal("value", rows[0].Value?.Label);
            Check.That(ReferenceEquals(rows, view.EntryRows(bytes, hero + parts.Offset, parts)), "same snapshot, same rows");
        });
    }

    [Test]
    public void EntryRowsShowUnknownOnceTheEntryBlockCanNoLongerBeRead()
    {
        var (hero, _) = World();
        Ue.TryObject(hero, out var obj);
        var cls = obj.Class!;
        var parts = cls.Properties.Single(p => p.Name == "Parts");
        var bytes = new byte[cls.Size];
        Memory.Read(hero, bytes);
        long now = 0;
        var view = new RawView(new Snapshots(() => now), Samples.NoPin);

        Samples.Frame(() =>
        {
            var rows = view.EntryRows(bytes, hero + parts.Offset, parts);
            Check.That(rows.All(row => row.Block is not null), "readable at first");

            TestMemory.Clear();
            now = 1000;
            Check.That(view.EntryRows(bytes, hero + parts.Offset, parts).All(row => row.Block is null && row.Key is null), "freed entries must not show old data");
        });
    }

    [Test]
    public void FrozenEntryRowsKeepTheirFrozenBlocks()
    {
        var (hero, _) = World();
        Ue.TryObject(hero, out var obj);
        var cls = obj.Class!;
        var parts = cls.Properties.Single(p => p.Name == "Parts");
        var bytes = new byte[cls.Size];
        Memory.Read(hero, bytes);
        long now = 0;
        var snapshots = new Snapshots(() => now);
        var view = new RawView(snapshots, Samples.NoPin);

        Samples.Frame(() =>
        {
            var rows = view.EntryRows(bytes, hero + parts.Offset, parts).ToArray();
            snapshots.Frozen = true;
            TestMemory.Clear();
            now = 1000;
            var again = view.EntryRows(bytes, hero + parts.Offset, parts);
            Check.That(again.Select(row => row.Block).SequenceEqual(rows.Select(row => row.Block)), "frozen blocks stay");
            Check.That(again.All(row => row.Key is not null), "frozen rows stay");
        });
    }

    [Test]
    public void CachedRowsRebuildWhenTheLayoutChangesOnTheSameBytes()
    {
        var (hero, _) = World();
        Ue.TryObject(hero, out var obj);
        var spot = obj.Class!.Properties.Single(p => p.Name == "Spot");
        var info = Ue.Struct(spot.Struct)!;
        var other = info with { Properties = info.Properties.Take(1).ToArray() };
        var bytes = new byte[info.Size];
        Memory.Read(hero + spot.Offset, bytes);
        var element = new UProp("E", "IntProperty", 0, 4, 1, 0, 0, null);
        var view = new RawView(new Snapshots(() => 0), Samples.NoPin);

        Samples.Frame(() =>
        {
            Check.Equal(3, view.StructRows(info, bytes).Length);
            Check.Equal(1, view.StructRows(other, bytes).Length);
            Check.Equal(2, view.ElementRows(bytes, element, 2).Length);
            Check.Equal(3, view.ElementRows(bytes, element, 3).Length);
        });
    }
}
