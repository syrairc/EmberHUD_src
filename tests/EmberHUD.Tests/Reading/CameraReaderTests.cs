using EmberHUD.Host;
using EmberHUD.Host.Reading;

namespace EmberHUD.Tests;

public sealed class CameraReaderTests
{
    static (FakeWorld World, long Pawn, long Manager) Chain()
    {
        var world = new FakeWorld();
        var pawn = world.Alloc(0x400);
        var controller = world.Alloc(0x400);
        var manager = world.Alloc(0x2000);
        world.Memory.Write(pawn + Offsets.PawnController, controller);
        world.Memory.Write(controller + Offsets.ControllerCameraManager, manager);
        world.Memory.WriteArray(manager + Offsets.CameraCache + Offsets.PovLocation, new[] { 1f, 2f, 3f });
        world.Memory.WriteArray(manager + Offsets.CameraCache + Offsets.PovRotation, new[] { -45f, 90f, 0f });
        world.Memory.Write(manager + Offsets.CameraCache + Offsets.PovFov, 60f);
        return (world, pawn, manager);
    }

    [Fact]
    public void ReadsThePovThroughTheControllerAndCameraManager()
    {
        var (world, pawn, _) = Chain();

        Assert.Equal(new Camera(new Vector3(1, 2, 3), new Vector3(-45, 90, 0), 60), new CameraReader(world.Memory).Read((nint)pawn));
    }

    [Fact]
    public void CameraNeedsTheWholeChain()
    {
        var (world, pawn, manager) = Chain();
        var reader = new CameraReader(world.Memory);

        world.Memory.Write(manager + Offsets.CameraCache + Offsets.PovFov, 0f);
        Assert.Null(reader.Read((nint)pawn));

        world.Memory.Write(manager + Offsets.CameraCache + Offsets.PovFov, float.NaN);
        Assert.Null(reader.Read((nint)pawn));

        world.Memory.Write(pawn + Offsets.PawnController, 0L);
        Assert.Null(reader.Read((nint)pawn));
    }
}
