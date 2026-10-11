using System.Collections;
using System.Numerics;
using Aember;

namespace Minimap;

public static class Scene
{
    public static Terrain Cells(int cols, int rows, bool shift, params (int col, int row)[] walkable)
    {
        var bits = new BitArray(cols * rows);
        foreach (var (col, row) in walkable) bits[row * cols + col] = true;
        return new Terrain(cols, rows, 10f, Vector2.Zero, shift, bits);
    }

    public static Entity Entity(EntityKind kind, Vector3 at, Rarity rarity = Rarity.Normal) => new(1, 1000, at, kind, rarity);

    public static GameState State(Player? player = null, Entity[]? entities = null, Terrain? terrain = null, int area = 1, float? rotation = null, MapIcon[]? icons = null, ExploredTrail? explored = null) =>
        new("test", area, player, entities ?? [], terrain, rotation) { MapIcons = icons ?? [], Explored = explored };

    public static Main Plugin() => new() { WallJob = new(inline: true), RegionJob = new(inline: true) };
}
