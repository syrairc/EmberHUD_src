using EmberHUD.Host.Memory;
using EmberHUD.Host;
using EmberHUD.Host.Reading;

namespace EmberHUD.Tests;

public sealed class LiveGameSourceTests : IDisposable
{
    private static nint At(long rva) => unchecked((nint)(FakeWorld.ModuleBase + rva));

    public void Dispose()
    {
        EmberHUD.Memory.Use(null);
        LiveGameSource.UseLookups(null, 0);
    }

    [Fact]
    public void LookupsReadTheGameAndDisposeClearsThem()
    {
        var world = new FakeWorld();
        world.SetLanguageTable(2, 7, ("level|name|4403", "Rainforest of Divine Legacy"));
        world.PublishEntities(world.AddEntity(11, 48, true, 1, 2, 0));
        LiveGameSource.UseLookups(world.Memory, unchecked((nint)FakeWorld.ModuleBase));

        Assert.Equal("Rainforest of Divine Legacy", Loc.Get("level|name|4403"));
        Assert.Equal([("level|name|4403", "Rainforest of Divine Legacy")], Loc.Find("rainforest"));
        Assert.Equal(11, Assert.Single(Scene.Entities()).Id);

        new LiveGameSource().Dispose();

        Assert.Null(Loc.Get("level|name|4403"));
        Assert.Empty(Loc.Find("rainforest"));
        Assert.Empty(Scene.Entities());
    }

    [Fact]
    public void LookupsRacingTheHandleClosingGiveNothing()
    {
        LiveGameSource.UseLookups(new ClosedBackend(), unchecked((nint)FakeWorld.ModuleBase));

        Assert.Null(Loc.Get("level|name|4403"));
        Assert.Empty(Loc.Find("rainforest"));
        Assert.Empty(Scene.Entities());
    }

    [Fact]
    public async Task AFindInProgressDoesNotHoldUpGet()
    {
        var cancel = TestContext.Current.CancellationToken;
        var world = new FakeWorld();
        world.SetLanguageTable(2, 7, ("level|name|4403", "Rainforest of Divine Legacy"));
        var gated = new GatedBackend(world.Memory);
        LiveGameSource.UseLookups(gated, unchecked((nint)FakeWorld.ModuleBase));

        var find = Task.Run(() =>
        {
            gated.BlockedThread = Environment.CurrentManagedThreadId;
            return Loc.Find("rainforest");
        }, cancel);
        Assert.True(gated.Entered.Wait(5000, cancel));
        var get = Task.Run(() => Loc.Get("level|name|4403"), cancel);
        var getFinished = await Task.WhenAny(get, Task.Delay(2000, cancel)) == get;
        gated.Release.Set();

        Assert.True(getFinished, "Loc.Get waited for Loc.Find");
        Assert.Equal("Rainforest of Divine Legacy", await get);
        Assert.Single(await find);
    }

    [Fact]
    public void SourceForPublishesTheGlobalsTheLayoutAndTheBackend()
    {
        var world = new FakeWorld();
        var source = LiveGameSource.SourceFor(world.Memory, unchecked((nint)FakeWorld.ModuleBase));
        EmberHUD.Memory.Use(source);

        Assert.Equal(At(Offsets.FightMgrGlobalRva), EmberHUD.Memory.Globals["FightMgr"]);
        Assert.Equal(At(Offsets.FNamePoolRva), source.NamePool);
        Assert.Equal(At(Offsets.GObjectsGlobalRva), source.ObjectArray);
        Assert.Equal(6, EmberHUD.Memory.Globals.Count);
        Assert.Equal(Offsets.UObjectClass, source.Layout!.ObjectClass);
        Assert.Equal(Offsets.FPropertyOffset, source.Layout.PropertyOffset);
        Assert.Equal(Offsets.UeClassClassName, source.Layout.ClassClassName);
        Assert.True(EmberHUD.Memory.TryRead(At(Offsets.FightMgrGlobalRva), out long fightMgr));
        Assert.Equal(world.FightMgr, fightMgr);
    }

    [Fact]
    public void DisposeDetachesMemory()
    {
        EmberHUD.Memory.Use(LiveGameSource.SourceFor(new FakeWorld().Memory, unchecked((nint)FakeWorld.ModuleBase)));

        new LiveGameSource().Dispose();

        Assert.False(EmberHUD.Memory.Attached);
    }

    [Fact]
    public void AReadRacingTheHandleClosingReturnsFalse()
    {
        var source = LiveGameSource.SourceFor(new ClosedBackend(), unchecked((nint)FakeWorld.ModuleBase));

        Assert.False(source.Read(At(Offsets.FightMgrGlobalRva), new byte[8]));
    }

    private sealed class GatedBackend(IReadBackend inner) : IReadBackend
    {
        public volatile int BlockedThread = -1;
        public ManualResetEventSlim Entered { get; } = new();
        public ManualResetEventSlim Release { get; } = new();

        public bool TryRead(nint address, Span<byte> target)
        {
            if (Environment.CurrentManagedThreadId == BlockedThread)
            {
                Entered.Set();
                Release.Wait();
            }
            return inner.TryRead(address, target);
        }
    }

    private sealed class ClosedBackend : IReadBackend
    {
        public bool TryRead(nint address, Span<byte> target) => throw new ObjectDisposedException("handle");
    }
}
