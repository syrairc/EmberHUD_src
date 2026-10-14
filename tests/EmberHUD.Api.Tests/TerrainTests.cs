using System.Collections;

namespace EmberHUD.Api.Tests;

public class TerrainTests
{
    private static Terrain Grid(int cols, int rows, params (int col, int row)[] walkable)
    {
        var bits = new BitArray(cols * rows);
        foreach (var (col, row) in walkable) bits[row * cols + col] = true;
        return new Terrain(cols, rows, 10f, Vector2.Zero, bits);
    }

    [Fact]
    public void IsWalkableReadsTheBitmapRowMajor()
    {
        var terrain = Grid(3, 2, (2, 1));
        Assert.True(terrain.IsWalkable(2, 1));
        Assert.False(terrain.IsWalkable(1, 1));
        Assert.False(terrain.IsWalkable(2, 0));
    }

    [Theory]
    [InlineData(-1, 0)]
    [InlineData(0, -1)]
    [InlineData(3, 0)]
    [InlineData(0, 2)]
    public void OutsideTheGridIsNotWalkable(int col, int row) => Assert.False(Grid(3, 2, (0, 0), (0, 1)).IsWalkable(col, row));

    [Fact]
    public void BitmapShorterThanTheGridNeverThrows()
    {
        var terrain = new Terrain(4, 4, 10f, Vector2.Zero, new BitArray(3));
        Assert.False(terrain.IsWalkable(3, 3));
    }

    [Fact]
    public void ZeroSizedGridIsNeverWalkable()
    {
        var terrain = new Terrain(0, 0, 10f, Vector2.Zero, new BitArray(0));
        Assert.False(terrain.IsWalkable(0, 0));
    }

    [Fact]
    public void CellCenterIsOriginPlusHalfACell() =>
        Assert.Equal(new Vector2(125, 215), new Terrain(4, 4, 10f, new Vector2(100, 200), new BitArray(16)).CellCenter(2, 1));

    [Fact]
    public void ThePredicateIsOnlyAskedAboutCellsOnTheGrid()
    {
        var terrain = Grid(2, 2, (0, 0), (1, 0), (0, 1), (1, 1));
        var asked = new List<(int Col, int Row)>();
        var lines = terrain.Outline((col, row) =>
        {
            asked.Add((col, row));
            return true;
        });
        Assert.NotEmpty(lines);
        Assert.All(asked, cell => Assert.True(cell.Col is >= 0 and < 2 && cell.Row is >= 0 and < 2, $"asked about {cell}"));
    }

    [Fact]
    public void IsWalkableAsThePredicateGivesTheSameOutline()
    {
        var terrain = Grid(4, 4, (1, 1), (2, 1), (1, 2), (0, 3), (3, 0));
        Assert.Equivalent(terrain.Outline(), terrain.Outline(terrain.IsWalkable));
    }

    [Fact]
    public void TheOriginMovesTheOutline()
    {
        var bits = new BitArray(1);
        bits[0] = true;
        var line = new Terrain(1, 1, 10f, new Vector2(100, 200), bits).Outline().Single();

        Assert.True(SameLoop(line.Points, new(100, 200), new(110, 200), new(110, 210), new(100, 210)));
    }

    [Theory]
    [InlineData(0f)]
    [InlineData(-5f)]
    [InlineData(float.NaN)]
    [InlineData(float.PositiveInfinity)]
    [InlineData(float.NegativeInfinity)]
    public void ACellSizeThatIsNotPositiveHasNoOutline(float size)
    {
        var bits = new BitArray(1);
        bits[0] = true;

        Assert.Empty(new Terrain(1, 1, size, Vector2.Zero, bits).Outline());
    }

    [Fact]
    public void ZeroSizedGridHasNoOutline() => Assert.Empty(new Terrain(0, 0, 10f, Vector2.Zero, new BitArray(0)).Outline());

    [Fact]
    public void BitmapShorterThanTheGridNeverThrowsAndTheMissingCellsAreBlocked()
    {
        var bits = new BitArray(2);
        bits[0] = true;
        bits[1] = true;

        var line = new Terrain(4, 4, 10f, Vector2.Zero, bits).Outline().Single();

        Assert.True(SameLoop(line.Points, new(0, 0), new(20, 0), new(20, 10), new(0, 10)));
    }

    private static bool SameLoop(Vector2[] points, params Vector2[] corners) => points.Length == corners.Length && corners.All(points.Contains);

    [Fact]
    public void ALoneCellIsOneClosedSquareWalkedCornerToCorner()
    {
        var line = Grid(3, 3, (1, 1)).Outline().Single();

        Assert.True(line.Closed);
        Assert.True(SameLoop(line.Points, new(10, 10), new(20, 10), new(20, 20), new(10, 20)));
        for (var i = 0; i < 4; i++) Assert.Equal(10f, Vector2.Distance(line.Points[i], line.Points[(i + 1) % 4]));
    }

    [Fact]
    public void TwoLoopsTouchingAtACornerStayClosedAndKeepTheSharedCorner()
    {
        var lines = Grid(2, 2, (0, 0), (1, 1)).Outline();

        Assert.Equal(2, lines.Length);
        Assert.All(lines, line => Assert.True(line.Closed));
        Assert.Equal(8, lines.Sum(line => line.Points.Length));
        Assert.Equal(2, lines.SelectMany(line => line.Points).Count(point => point == new Vector2(10, 10)));
    }

    [Fact]
    public void EndpointsThatDifferByRoundingStillJoin()
    {
        var bits = new BitArray(3);
        bits.SetAll(true);
        var line = new Terrain(3, 1, 10f, new Vector2(0.1f, 0.3f), bits).Outline().Single();

        Assert.True(line.Closed);
        Assert.Equal(4, line.Points.Length);
    }

    [Fact]
    public void NoWalkableCellsGiveNoLines() => Assert.Empty(Grid(3, 3).Outline());

    [Fact]
    public void AStraightRunOfCellsIsOneLongSide()
    {
        var line = Grid(7, 3, (1, 1), (2, 1), (3, 1), (4, 1), (5, 1)).Outline().Single();

        Assert.True(line.Closed);
        Assert.True(SameLoop(line.Points, new(10, 10), new(60, 10), new(60, 20), new(10, 20)));
    }

    [Fact]
    public void ACornerKeepsItsPointAndAnLShapeKeepsSix()
    {
        var line = Grid(4, 4, (1, 1), (2, 1), (1, 2)).Outline().Single();

        Assert.True(SameLoop(line.Points, new(10, 10), new(30, 10), new(30, 20), new(20, 20), new(20, 30), new(10, 30)));
    }

    [Fact]
    public void APredicateOutlineCoversOnlyTheCellsItKeeps()
    {
        var terrain = Grid(4, 3, (1, 1), (2, 1));
        var line = terrain.Outline((col, row) => terrain.IsWalkable(col, row) && col == 1).Single();

        Assert.True(SameLoop(line.Points, new(10, 10), new(20, 10), new(20, 20), new(10, 20)));
    }

    [Fact]
    public void AHoleInsideComesOutAsItsOwnClosedLoop()
    {
        var all = Enumerable.Range(0, 9).Select(i => (i % 3, i / 3)).Where(cell => cell != (1, 1)).ToArray();
        var lines = Grid(3, 3, all).Outline();

        Assert.Equal(2, lines.Length);
        Assert.All(lines, line => Assert.True(line.Closed));
    }
}
