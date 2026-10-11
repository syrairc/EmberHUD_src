using System.Collections;

namespace TorchlightSDK.Api.Tests;

public class WallExtractorTests
{
    private static Terrain Grid(int cols, int rows, bool shift, params (int col, int row)[] walkable)
    {
        var bits = new BitArray(cols * rows);
        foreach (var (col, row) in walkable) bits[row * cols + col] = true;
        return new Terrain(cols, rows, 10f, Vector2.Zero, shift, bits);
    }

    private static void Has(List<WallSegment> walls, float ax, float ay, float bx, float by) =>
        Assert.Contains(new WallSegment(ax, ay, bx, by), walls);

    private static void Lacks(List<WallSegment> walls, float ax, float ay, float bx, float by) =>
        Assert.DoesNotContain(new WallSegment(ax, ay, bx, by), walls);

    [Fact]
    public void IsWalkableAsThePredicateGivesTheSameWalls()
    {
        var terrain = Grid(4, 4, true, (1, 1), (2, 1), (1, 2), (0, 3), (3, 0));
        Assert.Equal(WallExtractor.Extract(terrain), WallExtractor.Extract(terrain, terrain.IsWalkable));
    }

    [Fact]
    public void APredicateOutlinesOnlyTheCellsItKeeps()
    {
        var terrain = Grid(4, 3, false, (1, 1), (2, 1));
        var walls = WallExtractor.Extract(terrain, (col, row) => terrain.IsWalkable(col, row) && col == 1);
        Assert.Equal(4, walls.Count);
        Has(walls, 20, 10, 20, 20);
        Lacks(walls, 30, 10, 30, 20);
    }

    [Fact]
    public void ThePredicateIsOnlyAskedAboutCellsOnTheGrid()
    {
        var terrain = Grid(2, 2, true, (0, 0), (1, 0), (0, 1), (1, 1));
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
    public void NoWalkableCellsHaveNoWalls() => Assert.Empty(WallExtractor.Extract(Grid(3, 3, false)));

    [Fact]
    public void ALoneCellOnAnUnshiftedGridHasFourFullEdges()
    {
        var walls = WallExtractor.Extract(Grid(3, 3, false, (1, 1)));

        Assert.Equal(4, walls.Count);
        Has(walls, 10, 10, 10, 20);
        Has(walls, 20, 10, 20, 20);
        Has(walls, 10, 10, 20, 10);
        Has(walls, 10, 20, 20, 20);
    }

    [Fact]
    public void TwoNeighboursShareNoWall()
    {
        var walls = WallExtractor.Extract(Grid(4, 3, false, (1, 1), (2, 1)));

        Assert.Equal(6, walls.Count);
        Lacks(walls, 20, 10, 20, 20);
    }

    [Fact]
    public void ALoneEvenCellOnAShiftedGridHasSixEdges()
    {
        var walls = WallExtractor.Extract(Grid(3, 3, true, (1, 0)));

        Assert.Equal(6, walls.Count);
        Has(walls, 10, 0, 10, 10);
        Has(walls, 20, 0, 20, 10);
        Has(walls, 10, 0, 15, 0);
        Has(walls, 15, 0, 20, 0);
        Has(walls, 10, 10, 15, 10);
        Has(walls, 15, 10, 20, 10);
    }

    [Fact]
    public void AnOddRowIsShiftedHalfACell()
    {
        var walls = WallExtractor.Extract(Grid(3, 3, true, (1, 1)));

        Assert.Equal(6, walls.Count);
        Has(walls, 15, 10, 15, 20);
        Has(walls, 25, 10, 25, 20);
        Has(walls, 15, 10, 20, 10);
        Has(walls, 20, 20, 25, 20);
    }

    [Fact]
    public void ShiftedNeighboursAcrossRowsCloseTheirSharedHalf()
    {
        var walls = WallExtractor.Extract(Grid(3, 2, true, (1, 0), (0, 1)));

        Assert.Equal(10, walls.Count);
        Lacks(walls, 10, 10, 15, 10);
        Has(walls, 15, 10, 20, 10);
        Has(walls, 5, 10, 10, 10);
    }

    private static void HalfEdges(List<WallSegment> walls, float x0, float y0, bool topLeft, bool topRight, bool bottomLeft, bool bottomRight)
    {
        var mid = x0 + 5;
        var x1 = x0 + 10;
        var y1 = y0 + 10;
        Check(walls, topLeft, x0, y0, mid, y0);
        Check(walls, topRight, mid, y0, x1, y0);
        Check(walls, bottomLeft, x0, y1, mid, y1);
        Check(walls, bottomRight, mid, y1, x1, y1);
    }

    private static void Check(List<WallSegment> walls, bool present, float ax, float ay, float bx, float by)
    {
        if (present) Has(walls, ax, ay, bx, by);
        else Lacks(walls, ax, ay, bx, by);
    }

    [Fact]
    public void AnOddCellOnlyClosesItsLeftHalvesAgainstTheCellsDirectlyAboveAndBelow()
    {
        var walls = WallExtractor.Extract(Grid(3, 3, true, (1, 1), (1, 0), (1, 2)));

        Assert.Equal(14, walls.Count);
        Has(walls, 15, 10, 15, 20);
        Has(walls, 25, 10, 25, 20);
        HalfEdges(walls, 15, 10, false, true, false, true);
    }

    [Fact]
    public void AnOddCellOnlyClosesItsRightHalvesAgainstTheCellsAboveAndBelowToTheRight()
    {
        var walls = WallExtractor.Extract(Grid(3, 3, true, (1, 1), (2, 0), (2, 2)));

        Assert.Equal(14, walls.Count);
        Has(walls, 15, 10, 15, 20);
        Has(walls, 25, 10, 25, 20);
        HalfEdges(walls, 15, 10, true, false, true, false);
    }

    [Fact]
    public void AnEvenCellOnlyClosesItsLeftHalvesAgainstTheCellsAboveAndBelowToTheLeft()
    {
        var walls = WallExtractor.Extract(Grid(3, 5, true, (1, 2), (0, 1), (0, 3)));

        Assert.Equal(14, walls.Count);
        Has(walls, 10, 20, 10, 30);
        Has(walls, 20, 20, 20, 30);
        HalfEdges(walls, 10, 20, false, true, false, true);
    }

    [Fact]
    public void AnEvenCellOnlyClosesItsRightHalvesAgainstTheCellsDirectlyAboveAndBelow()
    {
        var walls = WallExtractor.Extract(Grid(3, 5, true, (1, 2), (1, 1), (1, 3)));

        Assert.Equal(14, walls.Count);
        Has(walls, 10, 20, 10, 30);
        Has(walls, 20, 20, 20, 30);
        HalfEdges(walls, 10, 20, true, false, true, false);
    }

    [Fact]
    public void TheOriginMovesAShiftedOddRowToo()
    {
        var bits = new BitArray(9);
        bits[4] = true;

        var walls = WallExtractor.Extract(new Terrain(3, 3, 10f, new Vector2(100, 200), true, bits));

        Assert.Equal(6, walls.Count);
        Has(walls, 115, 210, 115, 220);
        Has(walls, 125, 210, 125, 220);
        Has(walls, 115, 210, 120, 210);
        Has(walls, 120, 220, 125, 220);
    }

    [Fact]
    public void TheOriginMovesTheWalls()
    {
        var bits = new BitArray(1);
        bits[0] = true;

        var walls = WallExtractor.Extract(new Terrain(1, 1, 10f, new Vector2(100, 200), false, bits));

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

        Assert.Empty(WallExtractor.Extract(new Terrain(1, 1, size, Vector2.Zero, false, bits)));
    }

    [Fact]
    public void ZeroSizedGridHasNoWalls() => Assert.Empty(WallExtractor.Extract(new Terrain(0, 0, 10f, Vector2.Zero, false, new BitArray(0))));

    [Fact]
    public void BitmapShorterThanTheGridNeverThrowsAndTheMissingCellsAreBlocked()
    {
        var bits = new BitArray(2);
        bits[0] = true;
        bits[1] = true;

        var walls = WallExtractor.Extract(new Terrain(4, 4, 10f, Vector2.Zero, false, bits));

        Assert.Equal(6, walls.Count);
    }
}
