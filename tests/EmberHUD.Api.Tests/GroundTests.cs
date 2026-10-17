using System.Collections.Generic;
using System.Drawing;
using EmberHUD.Testing;
using Hexa.NET.ImGui;

namespace EmberHUD.Api.Tests;

public sealed class GroundTests
{
    static readonly Vector2 Screen = new(1920, 1080);
    static readonly Camera Forward = new(Vector3.Zero, Vector3.Zero, 90);
    static readonly Camera Looking = new(new Vector3(-1000, 0, 1000), new Vector3(-45, 0, 0), 90);

    static int Vertices(Camera? camera, Action draw, Player? player = null)
    {
        TestGame.Set(new GameState { Camera = camera, Player = player });
        return TestFrame.Vertices(draw);
    }

    static (Vector2 Pos, uint Col)[] Drawn(Camera? camera, Action draw)
    {
        TestGame.Set(new GameState { Camera = camera });
        (Vector2, uint)[] result = [];
        TestFrame.Run(() =>
        {
            var list = ImGui.GetBackgroundDrawList();
            var before = list.VtxBuffer.Size;
            draw();
            result = [.. Enumerable.Range(before, list.VtxBuffer.Size - before).Select(i => (list.VtxBuffer[i].Pos, list.VtxBuffer[i].Col))];
        });
        return result;
    }

    static float Alpha(uint colour) => colour >> 24;

    static float MaxAlpha(IEnumerable<(Vector2 Pos, uint Col)> vertices) => vertices.Max(v => Alpha(v.Col));

    static List<Vector2[]> Runs(Camera camera, bool closed, params Vector3[] world)
    {
        var runs = new List<Vector2[]>();
        Ground.ProjectRuns(camera, Screen, world, closed, run => runs.Add(run.ToArray()));
        return runs;
    }

    [Fact]
    public void ASegmentInFrontIsKept()
    {
        Vector3 a = new(100, 0, 0), b = new(500, 50, 0);

        Assert.True(Ground.ClipSegment(Forward, ref a, ref b));

        Assert.Equal((new Vector3(100, 0, 0), new Vector3(500, 50, 0)), (a, b));
    }

    [Fact]
    public void ASegmentBehindTheCameraIsDropped()
    {
        Vector3 a = new(-100, 0, 0), b = new(-500, 50, 0);
        Assert.False(Ground.ClipSegment(Forward, ref a, ref b));
    }

    [Fact]
    public void ASegmentCrossingTheNearPlaneIsCutThere()
    {
        Vector3 a = new(500, 0, 0), b = new(-500, 0, 0);

        Assert.True(Ground.ClipSegment(Forward, ref a, ref b));

        Assert.Equal(500, a.X);
        Assert.Equal(Ground.Near, b.X, 0.001f);
    }

    [Fact]
    public void ACutAtTheStartMovesTheStartPoint()
    {
        Vector3 a = new(-500, 0, 0), b = new(500, 0, 0);

        Assert.True(Ground.ClipSegment(Forward, ref a, ref b));

        Assert.Equal(Ground.Near, a.X, 0.001f);
        Assert.Equal(500, b.X);
    }

    [Fact]
    public void APolygonHalfBehindTheCameraIsCutToTheFront()
    {
        Vector3[] square = [new(-500, -100, 0), new(500, -100, 0), new(500, 100, 0), new(-500, 100, 0)];
        List<Vector3> clipped = [];

        Ground.ClipPolygon(Forward, square, clipped);

        Vector3[] expected = [new(Ground.Near, -100, 0), new(500, -100, 0), new(500, 100, 0), new(Ground.Near, 100, 0)];
        Assert.Equal(expected.Length, clipped.Count);
        for (var i = 0; i < expected.Length; i++)
        {
            Assert.Equal(expected[i].X, clipped[i].X, 0.001f);
            Assert.Equal(expected[i].Y, clipped[i].Y, 0.001f);
        }
    }

    [Fact]
    public void APolygonEntirelyBehindTheCameraIsEmpty()
    {
        List<Vector3> clipped = [];

        Ground.ClipPolygon(Forward, [new(-500, -100, 0), new(-400, -100, 0), new(-400, 100, 0)], clipped);

        Assert.Empty(clipped);
    }

    [Fact]
    public void APathInFrontIsOneRun()
    {
        var runs = Runs(Forward, false, new Vector3(100, 0, 0), new Vector3(300, 50, 0), new Vector3(500, 0, 0));

        Assert.Equal(3, Assert.Single(runs).Length);
    }

    [Fact]
    public void APathThatLeavesAndReturnsInFrontSplitsIntoTwoRuns()
    {
        var runs = Runs(Forward, false, new Vector3(100, 0, 0), new Vector3(-100, 0, 0), new Vector3(-100, 100, 0), new Vector3(100, 100, 0));

        Assert.Equal(2, runs.Count);
    }

    [Fact]
    public void AClosedOutlineAddsTheClosingSegment()
    {
        var open = Runs(Forward, false, new Vector3(100, 0, 0), new Vector3(300, 50, 0), new Vector3(300, -50, 0));
        var closed = Runs(Forward, true, new Vector3(100, 0, 0), new Vector3(300, 50, 0), new Vector3(300, -50, 0));

        Assert.Equal(open.Single().Length + 1, closed.Single().Length);
    }

    [Fact]
    public void ARectangleHasFourCornersTurnedAroundItsCentre()
    {
        Span<Vector2> corners = stackalloc Vector2[4];

        Ground.RectCorners(new Vector2(10, 20), new Vector2(4, 2), 90, corners);

        Vector2[] expected = [new(11, 18), new(11, 22), new(9, 22), new(9, 18)];
        for (var i = 0; i < 4; i++)
        {
            Assert.Equal(expected[i].X, corners[i].X, 0.001f);
            Assert.Equal(expected[i].Y, corners[i].Y, 0.001f);
        }
    }

    [Fact]
    public void HeightDefaultsToThePlayersGround()
    {
        TestGame.Set(new GameState { Player = new Player(new Vector3(0, 0, 42), Vector2.UnitX) });
        Assert.Equal(42, Ground.HeightOrPlayer(null));
        Assert.Equal(7, Ground.HeightOrPlayer(7));

        TestGame.Set(GameState.Empty);
        Assert.Equal(0, Ground.HeightOrPlayer(null));
    }

    [Fact]
    public void ACircleInViewDraws() => Assert.True(Vertices(Looking, () => Ground.Circle(Vector2.Zero, 200, Color.Red)) > 0);

    [Fact]
    public void ADotInViewDrawsFarFewerVerticesThanACircleFilled()
    {
        var dot = Vertices(Looking, () => Ground.Dot(Vector2.Zero, 20, Color.Red));
        var circle = Vertices(Looking, () => Ground.CircleFilled(Vector2.Zero, 20, Color.Red));

        Assert.True(dot > 0);
        Assert.True(dot * 3 < circle);
    }

    [Fact]
    public void ADotWithoutACameraOrBehindItDrawsNothing()
    {
        Assert.Equal(0, Vertices(null, () => Ground.Dot(Vector2.Zero, 20, Color.Red)));
        Assert.Equal(0, Vertices(Forward, () => Ground.Dot(new Vector2(-1000, 0), 20, Color.Red, z: 0)));
    }

    [Fact]
    public void NothingDrawsWithoutACamera()
    {
        Assert.Equal(0, Vertices(null, () => Ground.Circle(Vector2.Zero, 200, Color.Red)));
        Assert.Equal(0, Vertices(null, () => Ground.RectFilled(Vector2.Zero, new Vector2(200), 0, Color.Red)));
        Assert.Equal(0, Vertices(null, () => Ground.Line(Vector2.Zero, new Vector2(500, 0), Color.Red)));
    }

    [Fact]
    public void ACircleBehindTheCameraDrawsNothing() =>
        Assert.Equal(0, Vertices(Forward, () => Ground.Circle(new Vector2(-1000, 0), 100, Color.Red, z: 0)));

    [Fact]
    public void ACircleStradlingTheNearPlaneStillDraws()
    {
        Assert.True(Vertices(Forward, () => Ground.Circle(new Vector2(0, 0), 400, Color.Red, z: -50)) > 0);
        Assert.True(Vertices(Forward, () => Ground.CircleFilled(new Vector2(0, 0), 400, Color.Red, z: -50)) > 0);
    }

    [Fact]
    public void EveryShapeDrawsInAFrame()
    {
        var count = Vertices(Looking, () =>
        {
            Ground.Circle(Vector2.Zero, 100, Color.Red, 2);
            Ground.CircleFilled(Vector2.Zero, 100, Color.Red);
            Ground.Rect(Vector2.Zero, new Vector2(100, 50), 30, Color.Red, 2);
            Ground.RectFilled(Vector2.Zero, new Vector2(100, 50), 30, Color.Red);
            Ground.Line(Vector2.Zero, new Vector2(300, 100), Color.Red, 3);
            Ground.Polyline([Vector2.Zero, new Vector2(100, 0), new Vector2(100, 100)], Color.Red, 2, true);
            Ground.PolygonFilled([Vector2.Zero, new Vector2(100, 0), new Vector2(100, 100)], Color.Red);
        });

        Assert.True(count > 0);
    }

    [Fact]
    public void TooFewPointsDrawNothing()
    {
        Assert.Equal(0, Vertices(Looking, () => Ground.Polyline([Vector2.Zero], Color.Red)));
        Assert.Equal(0, Vertices(Looking, () => Ground.PolygonFilled([Vector2.Zero, Vector2.One], Color.Red)));
    }

    [Fact]
    public void AGradientLineFadesTowardsTheEnd()
    {
        var vertices = Drawn(Forward, () => Ground.GradientLine(new Vector2(500, -100), new Vector2(500, 100), Color.FromArgb(255, Color.Red), Color.FromArgb(0, Color.Red), 3, 0));

        var quarter = vertices.Length / 4;
        Assert.True(MaxAlpha(vertices.Take(quarter)) > MaxAlpha(vertices.TakeLast(quarter)));
    }

    [Fact]
    public void ADashedLineDrawsOneSegmentPerDash()
    {
        var solid = Vertices(Forward, () => Ground.Line(new Vector2(500, 0), new Vector2(500, 10), Color.Red, 2, 0));
        var dashed = Vertices(Forward, () => Ground.DashedLine(new Vector2(500, 0), new Vector2(500, 100), Color.Red, 2, 10, 10, 0, 0));

        Assert.Equal(solid * 5, dashed);
    }

    [Fact]
    public void ThePhaseSlidesTheDashesAlongTheLine()
    {
        float[] Xs(float phase) => [.. Drawn(Forward, () => Ground.DashedLine(new Vector2(500, 0), new Vector2(500, 100), Color.Red, 2, 10, 10, phase, 0)).Select(v => v.Pos.X)];

        var (start, shifted) = (Xs(0), Xs(10));

        Assert.True(shifted.Min() > start.Min() + 10);
        Assert.True(shifted.Max() > start.Max() + 10);
    }

    [Fact]
    public void ANegativePhaseSlidesTheDashesTheOtherWay()
    {
        float[] Xs(float phase) => [.. Drawn(Forward, () => Ground.DashedLine(new Vector2(500, 0), new Vector2(500, 100), Color.Red, 2, 10, 10, phase, 0)).Select(v => v.Pos.X)];

        Assert.True(Xs(-5).Min() < Xs(5).Min());
        Assert.True(Xs(-5).Max() > Xs(5).Max());
        Assert.Equal(Xs(10), Xs(-10));
        Assert.Equal(Xs(5), Xs(25));
        Assert.Equal(Xs(-5), Xs(15));
    }

    [Fact]
    public void ADashedLineWithoutALengthOrACameraDrawsNothing()
    {
        Assert.Equal(0, Vertices(null, () => Ground.DashedLine(Vector2.Zero, new Vector2(100, 0), Color.Red, 2, 10, 10)));
        Assert.Equal(0, Vertices(Looking, () => Ground.DashedLine(Vector2.Zero, Vector2.Zero, Color.Red, 2, 10, 10)));
        Assert.Equal(0, Vertices(Looking, () => Ground.DashedLine(Vector2.Zero, new Vector2(100, 0), Color.Red, 2, 0, 10)));
    }

    [Fact]
    public void TextLiesOnTheGround()
    {
        var drawn = false;
        var vertices = Drawn(Looking, () => drawn = Ground.Text("MMMMMMMM", Vector2.Zero, 100, 0, Color.White, 0));

        Assert.True(drawn);
        var centre = Looking.ToScreen(Vector3.Zero, Screen)!.Value;
        Assert.Equal(centre.X, (vertices.Min(v => v.Pos.X) + vertices.Max(v => v.Pos.X)) / 2, 10f);
        Assert.InRange(centre.Y, vertices.Min(v => v.Pos.Y), vertices.Max(v => v.Pos.Y));
        var rows = vertices.OrderBy(v => v.Pos.Y).ToArray();
        var quarter = rows.Length / 4;
        float Span(IEnumerable<(Vector2 Pos, uint Col)> part) => part.Max(v => v.Pos.X) - part.Min(v => v.Pos.X);
        Assert.True(Span(rows.Take(quarter)) < Span(rows.TakeLast(quarter)));
    }

    [Fact]
    public void TextRasterisesAtItsScreenSize()
    {
        Vector2[] Uvs(float worldHeight)
        {
            TestGame.Set(new GameState { Camera = Looking });
            Vector2[] result = [];
            TestFrame.Run(() =>
            {
                var list = ImGui.GetBackgroundDrawList();
                var before = list.VtxBuffer.Size;
                Ground.Text("M", Vector2.Zero, worldHeight, 0, Color.White, 0);
                result = [.. Enumerable.Range(before, list.VtxBuffer.Size - before).Select(i => list.VtxBuffer[i].Uv)];
            });
            return result;
        }

        Assert.NotEqual(Uvs(100), Uvs(400));
    }

    [Fact]
    public void TextBehindTheCameraDrawsNothing()
    {
        var drawn = true;

        Assert.Empty(Drawn(Forward, () => drawn = Ground.Text("Hello", new Vector2(-1000, 0), 100, 0, Color.White, 0)));
        Assert.False(drawn);
    }

    [Fact]
    public void TextCrossingTheNearPlaneAddsNothing()
    {
        TestGame.Set(new GameState { Camera = Forward });
        TestFrame.Run(() =>
        {
            var list = ImGui.GetBackgroundDrawList();
            Draw.Text("before", new Vector2(10, 10), Color.White);
            var (vtx, idx, cmds) = (list.VtxBuffer.Size, list.IdxBuffer.Size, list.CmdBuffer.Size);

            Assert.False(Ground.Text("Hello", Vector2.Zero, 4000, 0, Color.White, 0));

            Assert.Equal((vtx, idx, cmds), (list.VtxBuffer.Size, list.IdxBuffer.Size, list.CmdBuffer.Size));
            Draw.Text("after", new Vector2(10, 40), Color.White);
            Assert.True(list.VtxBuffer.Size > vtx);
        });
    }

    [Fact]
    public void TextWithoutACameraOrContentDrawsNothing()
    {
        Assert.Equal(0, Vertices(null, () => Assert.False(Ground.Text("Hello", Vector2.Zero, 100, 0, Color.White, 0))));
        Assert.Equal(0, Vertices(Looking, () => Assert.False(Ground.Text("", Vector2.Zero, 100, 0, Color.White, 0))));
    }
}
