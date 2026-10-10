using TorchlightSDK.Host;
using TorchlightSDK.Host.Reading;

namespace TorchlightSDK.Tests.Reading;

public class TerrainReaderTests
{
    private static readonly byte[] FourByThree = [1, 0, 2, 1, 0, 0, 1, 1, 0, 3, 0, 1];

    [Fact]
    public void ReadsWalkableFromClearanceAndTheGeometryFields()
    {
        var world = new FakeWorld();
        world.SetGrid(4, 3, FourByThree, cellSize: 32, oddRowShift: 16, originX: -64, originY: 128);
        var terrain = new TerrainReader(world.Memory).Read(unchecked((nint)world.Scene));
        Assert.NotNull(terrain);
        Assert.Equal((4, 3), (terrain.Cols, terrain.Rows));
        Assert.Equal(32f, terrain.CellSize);
        Assert.Equal(new Vector2(-64, 128), terrain.Origin);
        Assert.True(terrain.OddRowShift);
        for (var row = 0; row < 3; row++)
            for (var col = 0; col < 4; col++)
                Assert.Equal(FourByThree[row * 4 + col] >= 1, terrain.IsWalkable(col, row));
    }

    [Fact]
    public void ZeroShiftMeansNoOddRowShift()
    {
        var world = new FakeWorld();
        world.SetGrid(4, 3, FourByThree, oddRowShift: 0);
        Assert.False(new TerrainReader(world.Memory).Read(unchecked((nint)world.Scene))!.OddRowShift);
    }

    [Fact]
    public void SeededGenerationCountsUpFromTheSeed()
    {
        var world = new FakeWorld();
        world.SetGrid(4, 3, FourByThree);
        var reader = new TerrainReader(world.Memory, 10);
        Assert.Equal(10, reader.Generation);
        reader.Read(unchecked((nint)world.Scene));
        Assert.Equal(11, reader.Generation);
        world.SetGrid(4, 3, FourByThree);
        reader.Read(unchecked((nint)world.Scene));
        Assert.Equal(12, reader.Generation);
    }

    [Fact]
    public void SameGridReturnsTheSameInstanceAndANewGridANewOne()
    {
        var world = new FakeWorld();
        world.SetGrid(4, 3, FourByThree);
        var reader = new TerrainReader(world.Memory);
        var first = reader.Read(unchecked((nint)world.Scene));
        Assert.Same(first, reader.Read(unchecked((nint)world.Scene)));
        Assert.Equal(1, reader.Generation);

        world.SetGrid(4, 3, FourByThree);
        var second = reader.Read(unchecked((nint)world.Scene));
        Assert.NotSame(first, second);
        Assert.Equal(2, reader.Generation);
    }

    [Fact]
    public void UniformBlocksReadAsUnwalkable()
    {
        var world = new FakeWorld();
        var allWalkable = Enumerable.Repeat((byte)1, 16).ToArray();
        world.SetGrid(4, 4, allWalkable, hybrid: (2, 2, 2, [0, 1, 0, 0]));
        var terrain = new TerrainReader(world.Memory).Read(unchecked((nint)world.Scene))!;
        Assert.False(terrain.IsWalkable(2, 0));
        Assert.False(terrain.IsWalkable(3, 1));
        Assert.True(terrain.IsWalkable(0, 0));
        Assert.True(terrain.IsWalkable(2, 2));
    }

    [Theory]
    [InlineData(-1, 3)]
    [InlineData(0, 3)]
    [InlineData(4, 0)]
    [InlineData(100000, 100000)]
    public void AbsurdDimensionsGiveNoTerrain(int cols, int rows)
    {
        var world = new FakeWorld();
        world.SetGrid(4, 3, FourByThree);
        var grid = world.GridAddress;
        world.Memory.Write(grid + Offsets.HybridCols, cols);
        world.Memory.Write(grid + Offsets.HybridRows, rows);
        var reader = new TerrainReader(world.Memory);
        Assert.Null(reader.Read(unchecked((nint)world.Scene)));
        Assert.Equal(0, reader.Generation);
    }

    [Fact]
    public void BadCellSizeOrOriginGivesNoTerrain()
    {
        var world = new FakeWorld();
        world.SetGrid(4, 3, FourByThree, cellSize: 0);
        Assert.Null(new TerrainReader(world.Memory).Read(unchecked((nint)world.Scene)));
        world.SetGrid(4, 3, FourByThree, originX: double.NaN);
        Assert.Null(new TerrainReader(world.Memory).Read(unchecked((nint)world.Scene)));
    }

    [Fact]
    public void MissingGridModuleGivesNoTerrain()
    {
        var world = new FakeWorld();
        Assert.Null(new TerrainReader(world.Memory).Read(unchecked((nint)world.Scene)));
    }

    [Fact]
    public void LosingTheGridClearsItSoTheNextReadRebuilds()
    {
        var world = new FakeWorld();
        world.SetGrid(4, 3, FourByThree);
        var reader = new TerrainReader(world.Memory);
        var first = reader.Read(unchecked((nint)world.Scene));
        world.Memory.Write(world.Scene + Offsets.SceneGridModule, 0L);
        Assert.Null(reader.Read(unchecked((nint)world.Scene)));
        world.SetGrid(4, 3, FourByThree);
        Assert.NotSame(first, reader.Read(unchecked((nint)world.Scene)));
        Assert.Equal(2, reader.Generation);
    }

    [Fact]
    public void FailedBuildLeavesGenerationAlone()
    {
        var world = new FakeWorld();
        world.SetGrid(4, 3, FourByThree);
        var reader = new TerrainReader(world.Memory);
        Assert.NotNull(reader.Read(unchecked((nint)world.Scene)));
        world.Memory.Write(world.GridAddress + Offsets.HybridCells, 0x7000_0000_0000L);
        Assert.Null(reader.Read(unchecked((nint)world.Scene)));
        Assert.Equal(1, reader.Generation);
    }

    [Fact]
    public void HugeBlocksPerRowGivesNoTerrainInsteadOfThrowing()
    {
        var world = new FakeWorld();
        var allWalkable = Enumerable.Repeat((byte)1, 16).ToArray();
        world.SetGrid(4, 4, allWalkable, hybrid: (2, 2, int.MaxValue, [0, 1, 0, 0]));
        var reader = new TerrainReader(world.Memory);
        Assert.Null(reader.Read(unchecked((nint)world.Scene)));
        Assert.Equal(0, reader.Generation);
    }

    [Fact]
    public void NonFiniteShiftGivesNoTerrain()
    {
        var world = new FakeWorld();
        world.SetGrid(4, 3, FourByThree, oddRowShift: double.NaN);
        Assert.Null(new TerrainReader(world.Memory).Read(unchecked((nint)world.Scene)));
    }

    [Fact]
    public void ZeroBlockWidthGivesNoTerrain()
    {
        var world = new FakeWorld();
        var allWalkable = Enumerable.Repeat((byte)1, 16).ToArray();
        world.SetGrid(4, 4, allWalkable, hybrid: (0, 2, 2, [0, 1, 0, 0]));
        Assert.Null(new TerrainReader(world.Memory).Read(unchecked((nint)world.Scene)));
    }
}
