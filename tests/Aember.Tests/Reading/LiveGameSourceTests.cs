using Aember.Host.Memory;
using Aember.Host;
using Aember.Host.Reading;

namespace Aember.Tests;

public sealed class LiveGameSourceTests : IDisposable
{
    private static nint At(long rva) => unchecked((nint)(FakeWorld.ModuleBase + rva));

    public void Dispose() => Aember.Memory.Use(null);

    [Fact]
    public void SourceForPublishesTheGlobalsTheLayoutAndTheBackend()
    {
        var world = new FakeWorld();
        var source = LiveGameSource.SourceFor(world.Memory, unchecked((nint)FakeWorld.ModuleBase));
        Aember.Memory.Use(source);

        Assert.Equal(At(Offsets.FightMgrGlobalRva), Aember.Memory.Globals["FightMgr"]);
        Assert.Equal(At(Offsets.FNamePoolRva), source.NamePool);
        Assert.Equal(At(Offsets.GObjectsGlobalRva), source.ObjectArray);
        Assert.Equal(6, Aember.Memory.Globals.Count);
        Assert.Equal(Offsets.UObjectClass, source.Layout!.ObjectClass);
        Assert.Equal(Offsets.FPropertyOffset, source.Layout.PropertyOffset);
        Assert.Equal(Offsets.UeClassClassName, source.Layout.ClassClassName);
        Assert.True(Aember.Memory.TryRead(At(Offsets.FightMgrGlobalRva), out long fightMgr));
        Assert.Equal(world.FightMgr, fightMgr);
    }

    [Fact]
    public void DisposeDetachesMemory()
    {
        Aember.Memory.Use(LiveGameSource.SourceFor(new FakeWorld().Memory, unchecked((nint)FakeWorld.ModuleBase)));

        new LiveGameSource().Dispose();

        Assert.False(Aember.Memory.Attached);
    }

    [Fact]
    public void AReadRacingTheHandleClosingReturnsFalse()
    {
        var source = LiveGameSource.SourceFor(new ClosedBackend(), unchecked((nint)FakeWorld.ModuleBase));

        Assert.False(source.Read(At(Offsets.FightMgrGlobalRva), new byte[8]));
    }

    private sealed class ClosedBackend : IReadBackend
    {
        public bool TryRead(nint address, Span<byte> target) => throw new ObjectDisposedException("handle");
    }
}
