using System.Collections.Generic;
using System.Drawing;
using EmberHUD.Testing;

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
}
