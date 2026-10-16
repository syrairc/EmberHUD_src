using System;
using System.Collections;
using System.Numerics;
using EmberHUD;
using EmberHUD.Testing;

namespace NavigationPlugin;

public sealed class PathFinderTests
{
    static Terrain Grid(params string[] rows)
    {
        var bits = new BitArray(rows[0].Length * rows.Length);
        for (var row = 0; row < rows.Length; row++)
            for (var col = 0; col < rows[row].Length; col++)
                bits[row * rows[0].Length + col] = rows[row][col] == '.';
        return new Terrain(rows[0].Length, rows.Length, 10f, Vector2.Zero, bits);
    }

    static Vector2 Centre(int col, int row) => new(col * 10 + 5, row * 10 + 5);

    static bool Walkable(Terrain terrain, Vector2 at) => terrain.IsWalkable((int)MathF.Floor(at.X / 10), (int)MathF.Floor(at.Y / 10));

    [Test]
    public void StraightLineInAnOpenRoom()
    {
        var path = PathFinder.Find(Grid("..........", "..........", "..........", "..........", ".........."), Centre(0, 0), Centre(9, 4));

        Check.Equal(2, path!.Length);
        Check.Equal(Centre(0, 0), path[0]);
        Check.Equal(Centre(9, 4), path[1]);
    }

    [Test]
    public void RoutesThroughTheGapInAWall()
    {
        var terrain = Grid("....#....", "....#....", "....#....", "....#....", "....#....", "....#....", ".........");

        var path = PathFinder.Find(terrain, Centre(0, 0), Centre(8, 0))!;

        Check.That(path.Length > 2, "bends round the wall");
        for (var i = 1; i < path.Length; i++)
            for (var step = 0; step <= 200; step++)
                Check.That(Walkable(terrain, Vector2.Lerp(path[i - 1], path[i], step / 200f)), $"segment {i} step {step}");
    }

    [Test]
    public void UnreachableTargetIsNull() =>
        Check.That(PathFinder.Find(Grid(".....", ".###.", ".#.#.", ".###.", "....."), Centre(0, 0), Centre(2, 2)) is null, "sealed pocket");

    [Test]
    public void TargetOutsideTheGridIsNull()
    {
        var terrain = Grid(".....", ".....");

        Check.That(PathFinder.Find(terrain, Centre(0, 0), new Vector2(-50, 5)) is null, "left of the grid");
        Check.That(PathFinder.Find(terrain, Centre(0, 0), new Vector2(1000, 5)) is null, "right of the grid");
        Check.That(PathFinder.Find(terrain, Centre(0, 0), new Vector2(float.NaN, 5)) is null, "not finite");
    }

    [Test]
    public void TargetOnABlockedCellSnapsToNearestWalkable()
    {
        var path = PathFinder.Find(Grid("....#...."), Centre(0, 0), new Vector2(42, 5))!;

        Check.Equal(Centre(3, 0), path[^1]);
    }

    [Test]
    public void StartOffTheGridSnapsToNearestWalkable()
    {
        var path = PathFinder.Find(Grid("#...."), new Vector2(-30, 5), Centre(4, 0))!;

        Check.Equal(Centre(1, 0), path[0]);
        Check.Equal(Centre(4, 0), path[^1]);
    }

    [Test]
    public void SmoothingNeverCutsACorner()
    {
        var path = PathFinder.Find(Grid("......", "#####.", "#####.", "#####.", "#####.", "#####."), Centre(0, 0), Centre(5, 5))!;

        Check.Equal(3, path.Length);
        Check.Equal(Centre(5, 0), path[1]);
    }

    [Test]
    public void LengthIsSumOfSegments() =>
        Check.That(MathF.Abs(PathFinder.Length([new Vector2(0, 0), new Vector2(3, 4), new Vector2(3, 10)]) - 11) < 1e-4f, "5 + 6");
}
