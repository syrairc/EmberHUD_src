using System;
using System.Linq;
using System.Numerics;
using Aember;
using Aember.Testing;
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
            raw.Objects(1);
            raw.Address();
            raw.Node("detached", 0x10000, 0);
        });
    }

    [Test]
    public void GlobalsDrawAndTheObjectsSnapshotListsEveryObject()
    {
        var (_, hero) = World();
        TestMemory.Global("FightMgr", hero);
        var raw = new RawTree(new Snapshots(() => 0)) { ObjectsJob = new(inline: true), OpenAll = true };

        Frame(raw.Globals);
        Frame(() => raw.Objects(1));
        Frame(() => raw.Objects(1));

        var row = raw.ObjectRows.Single(r => r.Name == "Hero");
        Check.Equal("Actor", row.Class);
        Check.Equal(hero, row.Address);
    }

    [Test]
    public void TheObjectsSnapshotRefreshesOnAnAreaChange()
    {
        var (ue, _) = World();
        var raw = new RawTree(new Snapshots(() => 0)) { ObjectsJob = new(inline: true) };
        Frame(() => raw.Objects(1));
        Frame(() => raw.Objects(1));
        var before = raw.ObjectRows.Length;

        ue.Object("Late", ue.Class("Thing"));
        Frame(() => raw.Objects(1));
        Check.Equal(before, raw.ObjectRows.Length);
        Frame(() => raw.Objects(2));
        Frame(() => raw.Objects(2));
        Check.Equal(before + 2, raw.ObjectRows.Length);
    }

    [Test]
    public void AFaultedObjectsJobLeavesNoRowsAndDoesNotThrow()
    {
        World();
        var raw = new RawTree(new Snapshots(() => 0)) { ObjectsJob = new(inline: true) };
        raw.ObjectsJob.Start(() => throw new InvalidOperationException("boom"));

        Frame(() => raw.Objects(1));

        Check.Equal(0, raw.ObjectRows.Length);
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
    public void TheObjectsTableOnlyDrawsTheRowsInView()
    {
        var raw = new RawTree(new Snapshots(() => 0)) { ObjectsJob = new(inline: true) };
        World();
        Frame(() => raw.Objects(1));
        Frame(() => raw.Objects(1));
        raw.ObjectRows = [.. Enumerable.Range(0, 5000).Select(i => new ObjectRow(i, $"Thing{i}", "Thing", "Outer", 0x10000 + i * 0x100))];

        Frame(() => raw.Objects(1));

        Check.That(raw.RowsDrawn is > 0 and < 200, $"the table drew {raw.RowsDrawn} of 5000 rows");
    }
}
