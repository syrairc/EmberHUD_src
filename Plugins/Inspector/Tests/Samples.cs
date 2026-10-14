using System;
using System.Collections;
using System.Numerics;
using EmberHUD;

namespace Inspector;

internal sealed class Thrower
{
    public int Fine => 1;
    public int Bad => throw new InvalidOperationException();
}

internal static class Samples
{
    public static readonly Player Player = new(new Vector3(10, 20, 0), Vector2.UnitX);

    public static readonly Entity[] Entities =
    [
        new Entity(1, 100, new Vector3(13, 24, 0), EntityKind.Monster, Rarity.Rare) { Address = 0x1000 },
        new Entity(2, 200, new Vector3(-5, 2, 1), EntityKind.Npc, Rarity.Normal),
    ];

    public static readonly MapIcon[] Icons = [new(5, "UI_MiniMap_Portal", new Vector2(1, 2), 1, 0, 0), new(6, "UI_MiniMap_S15xiaoguai", new Vector2(13, 24), 1, 0, 1)];

    public static readonly Terrain Terrain = new(2, 2, 32, Vector2.Zero, new BitArray(new[] { true, false, true, true }));

    public static readonly Camera Camera = new(new Vector3(-990, 20, 1000), new Vector3(-45, 0, 0), 90);

    public static readonly GameState Rich = new GameState("ready", 7, Player, Entities, Terrain, -90)
    {
        MapIcons = Icons,
        Explored = new ExploredTrail([new Vector2(1, 1)], 250),
        Camera = Camera,
    };
}
