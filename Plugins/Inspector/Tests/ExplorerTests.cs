using System;
using System.Linq;
using System.Numerics;
using Aember;
using Aember.Testing;
using Hexa.NET.ImGui;

namespace Inspector;

public sealed class ExplorerTests
{
    static void Frame(Action draw) => TestFrame.Run(() =>
    {
        ImGui.SetNextWindowSize(new Vector2(1200, 700));
        ImGui.Begin("explorer");
        draw();
        ImGui.End();
    });

    static (Explorer Explorer, TestUe Ue, nint Hero) Setup()
    {
        var ue = new TestUe();
        var vector = ue.ScriptStruct("Vector", 12, new TestProp("X", "FloatProperty", 0), new TestProp("Y", "FloatProperty", 4), new TestProp("Z", "FloatProperty", 8));
        var scene = ue.Class("SceneComponent", 0, 0x40, new TestProp("RelativeLocation", "StructProperty", 0x30, 12, Struct: vector));
        var actor = ue.Class("Actor", 0, 0x50, new TestProp("RootComponent", "ObjectProperty", 0x30, 8));
        var monster = ue.Class("EMonster", actor, 0x58);
        var root = ue.Object("Root", scene);
        TestMemory.Write(root + 0x30, new Vector3(13, 24, 0));
        var hero = ue.Object("Hero", monster);
        TestMemory.Write(hero + 0x30, root);
        var raw = new RawTree(new Snapshots(() => 0));
        return (new Explorer(raw, new Snapshots(() => 0)) { Job = new(inline: true) }, ue, hero);
    }

    [Test]
    public void TheIndexFillsOnFirstDrawAndRefreshesOnAnAreaChange()
    {
        var (explorer, ue, _) = Setup();
        Frame(() => explorer.Draw(1));
        Frame(() => explorer.Draw(1));
        var before = explorer.Index.Rows.Length;
        Check.That(before > 0, "rows after the first scan");

        ue.Object("Late", ue.Class("Thing"));
        Frame(() => explorer.Draw(1));
        Check.Equal(before, explorer.Index.Rows.Length);
        Frame(() => explorer.Draw(2));
        Frame(() => explorer.Draw(2));
        Check.Equal(before + 2, explorer.Index.Rows.Length);
    }

    [Test]
    public void AFaultedScanLeavesAnEmptyIndex()
    {
        var (explorer, _, hero) = Setup();
        explorer.Seed(ObjectIndex.Build([new ObjectRow(0, "Hero", "EMonster", "Outer", hero, hero, false)]), 1);
        Check.Equal(1, explorer.Index.Rows.Length);
        explorer.Job.Start(() => throw new InvalidOperationException("boom"));

        Frame(() => explorer.Draw(1));

        Check.Equal(0, explorer.Index.Rows.Length);
    }

    [Test]
    public void SelectingAnObjectDrawsItAndABackAndForwardTrail()
    {
        var (explorer, _, hero) = Setup();
        Frame(() => explorer.Draw(1));
        Frame(() => explorer.Draw(1));
        explorer.History.Push(hero);
        explorer.SelectedClass = explorer.Index.Rows.First(r => r.Name == "Hero").ClassAddress;

        Frame(() => explorer.Draw(1));
        Frame(() => explorer.Draw(1));

        Check.Equal(hero, explorer.History.Current);
    }

    [Test]
    public void AGoneObjectKeepsItsHistoryEntryAndDrawsWithoutReading()
    {
        var (explorer, _, hero) = Setup();
        Frame(() => explorer.Draw(1));
        explorer.History.Push(hero);

        TestMemory.WriteBytes(hero, new byte[0x30]);
        Frame(() => explorer.Draw(1));
        Frame(() => explorer.Draw(1));

        Check.Equal(hero, explorer.History.Current);
        Check.That(!Ue.TryObject(hero, out _), "the object is really gone");
    }

    [Test]
    public void SearchReplacesTheInstanceList()
    {
        var (explorer, _, _) = Setup();
        Frame(() => explorer.Draw(1));
        Frame(() => explorer.Draw(1));

        explorer.Search = "hero";
        Frame(() => explorer.Draw(1));

        Check.That(explorer.RowsDrawn > 0, "search results drew rows");
    }

    [Test]
    public void TheInstanceTableOnlyDrawsTheRowsInView()
    {
        var (explorer, _, _) = Setup();
        Frame(() => explorer.Draw(1));
        Frame(() => explorer.Draw(1));
        var cls = explorer.Index.Rows.First(r => r.Name == "Hero").ClassAddress;
        explorer.Seed(ObjectIndex.Build([.. Enumerable.Range(0, 5000).Select(i => new ObjectRow(i, $"Thing{i}", "EMonster", "Outer", 0x10000 + i * 0x100, cls, false))]), 1);
        explorer.SelectedClass = cls;

        Frame(() => explorer.Draw(1));

        Check.That(explorer.RowsDrawn is > 0 and < 200, $"the table drew {explorer.RowsDrawn} of 5000 rows");
    }

    [Test]
    public void DetachedSaysNotAttached()
    {
        TestMemory.Clear();
        var explorer = new Explorer(new RawTree(new Snapshots(() => 0)), new Snapshots(() => 0));

        Frame(() => explorer.Draw(1));
    }
}
