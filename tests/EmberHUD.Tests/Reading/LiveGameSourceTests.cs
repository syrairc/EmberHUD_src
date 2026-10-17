using EmberHUD.Host.Memory;
using EmberHUD.Host;
using EmberHUD.Tests.Reading;

namespace EmberHUD.Tests;

public sealed class LiveGameSourceTests : IDisposable
{
    private static nint At(int rva) => FakeWorld.Module + rva;

    public void Dispose()
    {
        EmberHUD.Memory.Use(null);
        LiveGameSource.UseLookups(null, 0);
    }

    private static FakeLua LuaWorld(FakeWorld world)
    {
        var lua = new FakeLua(world.Memory, FakeWorld.Module);
        var inner = lua.NewTable(0, 2);
        lua.SetString(inner, "name", "Gold");
        lua.SetTable(lua.Globals, "inner", inner);
        LiveGameSource.UseLookups(world.Memory, FakeWorld.Module);
        return lua;
    }

    [Fact]
    public void LuaDumpListsThePathFromTheGame()
    {
        LuaWorld(new FakeWorld());

        var dump = Lua.Dump("inner", 10);

        Assert.NotNull(dump);
        Assert.True(dump.Resolved);
        Assert.Equal(1, dump.Total);
        Assert.Equal(new LuaEntry("name", "string", "Gold"), Assert.Single(dump.Entries));
    }

    [Fact]
    public void LuaDumpReportsTheSegmentThatFailed()
    {
        LuaWorld(new FakeWorld());

        Assert.Equal(new LuaDump(false, "missing", [], 0), Lua.Dump("inner.missing", 10));
        Assert.Equal(new LuaDump(false, "nope", [], 0), Lua.Dump("nope.x", 10));
    }

    [Fact]
    public void LuaDumpWithNoLuaStateIsUnresolvedWithNoSegment()
    {
        var world = new FakeWorld();
        LiveGameSource.UseLookups(world.Memory, FakeWorld.Module);

        var dump = Lua.Dump("inner", 10);

        Assert.NotNull(dump);
        Assert.False(dump.Resolved);
        Assert.Equal("", dump.FailedAt);
        Assert.Empty(dump.Entries);
    }

    [Fact]
    public void ClearingTheLookupsClearsLua()
    {
        LuaWorld(new FakeWorld());
        LiveGameSource.UseLookups(null, 0);

        Assert.Null(Lua.Dump("inner", 10));
    }

    [Fact]
    public void LookupsReadTheGameAndDisposeClearsThem()
    {
        var world = new FakeWorld();
        world.SetLanguageTable(2, 7, ("level|name|4403", "Rainforest of Divine Legacy"));
        world.PublishEntities(world.AddEntity(11, 48, true, 1, 2, 0));
        LiveGameSource.UseLookups(world.Memory, FakeWorld.Module);

        Assert.Equal("Rainforest of Divine Legacy", Loc.Get("level|name|4403"));
        Assert.Equal([("level|name|4403", "Rainforest of Divine Legacy")], Loc.Find("rainforest", 50, out _));
        Assert.Equal(11, Assert.Single(Scene.Entities()).Id);

        new LiveGameSource().Dispose();

        Assert.Null(Loc.Get("level|name|4403"));
        Assert.Empty(Loc.Find("rainforest", 50, out _));
        Assert.Empty(Scene.Entities());
    }

    [Fact]
    public void TheSceneListerReadsAClassNameOnlyOnce()
    {
        var world = new FakeWorld();
        var statue = world.AddEntity(3, 77, false, 3, 3, 0, mapOffset: Offsets.FightMapGamePlay);
        world.Memory.Write(statue.Value + Offsets.UObjectClass, world.AddClass("EMapGodOfWar"));
        world.PublishEntities(statue);
        LiveGameSource.UseLookups(world.Memory, FakeWorld.Module);
        Assert.Equal("EMapGodOfWar", Assert.Single(Scene.Entities()).Class);
        var blockPointer = FakeWorld.Module + Offsets.FNamePoolRva + Offsets.FNameBlocks;
        world.Memory.Reads.Clear();

        Assert.Equal("EMapGodOfWar", Assert.Single(Scene.Entities()).Class);

        Assert.DoesNotContain(world.Memory.Reads, r => r.Address == blockPointer);
    }

    [Fact]
    public void LookupsRacingTheHandleClosingGiveNothing()
    {
        LiveGameSource.UseLookups(new ClosedBackend(), FakeWorld.Module);

        Assert.Null(Loc.Get("level|name|4403"));
        Assert.Empty(Loc.Find("rainforest", 50, out _));
        Assert.Empty(Scene.Entities());
    }

    [Fact]
    public async Task AFindInProgressDoesNotHoldUpGet()
    {
        var cancel = TestContext.Current.CancellationToken;
        var world = new FakeWorld();
        world.SetLanguageTable(2, 7, ("level|name|4403", "Rainforest of Divine Legacy"));
        var gated = new GatedBackend(world.Memory);
        LiveGameSource.UseLookups(gated, FakeWorld.Module);

        var find = Task.Run(() =>
        {
            gated.BlockedThread = Environment.CurrentManagedThreadId;
            return Loc.Find("rainforest", 50, out _);
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
        var source = LiveGameSource.SourceFor(world.Memory, FakeWorld.Module);
        EmberHUD.Memory.Use(source);

        Assert.Equal(At(Offsets.FightMgrGlobalRva), EmberHUD.Memory.Globals["FightMgr"]);
        Assert.Equal(At(Offsets.FNamePoolRva), source.NamePool);
        Assert.Equal(At(Offsets.GObjectsGlobalRva), source.ObjectArray);
        Assert.Equal(6, EmberHUD.Memory.Globals.Count);
        Assert.Equal(Offsets.UObjectClass, source.Layout!.ObjectClass);
        Assert.Equal(Offsets.FPropertyOffset, source.Layout.PropertyOffset);
        Assert.Equal(Offsets.UeClassClassName, source.Layout.ClassClassName);
        Assert.True(EmberHUD.Memory.TryRead(At(Offsets.FightMgrGlobalRva), out nint fightMgr));
        Assert.Equal(world.FightMgr, fightMgr);
    }

    [Fact]
    public void DisposeDetachesMemory()
    {
        EmberHUD.Memory.Use(LiveGameSource.SourceFor(new FakeWorld().Memory, FakeWorld.Module));

        new LiveGameSource().Dispose();

        Assert.False(EmberHUD.Memory.Attached);
    }

    [Fact]
    public void AReadRacingTheHandleClosingReturnsFalse()
    {
        var source = LiveGameSource.SourceFor(new ClosedBackend(), FakeWorld.Module);

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
