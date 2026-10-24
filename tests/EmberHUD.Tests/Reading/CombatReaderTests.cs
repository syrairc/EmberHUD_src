using EmberHUD.Host;
using EmberHUD.Host.Memory;

namespace EmberHUD.Tests;

public class CombatReaderTests
{
    private const int LocalPlayer = 15;

    private sealed class Rig
    {
        public FakeWorld World { get; } = new();
        public long Now { get; set; }
        public CombatReader Reader { get; }
        private readonly List<FakeEntity> _monsters = [];

        public Rig(bool withPlayer = true)
        {
            if (withPlayer) World.SetLocalPlayer(LocalPlayer);
            Reader = new CombatReader(World.Memory, FakeWorld.Module, () => Now);
        }

        public (nint Container, nint Hit) AddMonster(uint id, int attacker, double effective, double raw, byte inlineFlag = 0)
        {
            var monster = World.AddEntity(id, 100, true, 0, 0, 0) with { Value = World.Alloc(0x800) };
            var container = World.Alloc(0x400);
            var hit = World.Alloc(Offsets.HitSize);
            World.Memory.Write(monster.Value + Offsets.UObjectHurtContainer, container);
            World.Memory.Write(container + Offsets.HurtInlineFlag, inlineFlag);
            WriteHit(hit, attacker, effective, raw);
            SetList(container, hit);
            _monsters.Add(monster);
            World.PublishEntities(_monsters.ToArray());
            return (container, hit);
        }

        public void WriteHit(nint hit, int attacker, double effective, double raw, int eventId = 0)
        {
            World.Memory.Write(hit + Offsets.HitEvent, eventId);
            World.Memory.Write(hit + Offsets.HitAttacker, attacker);
            World.Memory.Write(hit + Offsets.HitEffective, effective);
            World.Memory.Write(hit + Offsets.HitRaw, raw);
        }

        public void SetList(nint container, params nint[] hits)
        {
            var array = World.Alloc(Math.Max(8, hits.Length * 8));
            if (hits.Length > 0) World.Memory.WriteArray(array, hits);
            World.Memory.Write(container + Offsets.HurtListPointer, array);
            World.Memory.Write(container + Offsets.HurtListCount, hits.Length);
        }

        public CombatStats Poll()
        {
            Reader.Poll();
            return Reader.Snapshot!;
        }
    }

    [Fact]
    public void CountsAHitFromTheLocalPlayer()
    {
        var rig = new Rig();
        rig.AddMonster(1, LocalPlayer, 100, 160);
        var stats = rig.Poll();
        Assert.Equal(100, stats.Effective);
        Assert.Equal(160, stats.Raw);
        Assert.Equal(1, stats.Hits);
    }

    [Fact]
    public void SplitsDamageByVictim()
    {
        var rig = new Rig();
        rig.AddMonster(1, LocalPlayer, 100, 160);
        rig.AddMonster(2, LocalPlayer, 30, 30);
        var stats = rig.Poll();
        Assert.Equal(new VictimDamage(100, 160), stats.ByVictim[1]);
        Assert.Equal(new VictimDamage(30, 30), stats.ByVictim[2]);
        Assert.Equal(130, stats.Effective);
    }

    [Fact]
    public void IgnoresHitsFromOtherAttackers()
    {
        var rig = new Rig();
        rig.AddMonster(1, 99, 100, 160);
        var stats = rig.Poll();
        Assert.Equal(0, stats.Effective);
        Assert.Equal(0, stats.Hits);
    }

    [Fact]
    public void CountsTheSameHitOnceAcrossPolls()
    {
        var rig = new Rig();
        rig.AddMonster(1, LocalPlayer, 100, 160);
        rig.Poll();
        rig.Poll();
        Assert.Equal(1, rig.Poll().Hits);
    }

    [Fact]
    public void CountsARecycledPointerWithANewValue()
    {
        var rig = new Rig();
        var (_, hit) = rig.AddMonster(1, LocalPlayer, 100, 160);
        rig.Poll();
        rig.WriteHit(hit, LocalPlayer, 250, 300);
        var stats = rig.Poll();
        Assert.Equal(2, stats.Hits);
        Assert.Equal(350, stats.Effective);
    }

    [Fact]
    public void CountsARecycledPointerWithTheSameValueButANewEvent()
    {
        var rig = new Rig();
        var (_, hit) = rig.AddMonster(1, LocalPlayer, 100, 160);
        rig.WriteHit(hit, LocalPlayer, 100, 160, eventId: 1);
        rig.Poll();
        rig.WriteHit(hit, LocalPlayer, 100, 160, eventId: 2);
        var stats = rig.Poll();
        Assert.Equal(2, stats.Hits);
        Assert.Equal(200, stats.Effective);
    }

    [Fact]
    public void AnEmptyListForgetsTheSeenHits()
    {
        var rig = new Rig();
        var (container, hit) = rig.AddMonster(1, LocalPlayer, 100, 160);
        rig.Poll();
        rig.SetList(container);
        rig.Poll();
        rig.SetList(container, hit);
        Assert.Equal(2, rig.Poll().Hits);
    }

    [Fact]
    public void SkipsTheStaticFallbackList()
    {
        var rig = new Rig();
        rig.AddMonster(1, LocalPlayer, 100, 160, inlineFlag: 1);
        Assert.Equal(0, rig.Poll().Hits);
    }

    [Fact]
    public void SkipsGarbageCounts()
    {
        var rig = new Rig();
        var (container, _) = rig.AddMonster(1, LocalPlayer, 100, 160);
        foreach (var count in new[] { -1, 0, Offsets.MaxHurtList + 1 })
        {
            rig.World.Memory.Write(container + Offsets.HurtListCount, count);
            Assert.Equal(0, rig.Poll().Hits);
        }

        rig.World.Memory.Write(container + Offsets.HurtListCount, 1);
        rig.World.Memory.Write(container + Offsets.HurtListPointer, 0L);
        Assert.Equal(0, rig.Poll().Hits);
    }

    [Fact]
    public void ClampsRawUpToEffectiveAndDropsNonFiniteDamage()
    {
        var rig = new Rig();
        var (_, hit) = rig.AddMonster(1, LocalPlayer, 100, 5);
        var stats = rig.Poll();
        Assert.Equal(100, stats.Raw);

        rig.WriteHit(hit, LocalPlayer, double.NaN, 5);
        Assert.Equal(1, rig.Poll().Hits);
        rig.WriteHit(hit, LocalPlayer, -3, 5);
        Assert.Equal(1, rig.Poll().Hits);
    }

    [Fact]
    public void ResetsWhenTheFightMgrChanges()
    {
        var rig = new Rig();
        rig.AddMonster(1, LocalPlayer, 100, 160);
        Assert.Equal(1, rig.Poll().Hits);

        var next = rig.World.Alloc(0x1000);
        rig.World.Memory.Write(FakeWorld.Module + Offsets.FightMgrGlobalRva, next);
        rig.Now += System.Diagnostics.Stopwatch.Frequency * (Offsets.CombatRefreshMilliseconds + 1) / 1000;
        Assert.Equal(0, rig.Poll().Hits);
    }

    [Fact]
    public void ResetsAtOnceWhenTheFightMgrPointerChangesBetweenRefreshes()
    {
        var rig = new Rig();
        rig.AddMonster(1, LocalPlayer, 100, 160);
        Assert.Equal(1, rig.Poll().Hits);

        rig.World.Memory.Write(FakeWorld.Module + Offsets.FightMgrGlobalRva, rig.World.Alloc(0x1000));
        Assert.Equal(0, rig.Poll().Hits);
    }

    [Fact]
    public void NoLocalPlayerMeansNoSnapshot()
    {
        var rig = new Rig(withPlayer: false);
        rig.AddMonster(1, LocalPlayer, 100, 160);
        rig.Reader.Poll();
        Assert.Null(rig.Reader.Snapshot);

        var withPlayer = new Rig();
        withPlayer.AddMonster(1, LocalPlayer, 100, 160);
        withPlayer.Poll();
        withPlayer.World.BreakPlayerMgr();
        withPlayer.Now += System.Diagnostics.Stopwatch.Frequency * (Offsets.CombatRefreshMilliseconds + 1) / 1000;
        withPlayer.Reader.Poll();
        Assert.Null(withPlayer.Reader.Snapshot);
    }

    [Fact]
    public void UnreadableContainerIsSkipped()
    {
        var rig = new Rig();
        rig.AddMonster(1, LocalPlayer, 100, 160);
        rig.AddMonster(2, LocalPlayer, 40, 40);
        rig.World.Memory.Write(FindMonsterValue(rig, 1) + Offsets.UObjectHurtContainer, FakeWorld.Unmapped);
        var stats = rig.Poll();
        Assert.Equal(40, stats.Effective);
        Assert.Equal(1, stats.Hits);
    }

    private static nint FindMonsterValue(Rig rig, int id) =>
        rig.World.Memory.ReadTMapPointers(rig.World.FightMgr + Offsets.FightMapMonster).Single(p => p.Key == id).Value;
}
