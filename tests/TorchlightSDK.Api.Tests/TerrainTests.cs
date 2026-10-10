using System.Collections;

namespace TorchlightSDK.Api.Tests;

public class TerrainTests
{
    private static Terrain Grid(int cols, int rows, bool shift, params (int col, int row)[] walkable)
    {
        var bits = new BitArray(cols * rows);
        foreach (var (col, row) in walkable) bits[row * cols + col] = true;
        return new Terrain(cols, rows, 10f, new Vector2(100, 200), shift, bits);
    }

    [Fact]
    public void IsWalkableReadsTheBitmapRowMajor()
    {
        var terrain = Grid(3, 2, false, (2, 1));
        Assert.True(terrain.IsWalkable(2, 1));
        Assert.False(terrain.IsWalkable(1, 1));
        Assert.False(terrain.IsWalkable(2, 0));
    }

    [Theory]
    [InlineData(-1, 0)]
    [InlineData(0, -1)]
    [InlineData(3, 0)]
    [InlineData(0, 2)]
    public void OutsideTheGridIsNotWalkable(int col, int row) => Assert.False(Grid(3, 2, false, (0, 0), (0, 1)).IsWalkable(col, row));

    [Fact]
    public void BitmapShorterThanTheGridNeverThrows()
    {
        var terrain = new Terrain(4, 4, 10f, Vector2.Zero, false, new BitArray(3));
        Assert.False(terrain.IsWalkable(3, 3));
    }

    [Fact]
    public void ZeroSizedGridIsNeverWalkable()
    {
        var terrain = new Terrain(0, 0, 10f, Vector2.Zero, false, new BitArray(0));
        Assert.False(terrain.IsWalkable(0, 0));
    }

    [Fact]
    public void CellCenterIsOriginPlusHalfACellWithoutShift() =>
        Assert.Equal(new Vector2(125, 215), Grid(4, 4, false).CellCenter(2, 1));

    [Fact]
    public void OddRowsShiftByHalfACellWhenEnabled()
    {
        var terrain = Grid(4, 4, true);
        Assert.Equal(new Vector2(125, 205), terrain.CellCenter(2, 0));
        Assert.Equal(new Vector2(130, 215), terrain.CellCenter(2, 1));
    }
}
