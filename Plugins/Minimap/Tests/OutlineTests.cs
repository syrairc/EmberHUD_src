using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using EmberHUD;
using EmberHUD.Testing;

namespace Minimap;

public sealed class OutlineTests
{
    static readonly Terrain Grid = Scene.Cells(4, 4);

    static bool SameRun(Vector2[] points, params Vector2[] expected) => points.SequenceEqual(expected) || points.Reverse().SequenceEqual(expected);

    [Test]
    public void ALoneCellIsOneClosedSquareWalkedCornerToCorner()
    {
        var terrain = Scene.OneCell();
        var line = Outline.Join(terrain.Walls(), terrain).Single();

        Check.That(line.Closed, "closed");
        Check.Equal(4, line.Points.Length);
        foreach (var corner in new[] { new Vector2(10, 10), new Vector2(20, 10), new Vector2(20, 20), new Vector2(10, 20) }) Check.That(line.Points.Contains(corner), $"has {corner}");
        for (var i = 0; i < 4; i++) Check.Equal(10f, Vector2.Distance(line.Points[i], line.Points[(i + 1) % 4]), $"step {i}");
    }

    [Test]
    public void SharedEndpointsChainAnOpenRunEndToEnd()
    {
        List<WallSegment> edges = [new(10, 0, 20, 0), new(0, 0, 10, 0), new(20, 0, 20, 10)];
        var line = Outline.Join(edges, Grid).Single();

        Check.That(!line.Closed, "open");
        Check.That(SameRun(line.Points, new(0, 0), new(20, 0), new(20, 10)), string.Join(" ", line.Points));
    }

    [Test]
    public void TwoLoopsTouchingAtACornerUseEveryEdgeOnceAndStayClosed()
    {
        var terrain = Scene.Cells(2, 2, (0, 0), (1, 1));
        var edges = terrain.Walls();
        var lines = Outline.Join(edges, terrain);

        Check.Equal(8, edges.Count);
        Check.That(lines.All(line => line.Closed), "all closed");
        Check.Equal(8, lines.Sum(line => line.Points.Length));
        Check.Equal(2, lines.SelectMany(line => line.Points).Count(point => point == new Vector2(10, 10)));
    }

    [Test]
    public void EndpointsThatDifferByRoundingStillJoin()
    {
        var terrain = new Terrain(4, 4, 10f, new Vector2(0.1f, 0.3f), new BitArray(16));
        List<WallSegment> edges = [new(0.1f, 0.3f, 10.1f, 0.3f), new(10.100001f, 0.3f, 20.1f, 0.3f)];

        Check.Equal(2, Outline.Join(edges, terrain).Single().Points.Length);
    }

    [Test]
    public void NoEdgesGiveNoLines() => Check.Equal(0, Outline.Join([], Grid).Length);

    [Test]
    public void AStraightRunOfCellsIsOneLongSide()
    {
        var terrain = Scene.Cells(7, 3, (1, 1), (2, 1), (3, 1), (4, 1), (5, 1));
        var line = Outline.Join(terrain.Walls(), terrain).Single();

        Check.That(line.Closed, "closed");
        Check.Equal(4, line.Points.Length);
        foreach (var corner in new[] { new Vector2(10, 10), new Vector2(60, 10), new Vector2(60, 20), new Vector2(10, 20) }) Check.That(line.Points.Contains(corner), $"has {corner}");
    }

    [Test]
    public void ACornerKeepsItsPointAndAnLShapeKeepsSix()
    {
        var terrain = Scene.Cells(4, 4, (1, 1), (2, 1), (1, 2));
        var line = Outline.Join(terrain.Walls(), terrain).Single();

        Check.Equal(6, line.Points.Length);
        foreach (var corner in new[] { new Vector2(10, 10), new Vector2(30, 10), new Vector2(30, 20), new Vector2(20, 20), new Vector2(20, 30), new Vector2(10, 30) }) Check.That(line.Points.Contains(corner), $"has {corner}");
    }

    [Test]
    public void AClosedLoopThatStartsMidSideStillComesOutAsFourCorners()
    {
        List<WallSegment> edges =
        [
            new(10, 0, 20, 0), new(20, 0, 30, 0), new(30, 0, 30, 10), new(30, 10, 20, 10),
            new(20, 10, 10, 10), new(10, 10, 0, 10), new(0, 10, 0, 0), new(0, 0, 10, 0),
        ];
        var line = Outline.Join(edges, Grid).Single();

        Check.That(line.Closed, "closed");
        Check.Equal(4, line.Points.Length);
        foreach (var corner in new[] { new Vector2(0, 0), new Vector2(30, 0), new Vector2(30, 10), new Vector2(0, 10) }) Check.That(line.Points.Contains(corner), $"has {corner}");
    }

    [Test]
    public void ARunThatDoublesBackOnItselfKeepsTheSpikeTip()
    {
        List<WallSegment> edges = [new(0, 10, 0, 0), new(0, 0, 10, 0), new(10, 0, 0, 0)];
        var line = Outline.Join(edges, Grid).Single();

        Check.That(!line.Closed, "open");
        Check.That(SameRun(line.Points, new(0, 10), new(0, 0), new(10, 0), new(0, 0)), string.Join(" ", line.Points));
    }
}
