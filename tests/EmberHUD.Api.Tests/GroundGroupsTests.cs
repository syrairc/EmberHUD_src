using System.Diagnostics;
using System.Drawing;
using Clipper2Lib;
using EmberHUD.Testing;
using Hexa.NET.ImGui;

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

    static readonly Camera Forward = new(Vector3.Zero, Vector3.Zero, 90);
    static readonly Camera Looking = new(new Vector3(-1000, 0, 1000), new Vector3(-45, 0, 0), 90);

    static (Vector2 Pos, uint Col)[] Drawn(Camera? camera, Action draw)
    {
        TestGame.Set(new GameState { Camera = camera });
        (Vector2, uint)[] result = [];
        TestFrame.Run(() =>
        {
            var list = ImGui.GetBackgroundDrawList();
            var before = list.VtxBuffer.Size;
            draw();
            Ground.FlushGroups();
            result = [.. Enumerable.Range(before, list.VtxBuffer.Size - before).Select(i => (list.VtxBuffer[i].Pos, list.VtxBuffer[i].Col))];
        });
        return result;
    }

    static void Annulus(string group)
    {
        for (var i = 0; i < 48; i++)
        {
            var (a, b) = (MathF.Tau * i / 48, MathF.Tau * (i + 1) / 48);
            Vector2 P(float angle, float r) => new(500 + r * MathF.Cos(angle), r * MathF.Sin(angle));
            GroundGroups.TryAddArea(group, [P(a, 200), P(b, 200), P(b, 100), P(a, 100)], 0);
        }
    }

    [Fact]
    public void TriangulatedAreaMatchesMergedArea()
    {
        GroundGroups.Clear();
        GroundGroups.Declare("g", Style());
        Annulus("g");
        var merged = Assert.Single(GroundGroups.Merge()).Merged;
        var triangles = new List<Vector2>();
        GroundGroups.Triangulate(merged, triangles);
        Assert.NotEmpty(triangles);
        Assert.Equal(0, triangles.Count % 3);
        double area = 0;
        for (var i = 0; i < triangles.Count; i += 3)
        {
            var (a, b, c) = (triangles[i], triangles[i + 1], triangles[i + 2]);
            area += Math.Abs((double)((b.X - a.X) * (c.Y - a.Y) - (c.X - a.X) * (b.Y - a.Y))) / 2;
        }

        var expected = Area(merged);
        Assert.InRange(area, expected * 0.995, expected * 1.005);
        GroundGroups.Clear();
    }

    [Fact]
    public void GroupedShapesDrawOnlyAtFlush()
    {
        GroundGroups.Clear();
        TestGame.Set(new GameState { Camera = Looking });
        int middle = -1, after = -1;
        TestFrame.Run(() =>
        {
            var list = ImGui.GetBackgroundDrawList();
            Ground.Group("g", Color.Red, Color.FromArgb(80, 0, 255, 0), 2);
            var start = list.VtxBuffer.Size;
            Ground.CircleFilled(new Vector2(0, 0), 100, Color.Green, 0, group: "g");
            middle = list.VtxBuffer.Size - start;
            Ground.FlushGroups();
            after = list.VtxBuffer.Size - start;
        });
        Assert.Equal(0, middle);
        Assert.True(after > 0);
    }

    [Fact]
    public void UndeclaredGroupDrawsLikeToday()
    {
        GroundGroups.Clear();
        var plain = Drawn(Looking, () => Ground.CircleFilled(new Vector2(0, 0), 100, Color.Green, 0)).Length;
        var nope = Drawn(Looking, () => Ground.CircleFilled(new Vector2(0, 0), 100, Color.Green, 0, group: "nope")).Length;
        Assert.True(plain > 0);
        Assert.Equal(plain, nope);
    }

    [Fact]
    public void MergedFillHasNoAntiAliasFringe()
    {
        GroundGroups.Clear();
        var drawn = Drawn(Looking, () =>
        {
            Ground.Group("g", Color.Red, Color.FromArgb(80, 0, 255, 0), 0);
            Ground.CircleFilled(new Vector2(0, 0), 100, Color.Green, 0, group: "g");
            Ground.CircleFilled(new Vector2(120, 0), 100, Color.Green, 0, group: "g");
        });
        Assert.NotEmpty(drawn);
        Assert.Equal(0, drawn.Length % 3);
        Assert.All(drawn, v => Assert.Equal(80u, v.Col >> 24));
    }

    static double ScreenArea((Vector2 Pos, uint Col)[] vertices)
    {
        double area = 0;
        for (var i = 0; i + 2 < vertices.Length; i += 3)
        {
            var (a, b, c) = (vertices[i].Pos, vertices[i + 1].Pos, vertices[i + 2].Pos);
            area += Math.Abs((double)((b.X - a.X) * (c.Y - a.Y) - (c.X - a.X) * (b.Y - a.Y))) / 2;
        }

        return area;
    }

    [Fact]
    public void OverlapIsNotDarker()
    {
        GroundGroups.Clear();
        var grouped = Drawn(Looking, () =>
        {
            Ground.Group("g", Color.Red, Color.FromArgb(80, 0, 255, 0), 0);
            Ground.CircleFilled(new Vector2(0, 0), 100, Color.Green, 0, group: "g");
            Ground.CircleFilled(new Vector2(50, 0), 100, Color.Green, 0, group: "g");
        });
        var stacked = Drawn(Looking, () =>
        {
            Ground.Group("a", Color.Red, Color.FromArgb(80, 0, 255, 0), 0);
            Ground.Group("b", Color.Red, Color.FromArgb(80, 0, 255, 0), 0);
            Ground.CircleFilled(new Vector2(0, 0), 100, Color.Green, 0, group: "a");
            Ground.CircleFilled(new Vector2(50, 0), 100, Color.Green, 0, group: "b");
        });
        Assert.Equal(80u, grouped.Max(v => v.Col >> 24));
        Assert.InRange(ScreenArea(grouped) / ScreenArea(stacked), 0.6, 0.72);
    }

    [Fact]
    public void MergedShapeBehindCameraDrawsNothing()
    {
        GroundGroups.Clear();
        var drawn = Drawn(Forward, () =>
        {
            Ground.Group("g", Color.Red, Color.FromArgb(80, 0, 255, 0), 2);
            Ground.CircleFilled(new Vector2(-500, 0), 100, Color.Green, 0, group: "g");
            Ground.Circle(new Vector2(-500, 50), 100, Color.Green, 2, 0, group: "g");
        });
        Assert.Empty(drawn);
    }

    [Fact]
    public void LineStartingBehindTheCameraStillGroups()
    {
        GroundGroups.Clear();
        var drawn = Drawn(Forward, () =>
        {
            Ground.Group("g", Color.Transparent, Color.Red, 0);
            Ground.Line(new Vector2(-500, 0), new Vector2(1000, 0), Color.Red, 4, 0, group: "g");
        });
        Assert.NotEmpty(drawn);
    }

    static bool NearRing((Vector2 Pos, uint Col)[] vertices, float radius)
    {
        for (var i = 0; i < 48; i++)
        {
            var angle = MathF.Tau * i / 48;
            var at = Looking.ToScreen(new Vector3(500 + radius * MathF.Cos(angle), radius * MathF.Sin(angle), 0), new Vector2(1920, 1080));
            if (at is { } p && vertices.Any(v => Vector2.Distance(v.Pos, p) < 3)) return true;
        }

        return false;
    }

    [Fact]
    public void BorderDrawsEveryRing()
    {
        GroundGroups.Clear();
        var drawn = Drawn(Looking, () =>
        {
            Ground.Group("g", Color.FromArgb(255, 255, 0, 0), Color.FromArgb(0, 0, 255, 0), 2);
            Annulus("g");
        });
        Assert.NotEmpty(drawn);
        Assert.All(drawn, v => Assert.Equal(255u, v.Col >> 24));
        Assert.True(NearRing(drawn, 200));
        Assert.True(NearRing(drawn, 100));
    }

    [Fact]
    public void SliverUnionStillFills()
    {
        GroundGroups.Clear();
        GroundGroups.Declare("g", Style());
        GroundGroups.TryAddArea("g", [new(40.03f, 20), new(50.01f, 20.01f), new(60.03f, 20.02f), new(70.02f, 20.03f)], 0);
        var merged = Assert.Single(GroundGroups.Merge()).Merged;
        Assert.NotEqual(TriangulateResult.success, Clipper.Triangulate(merged, 2, out _));
        GroundGroups.Clear();
        var drawn = Drawn(Looking, () =>
        {
            Ground.Group("g", Color.Red, Color.FromArgb(80, 0, 255, 0), 0);
            Ground.PolygonFilled([new(40.03f, 20), new(50.01f, 20.01f), new(60.03f, 20.02f), new(70.02f, 20.03f)], Color.Green, 0, group: "g");
        });
        Assert.NotEmpty(drawn);
    }

    [Fact]
    public void FlushWithoutCameraClears()
    {
        GroundGroups.Clear();
        var none = Drawn(null, () =>
        {
            Ground.Group("g", Color.Red, Color.FromArgb(80, 0, 255, 0), 2);
            Ground.CircleFilled(new Vector2(0, 0), 100, Color.Green, 0, group: "g");
        });
        Assert.Empty(none);
        Assert.Empty(Drawn(Looking, () => Ground.FlushGroups()));
    }

    [Fact]
    public void FlushWithNothingDeclaredDoesNotTouchImGui()
    {
        GroundGroups.Clear();
        TestGame.Set(new GameState { Camera = Looking });
        try { Ground.FlushGroups(); }
        finally { TestGame.Set(GameState.Empty); }
    }

    [Fact]
    public void LayerlessGroupDrawsInTheCurrentChannel()
    {
        GroundGroups.Clear();
        TestGame.Set(new GameState { Camera = Looking });
        var middle = -1;
        TestFrame.Run(() =>
        {
            Draw.SplitLayers();
            var list = ImGui.GetBackgroundDrawList();
            GroundGroups.Declare("front", new GroupStyle(Color.Red, Color.FromArgb(80, 0, 255, 0), 0, null, (int)DrawLayer.Front));
            GroundGroups.Declare("plain", new GroupStyle(Color.Red, Color.FromArgb(80, 0, 255, 0), 0, null, -1));
            Ground.CircleFilled(new Vector2(0, 0), 100, Color.Green, 0, group: "front");
            Ground.CircleFilled(new Vector2(300, 0), 100, Color.Green, 0, group: "plain");
            var before = list.IdxBuffer.Size;
            Ground.FlushGroups();
            middle = list.IdxBuffer.Size - before;
            Draw.MergeLayers();
        });
        Assert.True(middle > 0);
    }

    [Fact]
    public void GroupDeclaredInBackLayerFlushesIntoBack()
    {
        GroundGroups.Clear();
        TestGame.Set(new GameState { Camera = Looking });
        var middle = -1;
        TestFrame.Run(() =>
        {
            Draw.SplitLayers();
            var list = ImGui.GetBackgroundDrawList();
            using (Draw.Layer(DrawLayer.Back)) Ground.Group("back", Color.Red, Color.FromArgb(80, 0, 255, 0), 0);
            Ground.CircleFilled(new Vector2(0, 0), 100, Color.Green, 0, group: "back");
            var before = list.IdxBuffer.Size;
            Ground.FlushGroups();
            middle = list.IdxBuffer.Size - before;
            Draw.MergeLayers();
        });
        Assert.Equal(0, middle);
    }
}
