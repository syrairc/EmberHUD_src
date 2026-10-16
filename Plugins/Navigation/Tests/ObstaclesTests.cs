using System.Collections;
using System.Numerics;
using EmberHUD;
using EmberHUD.Testing;

namespace NavigationPlugin;

public sealed class ObstaclesTests
{
    static Terrain Room() => new(30, 30, 10f, Vector2.Zero, new BitArray(900, true));

    static Vector2 Centre(int col, int row) => new(col * 10 + 5, row * 10 + 5);

    static int CountBlocked(Terrain terrain)
    {
        var blocked = 0;
        for (var i = 0; i < terrain.Walkable.Length; i++)
            if (!terrain.Walkable[i]) blocked++;
        return blocked;
    }

    [Test]
    public void StampClearsCellsInsideTheRadius()
    {
        var stamped = Obstacles.Stamp(Room(), [new Vector2(150, 150)], 25);
        Check.That(!stamped.IsWalkable(14, 14), "centre cell");
        Check.That(!stamped.IsWalkable(16, 15), "20 units away");
    }

    [Test]
    public void StampLeavesCellsOutsideTheRadius()
    {
        var stamped = Obstacles.Stamp(Room(), [new Vector2(150, 150)], 25);
        Check.That(stamped.IsWalkable(18, 15), "35 units away");
        Check.That(stamped.IsWalkable(0, 0), "far corner");
    }

    [Test]
    public void StampReturnsTheSameInstanceWhenNothingToDo()
    {
        var room = Room();
        Check.That(ReferenceEquals(room, Obstacles.Stamp(room, [new Vector2(150, 150)], 0)), "radius 0");
        Check.That(ReferenceEquals(room, Obstacles.Stamp(room, [], 25)), "no centres");
    }

    [Test]
    public void StampDoesNotMutateTheOriginal()
    {
        var room = Room();
        var stamped = Obstacles.Stamp(room, [new Vector2(150, 150)], 25);
        Check.That(!ReferenceEquals(room, stamped), "new instance");
        Check.Equal(0, CountBlocked(room));
        Check.That(CountBlocked(stamped) > 0, "stamped copy has blocked cells");
    }

    [Test]
    public void StampIgnoresNonFiniteCentres()
    {
        var stamped = Obstacles.Stamp(Room(), [new Vector2(float.NaN, 5), new Vector2(float.PositiveInfinity, 5), new Vector2(150, 150)], 25);
        Check.Equal(CountBlocked(Obstacles.Stamp(Room(), [new Vector2(150, 150)], 25)), CountBlocked(stamped));
        var room = Room();
        Check.That(ReferenceEquals(room, Obstacles.Stamp(room, [new Vector2(150, 150)], float.NaN)), "nan radius");
        Check.That(ReferenceEquals(room, Obstacles.Stamp(room, [new Vector2(150, 150)], float.PositiveInfinity)), "infinite radius");
    }

    [Test]
    public void StampStaysInsideTheGridAtTheEdge()
    {
        var stamped = Obstacles.Stamp(Room(), [new Vector2(0, 0), new Vector2(-1e9f, 1e9f), new Vector2(299, 299)], 40);
        Check.That(!stamped.IsWalkable(0, 0), "corner");
        Check.That(!stamped.IsWalkable(29, 29), "far corner");
        Check.Equal(900, stamped.Walkable.Length);
    }

    [Test]
    public void ARouteGoesAroundAStampedBlockerInAnOpenRoom()
    {
        var stamped = Obstacles.Stamp(Room(), [new Vector2(150, 150)], 35);
        var path = PathFinder.Find(stamped, Centre(2, 14), Centre(27, 14))!;
        Check.That(path.Length > 2, "bends around the disc");
        Check.Equal(Centre(27, 14), path[^1]);
        foreach (var point in path) Check.That(Vector2.Distance(point, new Vector2(150, 150)) >= 35, $"{point} too close");
    }
}
