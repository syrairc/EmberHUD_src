using System.Diagnostics;
using System.Drawing;
using Clipper2Lib;

namespace EmberHUD.Api.Tests;

public sealed class GroundGroupsTests
{
    static readonly Color Any = Color.White;

    static GroupStyle Style(Color? fill = null, string? under = null) => new(Any, fill ?? Any, 1, under, -1);

    static Vector2[] Disc(float x, float y, float radius, int segments = 96)
    {
        var points = new Vector2[segments];
        for (var i = 0; i < segments; i++)
        {
            var angle = MathF.Tau * i / segments;
            points[i] = new Vector2(x + radius * MathF.Cos(angle), y + radius * MathF.Sin(angle));
        }

        return points;
    }

    static double Area(PathsD paths) => paths.Sum(Clipper.Area);

    static double DiscArea(float radius, int segments = 96) => segments / 2.0 * radius * radius * Math.Sin(Math.PI * 2 / segments);

    [Fact]
    public void UndeclaredGroupIsNotTaken()
    {
        GroundGroups.Clear();
        Assert.False(GroundGroups.TryAddArea("x", Disc(0, 0, 10), 0));
        Assert.False(GroundGroups.TryAddArea(null, Disc(0, 0, 10), 0));
        Assert.False(GroundGroups.TryAddStrip("x", [Vector2.Zero, Vector2.One], false, 5, 0));
    }

    [Fact]
    public void TwoOverlappingDiscsMergeIntoOneRing()
    {
        GroundGroups.Clear();
        GroundGroups.Declare("g", Style());
        GroundGroups.TryAddArea("g", Disc(0, 0, 100), 0);
        GroundGroups.TryAddArea("g", Disc(150, 0, 100), 0);
        var merged = Assert.Single(GroundGroups.Merge()).Merged;
        Assert.Single(merged);
        var lens = 2 * 100 * 100 * Math.Acos(0.75) - 75 * Math.Sqrt(100 * 100 - 75 * 75) * 2;
        var expected = 2 * Math.PI * 100 * 100 - lens;
        Assert.InRange(Area(merged), expected * 0.98, expected * 1.02);
    }

    [Fact]
    public void SeparateDiscsStayTwoRings()
    {
        GroundGroups.Clear();
        GroundGroups.Declare("g", Style());
        GroundGroups.TryAddArea("g", Disc(0, 0, 100), 0);
        GroundGroups.TryAddArea("g", Disc(1000, 0, 100), 0);
        Assert.Equal(2, Assert.Single(GroundGroups.Merge()).Merged.Count);
    }

    [Fact]
    public void EnclosingShapesLeaveAHole()
    {
        GroundGroups.Clear();
        GroundGroups.Declare("g", Style());
        var (outer, inner) = (Disc(0, 0, 200, 48), Disc(0, 0, 100, 48));
        for (var i = 0; i < 48; i++)
        {
            var next = (i + 1) % 48;
            GroundGroups.TryAddArea("g", [outer[i], outer[next], inner[next], inner[i]], 0);
        }

        var merged = Assert.Single(GroundGroups.Merge()).Merged;
        Assert.Equal(2, merged.Count);
        Assert.Equal(1, merged.Count(Clipper.IsPositive));
        var expected = DiscArea(200, 48) - DiscArea(100, 48);
        Assert.InRange(Area(merged), expected * 0.98, expected * 1.02);
    }

    [Fact]
    public void AdjacentQuadsUnionWithoutSeam()
    {
        GroundGroups.Clear();
        GroundGroups.Declare("g", Style());
        GroundGroups.TryAddArea("g", [new(0, 0), new(100, 0), new(100, 100), new(0, 100)], 0);
        GroundGroups.TryAddArea("g", [new(100, 0), new(200, 0), new(200, 100), new(100, 100)], 0);
        var merged = Assert.Single(GroundGroups.Merge()).Merged;
        Assert.Single(merged);
        Assert.InRange(Area(merged), 20000 * 0.999, 20000 * 1.001);
    }

    [Fact]
    public void StripMergesWithDisc()
    {
        GroundGroups.Clear();
        GroundGroups.Declare("g", Style());
        Assert.True(GroundGroups.TryAddStrip("g", [new(0, 0), new(500, 0)], false, 20, 0));
        GroundGroups.TryAddArea("g", Disc(500, 0, 50), 0);
        Assert.Single(Assert.Single(GroundGroups.Merge()).Merged);
    }

    [Fact]
    public void UnderIsCutOut()
    {
        GroundGroups.Clear();
        GroundGroups.Declare("A", Style(under: "B"));
        GroundGroups.Declare("B", Style());
        GroundGroups.TryAddArea("B", Disc(100, 0, 100), 0);
        GroundGroups.TryAddArea("A", Disc(0, 0, 100), 0);
        var merged = GroundGroups.Merge();
        Assert.Equal(["A", "B"], merged.Select(m => m.Id));
        var lens = 2 * 100 * 100 * Math.Acos(0.5) - 50 * Math.Sqrt(100 * 100 - 50 * 50) * 2;
        var expected = Math.PI * 100 * 100 - lens;
        Assert.InRange(Area(merged[0].Merged), expected * 0.98, expected * 1.02);
    }

    [Fact]
    public void UnderComesAfterEvenWhenDeclaredFirst()
    {
        GroundGroups.Clear();
        GroundGroups.Declare("B", Style());
        GroundGroups.Declare("A", Style(under: "B"));
        GroundGroups.TryAddArea("A", Disc(0, 0, 10), 0);
        GroundGroups.TryAddArea("B", Disc(0, 0, 10), 0);
        Assert.Equal(["A", "B"], GroundGroups.Merge().Select(m => m.Id));
    }

    [Fact]
    public void UnderMissingCutsNothing()
    {
        GroundGroups.Clear();
        GroundGroups.Declare("A", Style(under: "Nope"));
        GroundGroups.TryAddArea("A", Disc(0, 0, 100), 0);
        Assert.InRange(Area(Assert.Single(GroundGroups.Merge()).Merged), DiscArea(100) * 0.999, DiscArea(100) * 1.001);
    }

    [Fact]
    public void RedeclareKeepsShapesLastStyleWins()
    {
        GroundGroups.Clear();
        GroundGroups.Declare("g", Style(Color.Red));
        GroundGroups.TryAddArea("g", Disc(0, 0, 10), 0);
        GroundGroups.Declare("g", Style(Color.Blue));
        GroundGroups.TryAddArea("g", Disc(1000, 0, 10), 0);
        var group = Assert.Single(GroundGroups.Merge());
        Assert.Equal(2, group.Merged.Count);
        Assert.Equal(Color.Blue, group.Style.Fill);
    }

    [Fact]
    public void ZIsTheFirstShapes()
    {
        GroundGroups.Clear();
        GroundGroups.Declare("g", Style());
        GroundGroups.TryAddArea("g", Disc(0, 0, 10), 7);
        GroundGroups.TryAddArea("g", Disc(50, 0, 10), 9);
        Assert.Equal(7, Assert.Single(GroundGroups.Merge()).Z);
    }

    [Fact]
    public void GarbageShapesAreSkipped()
    {
        GroundGroups.Clear();
        GroundGroups.Declare("g", Style());
        Assert.True(GroundGroups.TryAddArea("g", [new(float.NaN, 0), new(1, 0), new(0, 1)], 0));
        Assert.True(GroundGroups.TryAddArea("g", [new(0, 0), new(1, 0)], 0));
        Assert.True(GroundGroups.TryAddStrip("g", [new(0, 0), new(100, 0)], false, 0, 0));
        Assert.True(GroundGroups.TryAddStrip("g", [new(0, 0), new(100, 0)], false, float.NaN, 0));
        Assert.True(GroundGroups.TryAddArea("g", Disc(0, 0, 1e9f), 0));
        GroundGroups.TryAddArea("g", Disc(0, 0, 100), 0);
        Assert.InRange(Area(Assert.Single(GroundGroups.Merge()).Merged), DiscArea(100) * 0.999, DiscArea(100) * 1.001);
    }

    [Fact]
    public void MergeOf200ShapesIsCheap()
    {
        var random = new Random(49);
        void Fill()
        {
            GroundGroups.Clear();
            GroundGroups.Declare("g", Style());
            for (var i = 0; i < 200; i++)
            {
                var (x, y) = (random.NextSingle() * 3000, random.NextSingle() * 3000);
                if (i % 2 == 0) GroundGroups.TryAddArea("g", Disc(x, y, 30 + random.NextSingle() * 60, 48), 0);
                else GroundGroups.TryAddStrip("g", [new(x, y), new(x + random.NextSingle() * 400 - 200, y + random.NextSingle() * 400 - 200)], false, 20, 0);
            }
        }

        Fill();
        GroundGroups.Merge();
        var times = new double[20];
        for (var run = 0; run < times.Length; run++)
        {
            Fill();
            var watch = Stopwatch.StartNew();
            GroundGroups.Merge();
            times[run] = watch.Elapsed.TotalMilliseconds;
        }

        Array.Sort(times);
        Assert.True(times[10] < 4, $"median {times[10]:F2} ms");
    }
}
