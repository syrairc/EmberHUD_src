using System;
using System.Numerics;
using EmberHUD;
using EmberHUD.Testing;

namespace NavigationPlugin;

public sealed class ParticleTests
{
    static readonly Vector2[] Straight = [Vector2.Zero, new(1000, 0)];

    static Vector2[] Run(Vector2[] path, float length, float spacing, float speed, double seconds, int capacity = 64)
    {
        var into = new Vector2[capacity];
        var count = Particles.Positions(path, length, spacing, speed, seconds, into);
        return into[..count];
    }

    [Test]
    public void SpacingIsRespected()
    {
        var positions = Run(Straight, 1000, 250, 0, 0);

        Check.Equal(4, positions.Length);
        for (var i = 1; i < positions.Length; i++) Check.Equal(250f, positions[i].X - positions[i - 1].X);
    }

    [Test]
    public void ScrollsForwardWithTime()
    {
        var before = Run(Straight, 1000, 250, 100, 0);
        var after = Run(Straight, 1000, 250, 100, 1);

        Check.Equal(before[0].X + 100, after[0].X);
    }

    [Test]
    public void WrapsAtPathLength()
    {
        var start = Run(Straight, 1000, 250, 100, 0);
        var wrapped = Run(Straight, 1000, 250, 100, 10);

        Check.Equal(start[0].X, wrapped[0].X);
        Check.That(Array.TrueForAll(Run(Straight, 1000, 250, 300, 7), p => p.X >= 0 && p.X <= 1000), "inside the path");
    }

    [Test]
    public void ZeroLengthPathDrawsNothing()
    {
        Check.Equal(0, Run(Straight, 0, 250, 100, 1).Length);
        Check.Equal(0, Run(Straight, 100, 250, 100, 1).Length);
        Check.Equal(0, Run([Vector2.Zero], 1000, 250, 100, 1).Length);
    }

    [Test]
    public void NonFiniteInputDrawsNothing()
    {
        Check.Equal(0, Run(Straight, float.NaN, 250, 100, 1).Length);
        Check.Equal(0, Run(Straight, 1000, float.PositiveInfinity, 100, 1).Length);
        Check.Equal(0, Run(Straight, 1000, 250, float.NaN, 1).Length);
        Check.Equal(0, Run(Straight, 1000, 250, 100, double.NaN).Length);
        Check.Equal(0, Run(Straight, 1000, 0, 100, 1).Length);
    }

    [Test]
    public void CountNeverExceedsBuffer() => Check.Equal(3, Run(Straight, 1000, 1, 0, 0, 3).Length);

    [Test]
    public void FollowsACorner()
    {
        Vector2[] corner = [Vector2.Zero, new(100, 0), new(100, 200)];

        var positions = Run(corner, 300, 150, 0, 0);

        Check.Equal(new Vector2(0, 0), positions[0]);
        Check.Equal(new Vector2(100, 50), positions[1]);
    }
}
