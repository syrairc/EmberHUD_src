using System;
using System.Numerics;
using EmberHUD;
using EmberHUD.Testing;
using Hexa.NET.ImGui;

namespace Inspector;

public sealed class PropertyViewTests
{
    static void Frame(Action draw) => TestFrame.Run(() =>
    {
        ImGui.SetNextWindowSize(new Vector2(900, 600));
        ImGui.Begin("view");
        draw();
        ImGui.End();
    });

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

    static void Draw(PropertyView view, nint address, bool link)
    {
        if (Ue.TryObject(address, out var obj) && obj.Class is { } cls) view.Object(address, obj, cls, 0, link);
    }

    [Test]
    public void AnObjectDrawsGroupedWithEnumsStructsAndMapEntries()
    {
        var (hero, _) = World();
        var raw = new RawTree(new Snapshots(() => 0));
        raw.View.OpenAll = true;

        Frame(() => Draw(raw.View, hero, false));
        Frame(() => Draw(raw.View, hero, true));
    }

    [Test]
    public void HideDefaultsAndSearchDrawWithoutThrowing()
    {
        var (hero, _) = World();
        var raw = new RawTree(new Snapshots(() => 0));
        raw.View.HideDefaults = true;
        raw.View.OpenAll = true;

        Frame(() =>
        {
            raw.View.Toolbar();
            Draw(raw.View, hero, true);
        });
        raw.View.Search = "rare";
        Frame(() => Draw(raw.View, hero, true));
        raw.View.Search = "2";
        Frame(() => Draw(raw.View, hero, true));
        Frame(() => Draw(raw.View, hero, false));
        raw.View.Search = "no such thing";
        Frame(() => Draw(raw.View, hero, true));
    }

    [Test]
    public void AMissingDefaultObjectStillShowsEverything()
    {
        var (hero, _) = World(withDefault: false);
        var raw = new RawTree(new Snapshots(() => 0));
        raw.View.HideDefaults = true;

        Frame(() => Draw(raw.View, hero, true));
    }

    [Test]
    public void LinkModeNeverNavigatesByItself()
    {
        var (hero, child) = World();
        nint went = 0;
        var raw = new RawTree(new Snapshots(() => 0));
        raw.View.Navigate = at => went = at;
        raw.View.OpenAll = true;

        Frame(() => Draw(raw.View, hero, true));
        Check.Equal((nint)0, went);
        Check.That(Ue.TryObject(child, out _), "Owner points at a live object, so the row drew as a link");
    }
}
