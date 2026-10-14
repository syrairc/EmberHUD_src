using EmberHUD.Host;
using EmberHUD.Host.Memory;

namespace EmberHUD.Tests;

public class MinimapReaderTests
{
    private static MinimapReader ReaderFor(FakeWorld world) =>
        new(world.Memory, FakeWorld.Module, new Names(world.Memory, FakeWorld.Module));

    [Fact]
    public void ReadsTheRotateTurnsAsClockwiseDegrees()
    {
        var world = new FakeWorld();
        world.AddMinimap(0.25f);
        Assert.Equal(-90f, ReaderFor(world).ReadRotationDegrees(1));
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
        Assert.Equal(expected, ReaderFor(world).ReadRotationDegrees(1)!.Value, 3);
    }

    [Fact]
    public void FindsRotateWhereverItSitsInTheParameterArray()
    {
        var world = new FakeWorld();
        var minimap = world.AddMinimap(0f);
        world.WriteScalarParameters(minimap, ("MapResolution", 485f), ("Zoom", 0.8f), ("Other", 3f), ("Rotate", 0.25f));
        Assert.Equal(-90f, ReaderFor(world).ReadRotationDegrees(1));
    }

    [Fact]
    public void NoWidgetAtAllGivesNull()
    {
        Assert.Null(ReaderFor(new FakeWorld()).ReadRotationDegrees(1));
    }

    [Fact]
    public void ObjectsOfOtherClassesAreIgnored()
    {
        var world = new FakeWorld();
        world.AddMinimap(0.25f, className: "MiniMapOther_C", objectName: "MiniMap_C");
        world.AddObject(world.AddClass("Widget_C"), "MiniMap_C");
        Assert.Null(ReaderFor(world).ReadRotationDegrees(1));
    }

    [Fact]
    public void WideClassNamesAreRejected()
    {
        var world = new FakeWorld();
        world.AddObject(world.AddClass("MiniMap_C", wideName: true), "MiniMap_C");
        Assert.Null(ReaderFor(world).ReadRotationDegrees(1));
    }

    [Fact]
    public void DefaultObjectsAreIgnoredButTheInstanceIsFound()
    {
        var world = new FakeWorld();
        world.AddObject(world.AddClass("MiniMap_C"), "Default__MiniMap_C");
        Assert.Null(ReaderFor(world).ReadRotationDegrees(1));

        world.AddMinimap(0.25f);
        Assert.Equal(-90f, ReaderFor(world).ReadRotationDegrees(1));
    }

    [Fact]
    public void MissingRotateGivesNull()
    {
        var world = new FakeWorld();
        world.AddMinimap(null);
        Assert.Null(ReaderFor(world).ReadRotationDegrees(1));
    }

    [Theory]
    [InlineData(float.NaN)]
    [InlineData(float.PositiveInfinity)]
    [InlineData(float.NegativeInfinity)]
    public void NonFiniteRotateGivesNull(float value)
    {
        var world = new FakeWorld();
        world.AddMinimap(value);
        Assert.Null(ReaderFor(world).ReadRotationDegrees(1));
    }

    [Fact]
    public void TornMaterialPointersGiveNull()
    {
        var world = new FakeWorld();
        var minimap = world.AddMinimap(0.25f);
        world.Memory.Write(minimap.Widget + Offsets.MinimapWidgetMaterial, FakeWorld.Unmapped);
        Assert.Null(ReaderFor(world).ReadRotationDegrees(1));
        world.Memory.Write(minimap.Widget + Offsets.MinimapWidgetMaterial, 0L);
        Assert.Null(ReaderFor(world).ReadRotationDegrees(1));
    }

    public static TheoryData<int> AbsurdCounts => new() { 0, -1, Offsets.MaxScalarParams + 1, int.MaxValue };

    [Theory]
    [MemberData(nameof(AbsurdCounts))]
    public void AbsurdParameterCountGivesNull(int count)
    {
        var world = new FakeWorld();
        var minimap = world.AddMinimap(0.25f);
        world.Memory.Write(minimap.Material + Offsets.MaterialScalarParams + Offsets.TArrayNum, count);
        Assert.Null(ReaderFor(world).ReadRotationDegrees(1));
    }

    [Fact]
    public void UnmappedParameterArrayGivesNull()
    {
        var world = new FakeWorld();
        var minimap = world.AddMinimap(0.25f);
        world.Memory.Write(minimap.Material + Offsets.MaterialScalarParams, FakeWorld.Unmapped);
        Assert.Null(ReaderFor(world).ReadRotationDegrees(1));
    }

    public static TheoryData<int> AbsurdObjectCounts => new() { 0, -5, Offsets.MaxGObjectChunks * Offsets.GObjectsPerChunk + 1 };

    [Theory]
    [MemberData(nameof(AbsurdObjectCounts))]
    public void AbsurdObjectCountGivesNull(int count)
    {
        var world = new FakeWorld();
        world.AddMinimap(0.25f);
        world.Memory.Write(FakeWorld.Module + Offsets.GObjectsGlobalRva + Offsets.GObjectsNumElements, count);
        Assert.Null(ReaderFor(world).ReadRotationDegrees(1));
    }

    [Fact]
    public void UnmappedChunkTableGivesNull()
    {
        var world = new FakeWorld();
        world.AddMinimap(0.25f);
        world.Memory.Write(FakeWorld.Module + Offsets.GObjectsGlobalRva, FakeWorld.Unmapped);
        Assert.Null(ReaderFor(world).ReadRotationDegrees(1));
    }

    [Fact]
    public void FindsTheWidgetInALaterChunk()
    {
        var world = new FakeWorld();
        world.AddObject(world.AddClass("Other_C"), "Thing");
        var minimap = world.AddMinimap(0.25f, placeInTable: false);
        world.PlaceObject(70000, minimap.Widget);
        Assert.Equal(-90f, ReaderFor(world).ReadRotationDegrees(1));
    }

    [Fact]
    public void TheCachedWidgetSurvivesTheObjectTableGoingAway()
    {
        var world = new FakeWorld();
        world.AddMinimap(0.25f);
        var reader = ReaderFor(world);
        Assert.Equal(-90f, reader.ReadRotationDegrees(1));
        world.Memory.Write(FakeWorld.Module + Offsets.GObjectsGlobalRva, 0L);
        Assert.Equal(-90f, reader.ReadRotationDegrees(1));
    }

    [Fact]
    public void TheCachedRotateIndexTracksLiveValues()
    {
        var world = new FakeWorld();
        var minimap = world.AddMinimap(0.25f);
        var reader = ReaderFor(world);
        Assert.Equal(-90f, reader.ReadRotationDegrees(1));
        world.WriteScalarParameters(minimap, ("Zoom", 0.5f), ("Rotate", 0f));
        Assert.Equal(0f, reader.ReadRotationDegrees(1));
    }

    [Fact]
    public void ARetiredWidgetGivesNullThenRecoversOnTheNextGeneration()
    {
        var world = new FakeWorld();
        var old = world.AddMinimap(0.25f);
        var reader = ReaderFor(world);
        Assert.Equal(-90f, reader.ReadRotationDegrees(1));

        world.Memory.Write(old.Widget + Offsets.UObjectClass, 0L);
        world.AddMinimap(0.5f);
        Assert.Null(reader.ReadRotationDegrees(1));
        Assert.Null(reader.ReadRotationDegrees(1));
        Assert.Equal(-180f, reader.ReadRotationDegrees(2));
    }

    [Fact]
    public void ResetRescansAtOnceInsideTheSameGeneration()
    {
        var world = new FakeWorld();
        var old = world.AddMinimap(0.25f);
        var reader = ReaderFor(world);
        Assert.Equal(-90f, reader.ReadRotationDegrees(1));

        world.Memory.Write(old.Widget + Offsets.UObjectClass, 0L);
        world.AddMinimap(0.5f);
        Assert.Null(reader.ReadRotationDegrees(1));
        reader.Reset();
        Assert.Equal(-180f, reader.ReadRotationDegrees(1));
    }

    [Fact]
    public void TheObjectTableIsScannedOnceForEachGenerationWhileTheWidgetIsMissing()
    {
        var world = new FakeWorld();
        var reader = ReaderFor(world);
        Assert.Null(reader.ReadRotationDegrees(1));
        world.AddMinimap(0.25f);

        var before = world.Memory.Reads.Count;
        Assert.Null(reader.ReadRotationDegrees(1));
        Assert.Null(reader.ReadRotationDegrees(1));
        Assert.True(world.Memory.Reads.Count - before < 20);
        Assert.Equal(-90f, reader.ReadRotationDegrees(2));
    }

    [Fact]
    public void ABrokenCachedWidgetWithNoReplacementKeepsGivingNull()
    {
        var world = new FakeWorld();
        var minimap = world.AddMinimap(0.25f);
        var reader = ReaderFor(world);
        Assert.NotNull(reader.ReadRotationDegrees(1));
        world.Memory.Write(minimap.Widget + Offsets.MinimapWidgetMaterial, 0L);
        for (var i = 0; i < 5; i++)
            Assert.Null(reader.ReadRotationDegrees(1));
    }

    [Fact]
    public void AGenerationChangeRescansAtOnceAndPicksTheNewestWidgetEvenWhenTheOldOneIsStillReadable()
    {
        var world = new FakeWorld();
        world.AddMinimap(0.25f);
        var reader = ReaderFor(world);
        Assert.Equal(-90f, reader.ReadRotationDegrees(1));

        world.AddMinimap(0.5f);
        Assert.Equal(-90f, reader.ReadRotationDegrees(1));
        Assert.Equal(-180f, reader.ReadRotationDegrees(2));
        Assert.Equal(-180f, reader.ReadRotationDegrees(2));
    }

    [Fact]
    public void AFailedScanAfterAGenerationChangeWaitsForTheNextGeneration()
    {
        var world = new FakeWorld();
        var old = world.AddMinimap(0.25f);
        var reader = ReaderFor(world);
        Assert.NotNull(reader.ReadRotationDegrees(1));

        world.Memory.Write(old.Widget + Offsets.UObjectClass, 0L);
        Assert.Null(reader.ReadRotationDegrees(2));
        world.AddMinimap(0.5f);
        Assert.Null(reader.ReadRotationDegrees(2));
        Assert.Equal(-180f, reader.ReadRotationDegrees(3));
    }

    [Fact]
    public void TheWidgetIsExposedAfterARotationReadAndDroppedWhenItBreaks()
    {
        var world = new FakeWorld();
        var minimap = world.AddMinimap(0f);
        var reader = ReaderFor(world);
        Assert.Equal((nint)0, reader.Widget);
        reader.ReadRotationDegrees(1);
        Assert.Equal(minimap.Widget, reader.Widget);
        world.Memory.Write(minimap.Widget + Offsets.MinimapWidgetMaterial, 0L);
        reader.ReadRotationDegrees(1);
        Assert.Equal((nint)0, reader.Widget);
    }

    [Fact]
    public void ReadsTheHeadingInDegrees()
    {
        var world = new FakeWorld();
        var minimap = world.AddMinimap(0f);
        world.Memory.Write(minimap.Widget + Offsets.MinimapHeading, 37.5f);
        var reader = ReaderFor(world);
        reader.ReadRotationDegrees(1);
        Assert.Equal(37.5f, reader.ReadHeadingDegrees());
    }

    [Fact]
    public void ANonFiniteHeadingOrNoWidgetGivesNull()
    {
        var empty = ReaderFor(new FakeWorld());
        empty.ReadRotationDegrees(1);
        Assert.Null(empty.ReadHeadingDegrees());

        var world = new FakeWorld();
        var minimap = world.AddMinimap(0f);
        world.Memory.Write(minimap.Widget + Offsets.MinimapHeading, float.NaN);
        var reader = ReaderFor(world);
        reader.ReadRotationDegrees(1);
        Assert.Null(reader.ReadHeadingDegrees());
    }

    [Fact]
    public void TheWidgetTheGameBoundWinsOverANewerReadableOne()
    {
        var world = new FakeWorld();
        var bound = world.AddMinimap(0.25f);
        world.AddMinimap(0.5f);
        world.BindMinimap(bound);
        var reader = ReaderFor(world);

        Assert.Equal(-90f, reader.ReadRotationDegrees(1));
        Assert.Equal(bound.Widget, reader.Widget);
    }

    [Fact]
    public void BindingANewWidgetSwitchesOnTheNextReadWithoutWaitingForARescan()
    {
        var world = new FakeWorld();
        var old = world.AddMinimap(0.25f);
        world.BindMinimap(old);
        var reader = ReaderFor(world);
        Assert.Equal(-90f, reader.ReadRotationDegrees(1));

        var fresh = world.AddMinimap(0.5f);
        world.BindMinimap(fresh, serial: 9);

        Assert.Equal(-180f, reader.ReadRotationDegrees(1));
        Assert.Equal(-180f, reader.ReadRotationDegrees(2));
    }

    [Fact]
    public void AStaleWeakPointerFallsBackToTheScan()
    {
        var world = new FakeWorld();
        var bound = world.AddMinimap(0.25f);
        world.AddMinimap(0.5f);
        world.BindMinimap(bound, serial: 7, weakSerial: 6);
        var reader = ReaderFor(world);

        Assert.Equal(-180f, reader.ReadRotationDegrees(1));
    }

    [Fact]
    public void TheFallbackScanIsCountedOncePerGeneration()
    {
        var world = new FakeWorld();
        world.AddMinimap(0f);
        var reader = ReaderFor(world);
        Assert.Equal(0, reader.Scans);

        reader.ReadRotationDegrees(1);
        reader.ReadRotationDegrees(1);
        Assert.Equal(1, reader.Scans);

        reader.ReadRotationDegrees(2);
        Assert.Equal(2, reader.Scans);
    }

    [Fact]
    public void ABoundWidgetNeverScans()
    {
        var world = new FakeWorld();
        var minimap = world.AddMinimap(0f);
        world.BindMinimap(minimap);
        var reader = ReaderFor(world);

        reader.ReadRotationDegrees(1);

        Assert.Equal(0, reader.Scans);
    }
}
