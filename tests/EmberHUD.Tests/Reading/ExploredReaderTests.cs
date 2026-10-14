using EmberHUD.Host.Memory;
using EmberHUD.Host;

namespace EmberHUD.Tests;

public class ExploredReaderTests
{
    private static readonly Vector3[] Two = [new(1, 2, 3), new(4, 5, 6)];

    private static nint Geometry(FakeWorld world) =>
        world.Memory.TryReadPtr(world.Scene + Offsets.SceneGeometry, out var geometry) ? geometry : 0;

    private static (FakeWorld World, FakeMinimap Minimap, ExploredReader Explored, Func<ExploredTrail?> Read) Setup()
    {
        var world = new FakeWorld();
        var minimap = world.AddMinimap(0f);
        world.SetLevel(110, 0);
        long now = 0;
        var reader = new ExploredReader(world.Memory, () => now);
        return (world, minimap, reader, () =>
        {
            now += Offsets.ExploredRefreshMilliseconds;
            return reader.Read(minimap.Widget, Geometry(world), 0);
        });
    }

    private static Vector3[] Points(int count) => [.. Enumerable.Range(1, count).Select(i => new Vector3(i, i + 1, 0))];

    [Fact]
    public void ASteadyReadFindsTheRecordByItsCachedAddress()
    {
        var world = new FakeWorld();
        var minimap = world.AddMinimap(0f);
        world.SetLevel(110, 0);
        world.SetExplored(minimap, 0.4f, 1000, ("109_0", [new(9, 9, 9)]), ("110_0", Two));
        long now = 0;
        var reader = new ExploredReader(world.Memory, () => now);
        var geometry = Geometry(world);
        reader.Read(minimap.Widget, geometry, 0);
        now = Offsets.ExploredRefreshMilliseconds;
        world.Memory.Reads.Clear();

        var trail = reader.Read(minimap.Widget, geometry, 0);

        Assert.Equal(2, trail!.Count);
        Assert.DoesNotContain(world.Memory.Reads, r => r.Length == 2 * Offsets.SaveRecordStride);
        Assert.True(world.Memory.Reads.Count <= 11, $"{world.Memory.Reads.Count} reads");
    }

    [Fact]
    public void ReadsInsideTheRefreshIntervalAreFreeUntilTheGenerationChanges()
    {
        var world = new FakeWorld();
        var minimap = world.AddMinimap(0f);
        world.SetLevel(110, 0);
        world.SetExplored(minimap, 0.4f, 1000, ("110_0", Two));
        long now = 0;
        var reader = new ExploredReader(world.Memory, () => now);
        var geometry = Geometry(world);
        var first = reader.Read(minimap.Widget, geometry, 0);
        now = Offsets.ExploredRefreshMilliseconds - 1;
        world.Memory.Reads.Clear();

        Assert.Same(first, reader.Read(minimap.Widget, geometry, 0));
        Assert.Empty(world.Memory.Reads);

        world.SetExplored(minimap, 0.4f, 1000, ("110_0", [.. Two, new(7, 8, 9)]));
        Assert.Same(first, reader.Read(minimap.Widget, geometry, 0));
        Assert.Equal(3, reader.Read(minimap.Widget, geometry, 1)!.Count);
    }

    [Fact]
    public void AnOlderTrailKeepsItsPointsWhenTheRecordGrowsAndWhenItRestarts()
    {
        var (world, minimap, _, read) = Setup();
        world.SetExplored(minimap, 0.4f, 1000, ("110_0", Two));
        var first = read()!;
        var firstPoints = first.PointArray();

        world.SetExplored(minimap, 0.4f, 1000, ("110_0", [.. Two, new(7, 8, 9), new(10, 11, 12)]));
        var grown = read()!;
        world.SetExplored(minimap, 0.4f, 1000, ("110_0", [new(20, 30, 0)]));
        var restarted = read()!;

        Assert.Equal(firstPoints, first.PointArray());
        Assert.Equal([new Vector2(1, 2), new Vector2(4, 5), new Vector2(7, 8), new Vector2(10, 11)], grown.PointArray());
        Assert.Equal([new Vector2(20, 30)], restarted.PointArray());
    }

    [Fact]
    public void AppendingWithinTheCapacityKeepsTheSameBuffer()
    {
        var (world, minimap, _, read) = Setup();
        world.SetExplored(minimap, 0.4f, 1000, ("110_0", Points(3)));
        var three = read()!;
        world.SetExplored(minimap, 0.4f, 1000, ("110_0", Points(4)));
        var four = read()!;
        world.SetExplored(minimap, 0.4f, 1000, ("110_0", Points(5)));
        var five = read()!;

        Assert.NotSame(three.Buffer, four.Buffer);
        Assert.Same(four.Buffer, five.Buffer);
        Assert.Equal([3, 4, 5], new[] { three.Count, four.Count, five.Count });
    }

    [Fact]
    public void AMovedRecordTableIsFoundAgain()
    {
        var (world, minimap, _, read) = Setup();
        world.SetExplored(minimap, 0.4f, 1000, ("109_0", [new(9, 9, 9)]), ("110_0", Two));
        Assert.Equal(2, read()!.Count);

        world.SetExplored(minimap, 0.4f, 1000, ("110_0", [.. Two, new(7, 8, 9)]), ("109_0", [new(9, 9, 9)]));

        Assert.Equal(3, read()!.Count);
    }

    [Fact]
    public void ARecordReplacedByAnotherKeyIsNotFollowed()
    {
        var (world, minimap, _, read) = Setup();
        world.SetExplored(minimap, 0.4f, 1000, ("110_0", Two));
        Assert.Equal(2, read()!.Count);

        world.SetExplored(minimap, 0.4f, 1000, ("111_0", Points(9)));

        Assert.Equal(2, read()!.Count);
    }

    [Fact]
    public void TheCurrentLevelsRecordGivesItsPointsAndHalfTheBrushTimesTheMapSide()
    {
        var (world, minimap, _, read) = Setup();
        world.SetExplored(minimap, 0.4f, 1000, ("109_0", [new(9, 9, 9)]), ("110_0", Two));

        var trail = read();

        Assert.Equal([new Vector2(1, 2), new Vector2(4, 5)], trail!.PointArray());
        Assert.Equal(200f, trail!.Radius, 3);
    }

    [Fact]
    public void NewPointsAreAppendedAndAnUnchangedRecordGivesTheSameTrail()
    {
        var (world, minimap, _, read) = Setup();
        world.SetExplored(minimap, 0.4f, 1000, ("110_0", Two));
        var first = read();
        Assert.Same(first, read());

        world.SetExplored(minimap, 0.4f, 1000, ("110_0", [.. Two, new(7, 8, 9)]));
        var second = read();

        Assert.Equal([new Vector2(1, 2), new Vector2(4, 5), new Vector2(7, 8)], second!.PointArray());
        Assert.Equal(2, first!.PointArray().Length);
    }

    [Fact]
    public void AShorterRecordStartsOver()
    {
        var (world, minimap, _, read) = Setup();
        world.SetExplored(minimap, 0.4f, 1000, ("110_0", [.. Two, new(7, 8, 9)]));
        read();

        world.SetExplored(minimap, 0.4f, 1000, ("110_0", [new(20, 30, 0)]));

        Assert.Equal([new Vector2(20, 30)], read()!.PointArray());
    }

    [Fact]
    public void ANewLevelReadsItsOwnRecord()
    {
        var (world, minimap, _, read) = Setup();
        world.SetExplored(minimap, 0.4f, 1000, ("110_0", Two), ("111_2", [new(50, 60, 0)]));
        read();

        world.SetLevel(111, 2);

        Assert.Equal([new Vector2(50, 60)], read()!.PointArray());
    }

    [Fact]
    public void AFreeSlotWithTheSameKeyIsSkipped()
    {
        var (world, minimap, _, read) = Setup();
        world.SetExplored(minimap, 0.4f, 1000, null, ("110_0", Two));

        Assert.Equal(2, read()!.PointArray().Length);
    }

    [Fact]
    public void AnEmptyRecordIsAnEmptyTrailNotNull()
    {
        var (world, minimap, _, read) = Setup();
        world.SetExplored(minimap, 0.4f, 1000, ("110_0", []));

        Assert.Empty(read()!.PointArray());
    }

    [Fact]
    public void NoRecordNoSaveObjectOrNoWidgetGiveNull()
    {
        var (world, minimap, explored, read) = Setup();
        Assert.Null(read());

        world.SetExplored(minimap, 0.4f, 1000, ("109_0", Two));
        Assert.Null(read());
        Assert.Null(explored.Read(0, Geometry(world), 0));
    }

    [Theory]
    [InlineData(0f, 1000)]
    [InlineData(float.NaN, 1000)]
    [InlineData(0.4f, 0)]
    public void ABadRadiusGivesNull(float brush, int side)
    {
        var (world, minimap, _, read) = Setup();
        world.SetExplored(minimap, brush, side, ("110_0", Two));

        Assert.Null(read());
    }

    [Fact]
    public void AReadThatBreaksLaterKeepsTheLastTrail()
    {
        var (world, minimap, _, read) = Setup();
        world.SetExplored(minimap, 0.4f, 1000, ("110_0", Two));
        var good = read();

        world.Memory.Write(minimap.Widget + Offsets.MinimapSaveObject, 0L);

        Assert.Same(good, read());
    }

    [Fact]
    public void PointsStopAtTheCap()
    {
        var (world, minimap, _, read) = Setup();
        world.SetExplored(minimap, 0.4f, 1000, ("110_0", new Vector3[Offsets.MaxExploredPoints + 10]));

        Assert.Equal(Offsets.MaxExploredPoints, read()!.PointArray().Length);
    }
}
