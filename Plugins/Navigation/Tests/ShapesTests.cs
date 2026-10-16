using System;
using System.Linq;
using System.Numerics;
using EmberHUD;
using EmberHUD.Testing;

namespace NavigationPlugin;

public sealed class ShapesTests
{
    static readonly Vector2 East = Vector2.UnitX;

    static Vector2[] Chevron(Vector2 centre, Vector2 direction, float size)
    {
        var into = new Vector2[8];
        return into[..Shapes.Chevron(centre, direction, size, into)];
    }

    static Vector2[] Streak(Vector2 head, Vector2 direction, float length, float width)
    {
        var into = new Vector2[8];
        return into[..Shapes.Streak(head, direction, length, width, into)];
    }

    [Test]
    public void ChevronPointsAlongTheDirection()
    {
        var centre = new Vector2(10, 20);
        var points = Chevron(centre, East, 25);
        var tip = points.MaxBy(p => Vector2.Dot(p - centre, East));

        Check.That(points.Length >= 3, "polygon");
        Check.That(Vector2.Distance(tip, centre) <= 25, "tip near centre");
        Check.That(Vector2.Dot(tip - centre, East) > 0, "tip ahead");
        Check.Equal(1, points.Count(p => Vector2.Dot(p - centre, East) >= Vector2.Dot(tip - centre, East) - 0.001f));
    }

    [Test]
    public void ChevronIsCentredNearTheCentre()
    {
        var centre = new Vector2(300, -40);
        var points = Chevron(centre, Vector2.Normalize(new Vector2(1, 1)), 25);

        Check.That(Vector2.Distance(points.Aggregate(Vector2.Zero, (a, b) => a + b) / points.Length, centre) <= 25, "centroid");
    }

    [Test]
    public void StreakTrailsBehindTheHead()
    {
        var head = new Vector2(100, 50);
        var points = Streak(head, East, 200, 20);

        Check.That(points.Length >= 3, "polygon");
        Check.That(points.All(p => Vector2.Dot(p - head, East) <= 0.001f), "all behind");
        Check.That(points.Any(p => MathF.Abs(Vector2.Distance(p, head) - 200) < 0.01f), "tail at length");
    }

    [Test]
    public void ZeroDirectionDrawsNothing()
    {
        Check.Equal(0, Chevron(Vector2.Zero, Vector2.Zero, 25).Length);
        Check.Equal(0, Streak(Vector2.Zero, Vector2.Zero, 100, 10).Length);
    }

    [Test]
    public void NonFiniteInputDrawsNothing()
    {
        Check.Equal(0, Chevron(Vector2.Zero, new Vector2(float.NaN, 0), 25).Length);
        Check.Equal(0, Chevron(Vector2.Zero, East, float.PositiveInfinity).Length);
        Check.Equal(0, Chevron(new Vector2(float.NaN, 0), East, 25).Length);
        Check.Equal(0, Chevron(Vector2.Zero, East, 0).Length);
        Check.Equal(0, Streak(Vector2.Zero, East, float.NaN, 10).Length);
        Check.Equal(0, Streak(Vector2.Zero, East, 100, -1).Length);
        Check.Equal(0, Streak(new Vector2(0, float.PositiveInfinity), East, 100, 10).Length);
    }

    [Test]
    public void NeverOverrunsTheBuffer()
    {
        var into = new Vector2[2];

        Check.Equal(0, Shapes.Chevron(Vector2.Zero, East, 25, into));
        Check.Equal(0, Shapes.Streak(Vector2.Zero, East, 100, 10, into));
        Check.Equal(0, Shapes.Streak(Vector2.Zero, East, 100, 10, []));
    }
}
