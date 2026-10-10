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
