using EmberHUD.Host;
using EmberHUD.Host.Memory;

namespace EmberHUD.Tests;

public class FakeWorldTests
{
    [Fact]
    public void PlacingObjectsKeepsOneChunkTableAndReadsEveryObjectBack()
    {
        var world = new FakeWorld();
        var global = (nint)(FakeWorld.Module + Offsets.GObjectsGlobalRva);
        world.PlaceObject(5, 0x3000);
        Assert.True(world.Memory.TryReadPtr(global, out var table));

        world.PlaceObject(70000, 0x4000);
        world.PlaceObject(6, 0x5000);

        Assert.True(world.Memory.TryReadPtr(global, out var after));
        Assert.Equal(table, after);
        foreach (var (index, expected) in new[] { (5u, 0x3000), (6u, 0x5000), (70000u, 0x4000) })
        {
            Assert.True(world.Memory.TryReadGObject(FakeWorld.Module, index, out var obj, out _));
            Assert.Equal((nint)expected, obj);
        }
    }
}
