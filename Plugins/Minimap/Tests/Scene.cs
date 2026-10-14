using System.Collections;
using System.Numerics;
using EmberHUD;

namespace Minimap;

public static class Scene
{
    public static Terrain Cells(int cols, int rows, params (int col, int row)[] walkable)
    {
        var bits = new BitArray(cols * rows);
        foreach (var (col, row) in walkable) bits[row * cols + col] = true;
        return new Terrain(cols, rows, 10f, Vector2.Zero, bits);
    }

    public static Terrain OneCell() => Cells(3, 3, (1, 1));

    public static Entity Entity(EntityKind kind, Vector3 at, Rarity rarity = Rarity.Normal) => new(1, 1000, at, kind, rarity);

    public static GameState State(Player? player = null, Entity[]? entities = null, Terrain? terrain = null, int area = 1, float? rotation = null, MapIcon[]? icons = null, ExploredTrail? explored = null) =>
        new("test", area, player, entities ?? [], terrain, rotation) { MapIcons = icons ?? [], Explored = explored };

    public static Main Plugin()
    {
        var main = new Main { WallJob = new(inline: true), RegionJob = new(inline: true) };
        main.Initialize();
        return main;
    }
}
