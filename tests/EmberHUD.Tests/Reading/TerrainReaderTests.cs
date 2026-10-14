using EmberHUD.Host.Memory;
using EmberHUD.Host;

namespace EmberHUD.Tests.Reading;

public class TerrainReaderTests
{
    private static readonly byte[] FourByThree = [1, 0, 2, 1, 0, 0, 1, 1, 0, 3, 0, 1];

    [Fact]
    public void ReadsWalkableFromClearanceAndTheGeometryFields()
    {
        var world = new FakeWorld();
        world.SetGrid(4, 3, FourByThree, cellSize: 32, originX: -64, originY: 128);
        var terrain = new TerrainReader(world.Memory).Read(unchecked((nint)world.Scene));
        Assert.NotNull(terrain);
        Assert.Equal((4, 3), (terrain.Cols, terrain.Rows));
        Assert.Equal(32f, terrain.CellSize);
        Assert.Equal(new Vector2(-64, 128), terrain.Origin);
        for (var row = 0; row < 3; row++)
            for (var col = 0; col < 4; col++)
                Assert.Equal(FourByThree[row * 4 + col] >= 1, terrain.IsWalkable(col, row));
    }

    [Fact]
    public void CellIsWalkableOnlyWhenBothClearanceBytesArePositive()
    {
        var world = new FakeWorld();
        world.SetGrid(4, 1, [1, 1, 0, 5], secondClearance: [1, 0, 1, 3]);
        var terrain = new TerrainReader(world.Memory).Read(unchecked((nint)world.Scene))!;
        Assert.Equal([true, false, false, true], Enumerable.Range(0, 4).Select(col => terrain.IsWalkable(col, 0)));
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
        world.SetGrid(4, 3, FourByThree, originX: 64);
        reader.Read(unchecked((nint)world.Scene));
        Assert.Equal(12, reader.Generation);
    }

    [Fact]
    public void SameGeometryReturnsTheSameInstanceAndChangedGeometryANewOne()
    {
        var world = new FakeWorld();
        world.SetGrid(4, 3, FourByThree);
        var reader = new TerrainReader(world.Memory);
        var first = reader.Read(unchecked((nint)world.Scene));
        Assert.Same(first, reader.Read(unchecked((nint)world.Scene)));
        Assert.Equal(1, reader.Generation);

        world.SetGrid(4, 3, FourByThree, cellSize: 64);
        var second = reader.Read(unchecked((nint)world.Scene));
        Assert.NotSame(first, second);
        Assert.Equal(2, reader.Generation);
    }

    [Theory]
    [InlineData(5, 3, 32, 0, 0)]
    [InlineData(4, 4, 32, 0, 0)]
    [InlineData(4, 3, 64, 0, 0)]
    [InlineData(4, 3, 32, 8, 0)]
    [InlineData(4, 3, 32, 0, 8)]
    public void AnyChangeInDimensionsOrGeometryRebuilds(int cols, int rows, int cellSize, double originX, double originY)
    {
        var world = new FakeWorld();
        world.SetGrid(4, 3, FourByThree);
        var reader = new TerrainReader(world.Memory);
        reader.Read(unchecked((nint)world.Scene));
        world.SetGrid(cols, rows, Enumerable.Repeat((byte)1, cols * rows).ToArray(), cellSize, originX: originX, originY: originY);
        reader.Read(unchecked((nint)world.Scene));
        Assert.Equal(2, reader.Generation);
    }

    [Fact]
    public void DoubleBufferedGridSwapsDoNotRebuildOrBumpGeneration()
    {
        var world = new FakeWorld();
        var sceneAt = (nint)world.Scene;
        world.SetGrid(4, 3, FourByThree);
        world.Memory.TryRead(sceneAt + Offsets.SceneGeometry, out long geometryA);
        world.Memory.TryRead(sceneAt + Offsets.SceneGridModule, out long moduleA);
        world.SetGrid(4, 3, FourByThree);
        world.Memory.TryRead(sceneAt + Offsets.SceneGeometry, out long geometryB);
        world.Memory.TryRead(sceneAt + Offsets.SceneGridModule, out long moduleB);
        Assert.NotEqual(moduleA, moduleB);

        var reader = new TerrainReader(world.Memory);
        var first = reader.Read(sceneAt);
        for (var i = 0; i < 4; i++)
        {
            var (geometry, module) = i % 2 == 0 ? (geometryB, moduleB) : (geometryA, moduleA);
            world.Memory.Write(sceneAt + Offsets.SceneGeometry, geometry);
            world.Memory.Write(sceneAt + Offsets.SceneGridModule, module);
            Assert.Same(first, reader.Read(sceneAt));
        }

        Assert.Equal(1, reader.Generation);
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
        world.Memory.Write(world.GridAddress + Offsets.HybridCells, 0x7000_0000_0000L);
        Assert.Null(reader.Read(unchecked((nint)world.Scene)));
        Assert.Equal(0, reader.Generation);
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
    public void ZeroBlockWidthGivesNoTerrain()
    {
        var world = new FakeWorld();
        var allWalkable = Enumerable.Repeat((byte)1, 16).ToArray();
        world.SetGrid(4, 4, allWalkable, hybrid: (0, 2, 2, [0, 1, 0, 0]));
        Assert.Null(new TerrainReader(world.Memory).Read(unchecked((nint)world.Scene)));
    }

    private static bool ReadCells(FakeWorld world, TerrainReader reader, long cells)
    {
        world.Memory.Reads.Clear();
        reader.Read(unchecked((nint)world.Scene));
        return world.Memory.Reads.Any(read => read.Address == unchecked((nint)cells));
    }

    [Fact]
    public void BadCellsPointerIsNotRetriedForTheSameGridKey()
    {
        var world = new FakeWorld();
        world.SetGrid(4, 3, FourByThree);
        const long garbage = 0x7000_0000_0000L;
        world.Memory.Write(world.GridAddress + Offsets.HybridCells, garbage);
        var reader = new TerrainReader(world.Memory);

        Assert.True(ReadCells(world, reader, garbage));
        Assert.False(ReadCells(world, reader, garbage));
        Assert.Null(reader.Read(unchecked((nint)world.Scene)));
    }

    [Fact]
    public void ALatchedKeyIsRetriedEveryFewReadsSoAnAreaThatFinishesLoadingGetsTerrain()
    {
        var world = new FakeWorld();
        world.SetGrid(4, 3, FourByThree);
        const long garbage = 0x7000_0000_0000L;
        world.Memory.Write(world.GridAddress + Offsets.HybridCells, garbage);
        var reader = new TerrainReader(world.Memory);
        Assert.Null(reader.Read(unchecked((nint)world.Scene)));
        world.SetGrid(4, 3, FourByThree);

        for (var i = 1; i < TerrainReader.RetryEveryReads; i++)
            Assert.Null(reader.Read(unchecked((nint)world.Scene)));

        Assert.NotNull(reader.Read(unchecked((nint)world.Scene)));
    }

    [Fact]
    public void ANewGridKeyRetriesAfterABadCellsRead()
    {
        var world = new FakeWorld();
        world.SetGrid(4, 3, FourByThree);
        const long garbage = 0x7000_0000_0000L;
        world.Memory.Write(world.GridAddress + Offsets.HybridCells, garbage);
        var reader = new TerrainReader(world.Memory);
        ReadCells(world, reader, garbage);

        world.SetGrid(4, 3, FourByThree, originX: 64);
        world.Memory.Write(world.GridAddress + Offsets.HybridCells, garbage);

        Assert.True(ReadCells(world, reader, garbage));
    }

    [Fact]
    public void LosingTheGridClearsTheBadKeyLatch()
    {
        var world = new FakeWorld();
        world.SetGrid(4, 3, FourByThree);
        const long garbage = 0x7000_0000_0000L;
        world.Memory.Write(world.GridAddress + Offsets.HybridCells, garbage);
        var reader = new TerrainReader(world.Memory);
        reader.Read(unchecked((nint)world.Scene));
        world.Memory.Write(world.Scene + Offsets.SceneGridModule, 0L);
        reader.Read(unchecked((nint)world.Scene));

        world.SetGrid(4, 3, FourByThree);

        Assert.NotNull(reader.Read(unchecked((nint)world.Scene)));
    }
}
