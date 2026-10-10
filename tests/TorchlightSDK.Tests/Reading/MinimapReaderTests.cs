using TorchlightSDK.Host;
using TorchlightSDK.Host.Reading;

namespace TorchlightSDK.Tests.Reading;

public class MinimapReaderTests
{
    private sealed class Clock
    {
        public long Now;
    }

    private static MinimapReader ReaderFor(FakeWorld world, Clock? clock = null) =>
        new(world.Memory, unchecked((nint)FakeWorld.ModuleBase), clock is null ? null : () => clock.Now);

    [Fact]
    public void ReadsTheRotateTurnsAsClockwiseDegrees()
    {
        var world = new FakeWorld();
        world.AddMinimap(0.25f);
        Assert.Equal(-90f, ReaderFor(world).ReadRotationDegrees());
    }

    [Theory]
    [InlineData(0f, 0f)]
    [InlineData(0.5f, -180f)]
    [InlineData(0.75f, 90f)]
    [InlineData(-0.25f, 90f)]
    [InlineData(1f, 0f)]
    [InlineData(1.25f, -90f)]
    [InlineData(0.125f, -45f)]
    public void NormalisesIntoMinus180To180(float turns, float expected)
    {
        var world = new FakeWorld();
        world.AddMinimap(turns);
        Assert.Equal(expected, ReaderFor(world).ReadRotationDegrees()!.Value, 3);
    }

    [Fact]
    public void FindsRotateWhereverItSitsInTheParameterArray()
    {
        var world = new FakeWorld();
        var minimap = world.AddMinimap(0f);
        world.WriteScalarParameters(minimap, ("MapResolution", 485f), ("Zoom", 0.8f), ("Other", 3f), ("Rotate", 0.25f));
        Assert.Equal(-90f, ReaderFor(world).ReadRotationDegrees());
    }

    [Fact]
    public void NoWidgetAtAllGivesNull()
    {
        Assert.Null(ReaderFor(new FakeWorld()).ReadRotationDegrees());
    }

    [Fact]
    public void ObjectsOfOtherClassesAreIgnored()
    {
        var world = new FakeWorld();
        world.AddMinimap(0.25f, className: "MiniMapOther_C", objectName: "MiniMap_C");
        world.AddObject(world.AddClass("Widget_C"), "MiniMap_C");
        Assert.Null(ReaderFor(world).ReadRotationDegrees());
    }

    [Fact]
    public void WideClassNamesAreRejected()
    {
        var world = new FakeWorld();
        world.AddObject(world.AddClass("MiniMap_C", wideName: true), "MiniMap_C");
        Assert.Null(ReaderFor(world).ReadRotationDegrees());
    }

    [Fact]
    public void DefaultObjectsAreIgnoredButTheInstanceIsFound()
    {
        var world = new FakeWorld();
        world.AddObject(world.AddClass("MiniMap_C"), "Default__MiniMap_C");
        Assert.Null(ReaderFor(world).ReadRotationDegrees());

        world.AddMinimap(0.25f);
        Assert.Equal(-90f, ReaderFor(world).ReadRotationDegrees());
    }

    [Fact]
    public void MissingRotateGivesNull()
    {
        var world = new FakeWorld();
        world.AddMinimap(null);
        Assert.Null(ReaderFor(world).ReadRotationDegrees());
    }

    [Theory]
    [InlineData(float.NaN)]
    [InlineData(float.PositiveInfinity)]
    [InlineData(float.NegativeInfinity)]
    public void NonFiniteRotateGivesNull(float value)
    {
        var world = new FakeWorld();
        world.AddMinimap(value);
        Assert.Null(ReaderFor(world).ReadRotationDegrees());
    }

    [Fact]
    public void TornMaterialPointersGiveNull()
    {
        var world = new FakeWorld();
        var minimap = world.AddMinimap(0.25f);
        world.Memory.Write(minimap.Widget + Offsets.MinimapWidgetMaterial, 0x7777_0000_0000L);
        Assert.Null(ReaderFor(world).ReadRotationDegrees());
        world.Memory.Write(minimap.Widget + Offsets.MinimapWidgetMaterial, 0L);
        Assert.Null(ReaderFor(world).ReadRotationDegrees());
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(65)]
    [InlineData(int.MaxValue)]
    public void AbsurdParameterCountGivesNull(int count)
    {
        var world = new FakeWorld();
        var minimap = world.AddMinimap(0.25f);
        world.Memory.Write(minimap.Material + Offsets.MaterialScalarParams + Offsets.TArrayNum, count);
        Assert.Null(ReaderFor(world).ReadRotationDegrees());
    }

    [Fact]
    public void UnmappedParameterArrayGivesNull()
    {
        var world = new FakeWorld();
        var minimap = world.AddMinimap(0.25f);
        world.Memory.Write(minimap.Material + Offsets.MaterialScalarParams, 0x7777_0000_0000L);
        Assert.Null(ReaderFor(world).ReadRotationDegrees());
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-5)]
    [InlineData(64 * 65536 + 1)]
    public void AbsurdObjectCountGivesNull(int count)
    {
        var world = new FakeWorld();
        world.AddMinimap(0.25f);
        world.Memory.Write(FakeWorld.ModuleBase + Offsets.GObjectsGlobalRva + Offsets.GObjectsNumElements, count);
        Assert.Null(ReaderFor(world).ReadRotationDegrees());
    }

    [Fact]
    public void UnmappedChunkTableGivesNull()
    {
        var world = new FakeWorld();
        world.AddMinimap(0.25f);
        world.Memory.Write(FakeWorld.ModuleBase + Offsets.GObjectsGlobalRva, 0x7777_0000_0000L);
        Assert.Null(ReaderFor(world).ReadRotationDegrees());
    }

    [Fact]
    public void FindsTheWidgetInALaterChunk()
    {
        var world = new FakeWorld();
        world.AddObject(world.AddClass("Other_C"), "Thing");
        var minimap = world.AddMinimap(0.25f, placeInTable: false);
        world.PlaceObject(70000, minimap.Widget);
        Assert.Equal(-90f, ReaderFor(world).ReadRotationDegrees());
    }

    [Fact]
    public void TheCachedWidgetSurvivesTheObjectTableGoingAway()
    {
        var world = new FakeWorld();
        world.AddMinimap(0.25f);
        var reader = ReaderFor(world);
        Assert.Equal(-90f, reader.ReadRotationDegrees());
        world.Memory.Write(FakeWorld.ModuleBase + Offsets.GObjectsGlobalRva, 0L);
        Assert.Equal(-90f, reader.ReadRotationDegrees());
    }

    [Fact]
    public void TheCachedRotateIndexTracksLiveValues()
    {
        var world = new FakeWorld();
        var minimap = world.AddMinimap(0.25f);
        var reader = ReaderFor(world);
        Assert.Equal(-90f, reader.ReadRotationDegrees());
        world.WriteScalarParameters(minimap, ("Zoom", 0.5f), ("Rotate", 0f));
        Assert.Equal(0f, reader.ReadRotationDegrees());
    }

    [Fact]
    public void ARetiredWidgetGivesNullThenRecoversOnTheNextRescan()
    {
        var clock = new Clock();
        var world = new FakeWorld();
        var old = world.AddMinimap(0.25f);
        var reader = ReaderFor(world, clock);
        Assert.Equal(-90f, reader.ReadRotationDegrees());

        world.Memory.Write(old.Widget + Offsets.UObjectClass, 0L);
        world.AddMinimap(0.5f);
        clock.Now = 100;
        Assert.Null(reader.ReadRotationDegrees());
        clock.Now = 1999;
        Assert.Null(reader.ReadRotationDegrees());
        clock.Now = 2000;
        Assert.Equal(-180f, reader.ReadRotationDegrees());
    }

    [Fact]
    public void ScansAreThrottledWhileTheWidgetIsMissing()
    {
        var clock = new Clock();
        var world = new FakeWorld();
        var reader = ReaderFor(world, clock);
        Assert.Null(reader.ReadRotationDegrees());
        world.AddMinimap(0.25f);
        clock.Now = 1500;
        Assert.Null(reader.ReadRotationDegrees());
        clock.Now = 2000;
        Assert.Equal(-90f, reader.ReadRotationDegrees());
    }

    [Fact]
    public void ABrokenCachedWidgetWithNoReplacementKeepsGivingNull()
    {
        var clock = new Clock();
        var world = new FakeWorld();
        var minimap = world.AddMinimap(0.25f);
        var reader = ReaderFor(world, clock);
        Assert.NotNull(reader.ReadRotationDegrees());
        world.Memory.Write(minimap.Widget + Offsets.MinimapWidgetMaterial, 0L);
        for (var t = 100; t <= 6000; t += 900)
        {
            clock.Now = t;
            Assert.Null(reader.ReadRotationDegrees());
        }
    }
}
