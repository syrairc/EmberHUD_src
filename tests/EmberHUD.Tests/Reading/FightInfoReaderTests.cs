using EmberHUD.Host;
using EmberHUD.Host.Memory;

namespace EmberHUD.Tests;

public class FightInfoReaderTests
{
    private static readonly (double X, double Y) Spot = (500, 200);

    private sealed class Rig
    {
        public FakeWorld World { get; } = new();
        public long Now { get; set; } = 1000;
        public FightInfoReader Reader { get; }

        public Rig() => Reader = new FightInfoReader(World.Memory, FakeWorld.Module, () => Now);
    }

    [Fact]
    public void ReadsAMonster()
    {
        var rig = new Rig();
        var components = rig.World.AddFighter(7, Spot, Offsets.NpcComponentId, Offsets.RoleComponentId, Offsets.SideComponentId);
        rig.World.Memory.Write(components[Offsets.NpcComponentId] + Offsets.NpcSource, 4u);
        rig.World.Memory.Write(components[Offsets.RoleComponentId] + Offsets.FightRoleLevel, 83);
        rig.World.Memory.Write(components[Offsets.SideComponentId] + Offsets.SideCamp, (byte)FightCamps.Hostile);

        Assert.Equal(new FightInfo(FightArchetype.Monster, FightCamps.Hostile, 83, 4), rig.Reader.Read(7, 1));
    }

    [Fact]
    public void ServantIsAMinionOnTheServantCamp()
    {
        var rig = new Rig();
        var components = rig.World.AddFighter(7, Spot, Offsets.NpcComponentId, Offsets.ServantComponentId, Offsets.SideComponentId);
        rig.World.Memory.Write(components[Offsets.SideComponentId] + Offsets.SideCamp, (byte)FightCamps.Servant);

        Assert.Equal(new FightInfo(FightArchetype.Minion, FightCamps.Servant, 0, 0), rig.Reader.Read(7, 1));
    }

    [Fact]
    public void PlayerIsAPlayerWithoutASideCampReadAsMinusOne()
    {
        var rig = new Rig();
        rig.World.AddFighter(7, Spot, Offsets.PlayerComponentId);

        Assert.Equal(new FightInfo(FightArchetype.Player, -1, 0, 0), rig.Reader.Read(7, 1));
    }

    [Fact]
    public void InsaneLevelReadsAsZero()
    {
        var rig = new Rig();
        var components = rig.World.AddFighter(7, Spot, Offsets.NpcComponentId, Offsets.RoleComponentId);
        rig.World.Memory.Write(components[Offsets.RoleComponentId] + Offsets.FightRoleLevel, 123456789);

        Assert.Equal(0, rig.Reader.Read(7, 1)?.Level);
    }

    [Fact]
    public void NoFighterIsNull()
    {
        var rig = new Rig();
        rig.World.AddFighter(7, Spot, Offsets.NpcComponentId);

        Assert.Null(rig.Reader.Read(8, 1));
    }

    [Fact]
    public void CampIsReReadOnlyAfterTheRefreshInterval()
    {
        var rig = new Rig();
        var components = rig.World.AddFighter(7, Spot, Offsets.NpcComponentId, Offsets.SideComponentId);
        var camp = components[Offsets.SideComponentId] + Offsets.SideCamp;
        rig.World.Memory.Write(camp, (byte)FightCamps.Hostile);
        Assert.Equal(FightCamps.Hostile, rig.Reader.Read(7, 1)?.Camp);

        rig.World.Memory.Write(camp, (byte)FightCamps.Servant);
        rig.Now += Offsets.FightInfoCampRefreshMilliseconds - 1;
        Assert.Equal(FightCamps.Hostile, rig.Reader.Read(7, 1)?.Camp);

        rig.Now += 1;
        var refreshed = rig.Reader.Read(7, 1);
        Assert.Equal(FightCamps.Servant, refreshed?.Camp);
        Assert.Equal(FightArchetype.Monster, refreshed?.Archetype);
    }

    [Fact]
    public void SteadyStateDoesNotTouchMemoryAndRefreshDoesNotReReadTheArchetype()
    {
        var rig = new Rig();
        var components = rig.World.AddFighter(7, Spot, Offsets.NpcComponentId, Offsets.RoleComponentId, Offsets.SideComponentId);
        rig.Reader.Read(7, 1);

        rig.World.Memory.Reads.Clear();
        rig.Reader.Read(7, 1);
        Assert.Empty(rig.World.Memory.Reads);

        rig.Now += Offsets.FightInfoCampRefreshMilliseconds;
        rig.Reader.Read(7, 1);
        Assert.NotEmpty(rig.World.Memory.Reads);
        Assert.DoesNotContain(rig.World.Memory.Reads, read => read.Address == components[Offsets.NpcComponentId] + Offsets.NpcSource || read.Address == components[Offsets.RoleComponentId] + Offsets.FightRoleLevel);
        Assert.True(rig.World.Memory.Reads.Count < 20);
    }

    [Fact]
    public void GenerationChangeClearsTheCache()
    {
        var rig = new Rig();
        var components = rig.World.AddFighter(7, Spot, Offsets.NpcComponentId, Offsets.RoleComponentId);
        var level = components[Offsets.RoleComponentId] + Offsets.FightRoleLevel;
        rig.World.Memory.Write(level, 10);
        Assert.Equal(10, rig.Reader.Read(7, 1)?.Level);

        rig.World.Memory.Write(level, 20);
        Assert.Equal(10, rig.Reader.Read(7, 1)?.Level);
        Assert.Equal(20, rig.Reader.Read(7, 2)?.Level);
    }

    [Fact]
    public void AFailedReadIsRetriedAfterTheRefreshInterval()
    {
        var rig = new Rig();
        Assert.Null(rig.Reader.Read(7, 1));

        rig.World.AddFighter(7, Spot, Offsets.NpcComponentId);
        Assert.Null(rig.Reader.Read(7, 1));

        rig.Now += Offsets.FightInfoCampRefreshMilliseconds;
        Assert.Equal(FightArchetype.Monster, rig.Reader.Read(7, 1)?.Archetype);
    }

    [Fact]
    public void GarbageMemoryIsNull()
    {
        var rig = new Rig();
        rig.World.AddFighter(7, Spot, Offsets.NpcComponentId);
        rig.World.Memory.MakeUnreadable(FakeWorld.Module + Offsets.FightMgrGlobalRva, 8);

        Assert.Null(rig.Reader.Read(7, 1));
    }
}
