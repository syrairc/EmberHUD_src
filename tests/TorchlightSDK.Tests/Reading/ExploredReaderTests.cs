using System.Numerics;
using TorchlightSDK.Host;
using TorchlightSDK.Host.Reading;

namespace TorchlightSDK.Tests.Reading;

public class ExploredReaderTests
{
    private static readonly Vector3[] Two = [new(1, 2, 3), new(4, 5, 6)];

    private static (FakeWorld World, FakeMinimap Minimap, ExploredReader Explored, Func<ExploredTrail?> Read) Setup()
    {
        var world = new FakeWorld();
        var minimap = world.AddMinimap(0f);
        world.SetLevel(110, 0);
        var reader = new ExploredReader(world.Memory);
        return (world, minimap, reader, () => reader.Read((nint)minimap.Widget, (nint)world.Scene));
    }

    [Fact]
    public void TheCurrentLevelsRecordGivesItsPointsAndHalfTheBrushTimesTheMapSide()
    {
        var (world, minimap, _, read) = Setup();
        world.SetExplored(minimap, 0.4f, 1000, ("109_0", [new(9, 9, 9)]), ("110_0", Two));

        var trail = read();

        Assert.Equal([new Vector2(1, 2), new Vector2(4, 5)], trail!.Points);
        Assert.Equal(200f, trail.Radius, 3);
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

        Assert.Equal([new Vector2(1, 2), new Vector2(4, 5), new Vector2(7, 8)], second!.Points);
        Assert.Equal(2, first!.Points.Length);
    }

    [Fact]
    public void AShorterRecordStartsOver()
    {
        var (world, minimap, _, read) = Setup();
        world.SetExplored(minimap, 0.4f, 1000, ("110_0", [.. Two, new(7, 8, 9)]));
        read();

        world.SetExplored(minimap, 0.4f, 1000, ("110_0", [new(20, 30, 0)]));

        Assert.Equal([new Vector2(20, 30)], read()!.Points);
    }

    [Fact]
    public void ANewLevelReadsItsOwnRecord()
    {
        var (world, minimap, _, read) = Setup();
        world.SetExplored(minimap, 0.4f, 1000, ("110_0", Two), ("111_2", [new(50, 60, 0)]));
        read();

        world.SetLevel(111, 2);

        Assert.Equal([new Vector2(50, 60)], read()!.Points);
    }

    [Fact]
    public void AFreeSlotWithTheSameKeyIsSkipped()
    {
        var (world, minimap, _, read) = Setup();
        world.SetExplored(minimap, 0.4f, 1000, null, ("110_0", Two));

        Assert.Equal(2, read()!.Points.Length);
    }

    [Fact]
    public void AnEmptyRecordIsAnEmptyTrailNotNull()
    {
        var (world, minimap, _, read) = Setup();
        world.SetExplored(minimap, 0.4f, 1000, ("110_0", []));

        Assert.Empty(read()!.Points);
    }

    [Fact]
    public void NoRecordNoSaveObjectOrNoWidgetGiveNull()
    {
        var (world, minimap, explored, read) = Setup();
        Assert.Null(read());

        world.SetExplored(minimap, 0.4f, 1000, ("109_0", Two));
        Assert.Null(read());
        Assert.Null(explored.Read(0, (nint)world.Scene));
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

        Assert.Equal(Offsets.MaxExploredPoints, read()!.Points.Length);
    }
}
