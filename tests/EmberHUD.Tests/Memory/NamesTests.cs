using EmberHUD.Host;
using EmberHUD.Host.Memory;

namespace EmberHUD.Tests;

public class NamesTests
{
    private static Names NamesFor(FakeWorld world) => new(world.Memory, FakeWorld.Module);

    [Fact]
    public void AKnownNameIsNotReadAgain()
    {
        var world = new FakeWorld();
        var index = world.AddName("Fight_C");
        var names = NamesFor(world);
        Assert.True(names.TryRead(index, out var first));
        var reads = world.Memory.Reads.Count;

        Assert.True(names.TryRead(index, out var second));

        Assert.Equal("Fight_C", first);
        Assert.Same(first, second);
        Assert.Equal(reads, world.Memory.Reads.Count);
    }

    [Fact]
    public void AFailedNameIsReadAgainNextTime()
    {
        var world = new FakeWorld();
        var index = world.AddName("Fight_C");
        var blockPointer = FakeWorld.Module + Offsets.FNamePoolRva + Offsets.FNameBlocks;
        Assert.True(world.Memory.TryReadPtr(blockPointer, out var block));
        var names = NamesFor(world);

        world.Memory.Write(blockPointer, 0L);
        Assert.False(names.TryRead(index, out _));
        world.Memory.Write(blockPointer, (long)block);

        Assert.True(names.TryRead(index, out var name));
        Assert.Equal("Fight_C", name);
    }

    [Fact]
    public void WideAndOutOfRangeNamesAreRejected()
    {
        var world = new FakeWorld();
        var wide = world.AddName("Fight_C", wide: true);
        var names = NamesFor(world);

        Assert.False(names.TryRead(wide, out _));
        Assert.False(names.TryRead((uint)Offsets.MaxFNameBlocks * (uint)Offsets.FNameBlockSize, out _));
    }

    [Fact]
    public void AClassNameIsTheNameOfTheObjectsClass()
    {
        var world = new FakeWorld();
        var obj = world.AddObject(world.AddClass("EMapGodOfWar"), "Statue_1");

        Assert.True(NamesFor(world).TryReadClassName(obj, out var name));
        Assert.Equal("EMapGodOfWar", name);
    }

    [Fact]
    public void ANullOrUnmappedObjectHasNoClassName()
    {
        var world = new FakeWorld();
        var names = NamesFor(world);

        Assert.False(names.TryReadClassName(0, out _));
        Assert.False(names.TryReadClassName(FakeWorld.Unmapped, out _));
    }
}
