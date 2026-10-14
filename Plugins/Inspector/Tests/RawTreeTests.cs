using System;
using System.Linq;
using System.Numerics;
using EmberHUD;
using EmberHUD.Testing;
using Hexa.NET.ImGui;

namespace Inspector;

public sealed class RawTreeTests
{
    static void Frame(Action draw) => TestFrame.Run(() =>
    {
        ImGui.SetNextWindowSize(new Vector2(900, 600));
        ImGui.Begin("raw");
        draw();
        ImGui.End();
    });

    static (TestUe Ue, nint Hero) World()
    {
        var ue = new TestUe();
        var vector = ue.ScriptStruct("Vector", 12, new TestProp("X", "FloatProperty", 0), new TestProp("Y", "FloatProperty", 4), new TestProp("Z", "FloatProperty", 8));
        var scene = ue.Class("SceneComponent", 0, 0x40, new TestProp("RelativeLocation", "StructProperty", 0x30, 12, Struct: vector));
        var actor = ue.Class("Actor", 0, 0x50,
            new TestProp("RootComponent", "ObjectProperty", 0x30, 8),
            new TestProp("Tags", "ArrayProperty", 0x38, 16, Element: new TestProp("Tags", "NameProperty", 0, 8)),
            new TestProp("bHidden", "BoolProperty", 0x48, 1, BoolMask: 1));
        var root = ue.Object("Root", scene);
        TestMemory.Write(root + 0x30, new Vector3(1, 2, 3));
        var hero = ue.Object("Hero", actor);
        TestMemory.Write(hero + 0x30, root);
        var tags = TestMemory.Alloc(16);
        TestMemory.Write(tags, ue.Name("Boss"));
        TestMemory.Write(hero + 0x38, tags);
        TestMemory.Write(hero + 0x40, 1);
        return (ue, hero);
    }

    [Test]
    public void AUObjectAndAHexBlockDrawFullyOpen()
    {
        var (_, hero) = World();
        var logic = TestMemory.Alloc(0x200);
        TestMemory.Write(logic + 8, hero);
        TestMemory.Write(logic + 16, 1.5f);
        var raw = new RawTree(new Snapshots(() => 0)) { OpenAll = true };

        Frame(() =>
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
        var raw = new RawTree(new Snapshots(() => 0)) { OpenAll = true };

        Frame(() =>
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
        var raw = new RawTree(new Snapshots(() => 0)) { OpenAll = true };

        Frame(() => raw.Node("odd", obj, 0));
    }

    [Test]
    public void GameStringsWithFormatSpecifiersDrawWithoutCrashing()
    {
        var ue = new TestUe();
        var cls = ue.Class("Bad%s%n%d", 0, 0x40, new TestProp("P%s%n", "Odd%s%n%dProperty", 0x30, 4));
        var obj = ue.Object("Obj%s%n%d", cls);
        var raw = new RawTree(new Snapshots(() => 0)) { OpenAll = true };

        Frame(() => raw.Node("fmt", obj, 0));
    }

    [Test]
    public void RawSectionsSayNotAttached()
    {
        TestMemory.Clear();
        var raw = new RawTree(new Snapshots(() => 0));

        Frame(() =>
        {
            raw.Globals();
            raw.Address();
            raw.Node("detached", 0x10000, 0);
        });
    }

    [Test]
    public void GlobalsDraw()
    {
        var (_, hero) = World();
        TestMemory.Global("FightMgr", hero);
        var raw = new RawTree(new Snapshots(() => 0)) { OpenAll = true };

        Frame(raw.Globals);
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
        var raw = new RawTree(new Snapshots(() => 0)) { OpenAll = true };

        Frame(() => raw.Node("odd", obj, 0));
    }

    [Test]
    public void HexRowsFormatEightBytesAndAreBuiltOncePerSnapshot()
    {
        var at = TestMemory.Alloc(0x20);
        TestMemory.Write(at, 0x0000000200000001L);
        var raw = new RawTree(new Snapshots(() => 0));
        var bytes = new byte[0x20];
        Memory.Read(at, bytes);

        Frame(() =>
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
        var (_, hero) = World();
        var block = TestMemory.Alloc(0x10);
        TestMemory.Write(block, hero);
        var bytes = new byte[0x10];
        Memory.Read(block, bytes);
        var raw = new RawTree(new Snapshots(() => 0));
        HexRow[] first = [];

        Frame(() => first = raw.RowsOf(bytes));
        Check.That(first[0].Target is { } label && label.StartsWith("->") && label.Contains("Hero"), first[0].Target);

        TestMemory.Clear();
        Frame(() => Check.That(ReferenceEquals(first, raw.RowsOf(bytes)), "no rebuild, so no memory read"));
    }
}
