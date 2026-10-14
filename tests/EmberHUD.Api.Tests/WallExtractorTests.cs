using System.Collections;

namespace EmberHUD.Api.Tests;

public class WallExtractorTests
{
    private static Terrain Grid(int cols, int rows, params (int col, int row)[] walkable)
    {
        var bits = new BitArray(cols * rows);
        foreach (var (col, row) in walkable) bits[row * cols + col] = true;
        return new Terrain(cols, rows, 10f, Vector2.Zero, bits);
    }

    private static void Has(List<WallSegment> walls, float ax, float ay, float bx, float by) =>
        Assert.Contains(new WallSegment(ax, ay, bx, by), walls);

    private static void Lacks(List<WallSegment> walls, float ax, float ay, float bx, float by) =>
        Assert.DoesNotContain(new WallSegment(ax, ay, bx, by), walls);

    [Fact]
    public void IsWalkableAsThePredicateGivesTheSameWalls()
    {
        var terrain = Grid(4, 4, (1, 1), (2, 1), (1, 2), (0, 3), (3, 0));
        Assert.Equal(WallExtractor.Extract(terrain), WallExtractor.Extract(terrain, terrain.IsWalkable));
    }

    [Fact]
    public void APredicateOutlinesOnlyTheCellsItKeeps()
    {
        var terrain = Grid(4, 3, (1, 1), (2, 1));
        var walls = WallExtractor.Extract(terrain, (col, row) => terrain.IsWalkable(col, row) && col == 1);
        Assert.Equal(4, walls.Count);
        Has(walls, 20, 10, 20, 20);
        Lacks(walls, 30, 10, 30, 20);
    }

    [Fact]
    public void ThePredicateIsOnlyAskedAboutCellsOnTheGrid()
    {
        var terrain = Grid(2, 2, (0, 0), (1, 0), (0, 1), (1, 1));
        var asked = new List<(int Col, int Row)>();
        var walls = WallExtractor.Extract(terrain, (col, row) =>
        {
            asked.Add((col, row));
            return true;
        });
        Assert.NotEmpty(walls);
        Assert.All(asked, cell => Assert.True(cell.Col is >= 0 and < 2 && cell.Row is >= 0 and < 2, $"asked about {cell}"));
    }

    [Fact]
    public void NoWalkableCellsHaveNoWalls() => Assert.Empty(WallExtractor.Extract(Grid(3, 3)));

    [Fact]
    public void ALoneCellHasFourFullEdges()
    {
        var walls = WallExtractor.Extract(Grid(3, 3, (1, 1)));

        Assert.Equal(4, walls.Count);
        Has(walls, 10, 10, 10, 20);
        Has(walls, 20, 10, 20, 20);
        Has(walls, 10, 10, 20, 10);
        Has(walls, 10, 20, 20, 20);
    }

    [Fact]
    public void TwoNeighboursShareNoWall()
    {
        var walls = WallExtractor.Extract(Grid(4, 3, (1, 1), (2, 1)));

        Assert.Equal(6, walls.Count);
        Lacks(walls, 20, 10, 20, 20);
    }

    [Fact]
    public void NeighboursAboveAndBelowShareNoWall()
    {
        var walls = WallExtractor.Extract(Grid(3, 4, (1, 1), (1, 2)));

        Assert.Equal(6, walls.Count);
        Lacks(walls, 10, 20, 20, 20);
    }

    [Fact]
    public void TheOriginMovesTheWalls()
    {
        var bits = new BitArray(1);
        bits[0] = true;

        var walls = WallExtractor.Extract(new Terrain(1, 1, 10f, new Vector2(100, 200), bits));

        Has(walls, 100, 200, 100, 210);
    }

    [Theory]
    [InlineData(0f)]
    [InlineData(-5f)]
    [InlineData(float.NaN)]
    [InlineData(float.PositiveInfinity)]
    [InlineData(float.NegativeInfinity)]
    public void ACellSizeThatIsNotPositiveHasNoWalls(float size)
    {
        var bits = new BitArray(1);
        bits[0] = true;

        Assert.Empty(WallExtractor.Extract(new Terrain(1, 1, size, Vector2.Zero, bits)));
    }

    [Fact]
    public void ZeroSizedGridHasNoWalls() => Assert.Empty(WallExtractor.Extract(new Terrain(0, 0, 10f, Vector2.Zero, new BitArray(0))));

    [Fact]
    public void BitmapShorterThanTheGridNeverThrowsAndTheMissingCellsAreBlocked()
    {
        var bits = new BitArray(2);
        bits[0] = true;
        bits[1] = true;

        var walls = WallExtractor.Extract(new Terrain(4, 4, 10f, Vector2.Zero, bits));

        Assert.Equal(6, walls.Count);
    }
}
