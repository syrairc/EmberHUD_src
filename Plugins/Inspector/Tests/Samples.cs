using System;
using System.Collections;
using System.Numerics;
using EmberHUD;
using EmberHUD.Testing;
using Hexa.NET.ImGui;

namespace Inspector;

internal sealed class Thrower
{
    public int Fine => 1;
    public int Bad => throw new InvalidOperationException();
}

internal static class Samples
{
    public static void NoPin(object _)
    {
    }

    public static readonly Player Player = new(new Vector3(10, 20, 0), Vector2.UnitX);

    public static readonly Entity[] Entities =
    [
        new Entity(1, 100, new Vector3(13, 24, 0), EntityKind.Monster, Rarity.Rare) { Address = 0x1000 },
        new Entity(2, 200, new Vector3(-5, 2, 1), EntityKind.Npc, Rarity.Normal),
    ];

    public static readonly MapIcon[] Icons = [new(5, "UI_MiniMap_Portal", new Vector2(1, 2), 1, 0, 0), new(6, "UI_MiniMap_S15xiaoguai", new Vector2(13, 24), 1, 0, 1)];

    public static readonly Terrain Terrain = new(2, 2, 32, Vector2.Zero, new BitArray(new[] { true, false, true, true }));

    public static readonly Camera Camera = new(new Vector3(-990, 20, 1000), new Vector3(-45, 0, 0), 90);

    public static readonly GameState Rich = new GameState
    {
        Status = "ready",
        AreaId = 7,
        Player = Player,
        Entities = Entities,
        Terrain = Terrain,
        MinimapRotation = -90,
        MapIcons = Icons,
        Explored = new ExploredTrail([new Vector2(1, 1)], 250),
        Camera = Camera,
    };

    public static void Frame(Action draw) => TestFrame.Run(() =>
    {
        ImGui.SetNextWindowSize(new Vector2(1200, 700));
        ImGui.Begin("frame");
        draw();
        ImGui.End();
    });

    public static (TestUe Ue, nint Hero, nint Root, nint Monster) ActorWorld()
    {
        var ue = new TestUe();
        var vector = ue.ScriptStruct("Vector", 12, new TestProp("X", "FloatProperty", 0), new TestProp("Y", "FloatProperty", 4), new TestProp("Z", "FloatProperty", 8));
        var scene = ue.Class("SceneComponent", 0, 0x40, new TestProp("RelativeLocation", "StructProperty", 0x30, 12, Struct: vector));
        var actor = ue.Class("Actor", 0, 0x50,
            new TestProp("RootComponent", "ObjectProperty", 0x30, 8),
            new TestProp("Tags", "ArrayProperty", 0x38, 16, Element: new TestProp("Tags", "NameProperty", 0, 8)),
            new TestProp("bHidden", "BoolProperty", 0x48, 1, BoolMask: 1));
        var monster = ue.Class("EMonster", actor, 0x58);
        var root = ue.Object("Root", scene);
        TestMemory.Write(root + 0x30, new Vector3(1, 2, 3));
        var hero = ue.Object("Hero", monster);
        TestMemory.Write(hero + 0x30, root);
        var tags = TestMemory.Alloc(16);
        TestMemory.Write(tags, ue.Name("Boss"));
        TestMemory.Write(hero + 0x38, tags);
        TestMemory.Write(hero + 0x40, 1);
        return (ue, hero, root, monster);
    }
}
