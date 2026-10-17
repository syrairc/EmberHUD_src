using EmberHUD.Host;
using EmberHUD.Host.Memory;

namespace EmberHUD.Tests;

public class CharacterReaderTests
{
    private const int Player = 11;

    private sealed class Rig
    {
        public FakeWorld World { get; } = new();
        public long Now { get; set; }
        public CharacterReader Reader { get; }

        public Rig()
        {
            World.SetLocalPlayer(Player);
            Reader = new CharacterReader(World.Memory, FakeWorld.Module, () => Now);
        }

        public void GivePlayerBuffs(params nint[] instances) =>
            World.AddFightEntity(Player, (Offsets.ComponentBuff, World.AddBuffComponent(instances)));

        public nint GiveSkills(params (uint Entity, int Cfg, int Charges, int MaxCharges, double Remaining, double Total)[] skills)
        {
            foreach (var skill in skills)
                World.AddFightEntity(skill.Entity, (Offsets.ComponentSkillCfg, World.AddSkillCfgComponent(skill.Cfg)));
            var component = World.AddCooldownComponent([.. skills.Select(s => (s.Entity, s.Charges, s.MaxCharges, s.Remaining, s.Total))]);
            World.AddFightEntity(Player, (Offsets.ComponentSkill, component));
            return component;
        }

        public nint FirstCooldownNode(nint component)
        {
            World.Memory.TryReadPtr(component + Offsets.CooldownMap, out var buckets);
            for (var i = 0; i < 5; i++)
                if (World.Memory.TryReadPtr(buckets + i * 8, out var head) && head != 0) return head;
            return 0;
        }
    }

    [Fact]
    public void ReadsIdRemainingAndTotalOfEveryBuff()
    {
        var rig = new Rig();
        rig.GivePlayerBuffs(rig.World.AddBuff(15010, 1, 1), rig.World.AddBuff(400, 3.5, 4), rig.World.AddBuff(320, 9.9, 10));

        var buffs = rig.Reader.ReadBuffs()!;

        Assert.Equal([320, 400, 15010], buffs.Select(b => b.Id).Order());
        var affliction = buffs.Single(b => b.Id == 400);
        Assert.Equal((3.5, 4.0), (affliction.Remaining, affliction.Total));
    }

    [Fact]
    public void SkipsABuffWithoutAConfigRow()
    {
        var rig = new Rig();
        rig.GivePlayerBuffs(rig.World.AddBuff(1, 1, 1, withCfgRow: false), rig.World.AddBuff(2, 1, 1));

        Assert.Equal(2, Assert.Single(rig.Reader.ReadBuffs()!).Id);
    }

    [Fact]
    public void SkipsAStaleInstanceWithoutThrowing()
    {
        var rig = new Rig();
        rig.GivePlayerBuffs(unchecked((nint)0x9999999999999999L), rig.World.AddBuff(2, 1, 1));

        Assert.Equal(2, Assert.Single(rig.Reader.ReadBuffs()!).Id);
    }

    [Fact]
    public void SkipsAFreedInstanceWhoseConfigRowDisagrees()
    {
        var rig = new Rig();
        rig.GivePlayerBuffs(rig.World.AddBuff(1, 1, 1, cfgId: unchecked((int)0x99999999)), rig.World.AddBuff(2, 1, 1));

        Assert.Equal(2, Assert.Single(rig.Reader.ReadBuffs()!).Id);
    }

    [Fact]
    public void NamesComeFromTheLocTableAndAreNullWhenTheRowIsMissing()
    {
        var rig = new Rig();
        rig.World.SetLanguageTable(2, 7, (Offsets.BuffNameKey + 15010 + Offsets.BuffNameSuffix, "Bull Rage"));
        rig.GivePlayerBuffs(rig.World.AddBuff(15010, 1, 1), rig.World.AddBuff(400, 1, 4));

        var buffs = rig.Reader.ReadBuffs()!;

        Assert.Equal("Bull Rage", buffs.Single(b => b.Id == 15010).Name);
        Assert.Null(buffs.Single(b => b.Id == 400).Name);
    }

    [Fact]
    public void NamesAreNullWithoutALanguageTable()
    {
        var rig = new Rig();
        rig.GivePlayerBuffs(rig.World.AddBuff(15010, 1, 1));

        Assert.Null(rig.Reader.ReadBuffs()!.Single().Name);
    }

    [Fact]
    public void ABuffMapWithNoBuffsIsEmptyNotNull()
    {
        var rig = new Rig();
        rig.GivePlayerBuffs();

        Assert.Empty(rig.Reader.ReadBuffs()!);
    }

    [Fact]
    public void IsNullWhenTheFightWorldLinkIsNull()
    {
        var rig = new Rig();
        rig.GivePlayerBuffs(rig.World.AddBuff(1, 1, 1));
        rig.World.Memory.Write(rig.World.FightMgr + Offsets.FightWorldFirst, (nint)0);

        Assert.Null(rig.Reader.ReadBuffs());
    }

    [Fact]
    public void IsNullWithoutALocalPlayer()
    {
        var world = new FakeWorld();
        world.AddFightEntity(Player, (Offsets.ComponentBuff, world.AddBuffComponent(world.AddBuff(1, 1, 1))));

        Assert.Null(new CharacterReader(world.Memory, FakeWorld.Module, () => 0).ReadBuffs());
    }

    [Fact]
    public void IsNullWhenThePlayerHasNoBuffComponent()
    {
        var rig = new Rig();
        rig.World.AddFightEntity(Player, (5, rig.World.Alloc(0x100)));

        Assert.Null(rig.Reader.ReadBuffs());
    }

    [Fact]
    public void IsNullWhenThePlayerIsNotInTheFightWorld()
    {
        var rig = new Rig();
        rig.World.AddFightEntity(99, (Offsets.ComponentBuff, rig.World.AddBuffComponent()));

        Assert.Null(rig.Reader.ReadBuffs());
    }

    [Fact]
    public void FindsThePlayerBehindOtherEntitiesInTheSameBucket()
    {
        var rig = new Rig();
        rig.World.AddFightEntity(Player + 7, (Offsets.ComponentBuff, rig.World.AddBuffComponent(rig.World.AddBuff(5, 1, 1))));
        rig.GivePlayerBuffs(rig.World.AddBuff(1, 1, 1));
        rig.World.AddFightEntity(Player + 14);

        Assert.Equal(1, Assert.Single(rig.Reader.ReadBuffs()!).Id);
    }

    [Fact]
    public void ChainLongerThanMaxBuffsIsNull()
    {
        var rig = new Rig();
        rig.GivePlayerBuffs([.. Enumerable.Range(1, Offsets.MaxBuffs + 20).Select(i => rig.World.AddBuff(i, 1, 1))]);

        Assert.Null(rig.Reader.ReadBuffs());
    }

    [Fact]
    public void AnUnreadableNodeAfterAGoodBuffIsNull()
    {
        var rig = new Rig();
        var component = rig.World.AddBuffComponent([.. Enumerable.Range(1, 6).Select(i => rig.World.AddBuff(i, 1, 1))]);
        rig.World.AddFightEntity(Player, (Offsets.ComponentBuff, component));
        rig.World.Memory.TryReadPtr(component + Offsets.BuffMap, out var buckets);
        rig.World.Memory.TryReadPtr(buckets, out var head);
        rig.World.Memory.Write(head + Offsets.NodeNext, FakeWorld.Unmapped);

        Assert.Null(rig.Reader.ReadBuffs());
    }

    [Fact]
    public void ReReadsEveryRefreshAndHoldsTheCacheBetween()
    {
        var rig = new Rig();
        var buff = rig.World.AddBuff(1, 4, 4);
        rig.GivePlayerBuffs(buff);
        Assert.Equal(4, rig.Reader.ReadBuffs()!.Single().Remaining);

        rig.World.Memory.Write(buff + Offsets.BuffRemaining, 3.0);
        Assert.Equal(4, rig.Reader.ReadBuffs()!.Single().Remaining);

        rig.Now += System.Diagnostics.Stopwatch.Frequency;
        Assert.Equal(3, rig.Reader.ReadBuffs()!.Single().Remaining);
    }

    [Fact]
    public void RejectsNonFiniteTimes()
    {
        var rig = new Rig();
        rig.GivePlayerBuffs(rig.World.AddBuff(1, double.NaN, 1), rig.World.AddBuff(2, 1, 1));

        Assert.Equal(2, Assert.Single(rig.Reader.ReadBuffs()!).Id);
    }

    private static readonly int[] Bar = [2300, 2040, 2210, 1230, 1960, 0];

    private static int[]? BarOf() => Bar;

    [Fact]
    public void ACooldownSkillReadsRemainingTotalAndItsBarSlot()
    {
        var rig = new Rig();
        rig.GiveSkills((50, 1230, 0, 0, 5.03, 8));

        var skill = Assert.Single(rig.Reader.ReadSkills(BarOf)!);

        Assert.Equal(new Skill(1230, 3, 5.03, 8, 0, 0), skill);
        Assert.False(skill.Ready);
    }

    [Fact]
    public void AChargeSkillReadsChargesAndMax()
    {
        var rig = new Rig();
        rig.GiveSkills((48, 2040, 3, 3, 0, 0));

        var skill = Assert.Single(rig.Reader.ReadSkills(BarOf)!);

        Assert.Equal(new Skill(2040, 1, 0, 0, 3, 3), skill);
        Assert.True(skill.Ready);
    }

    [Fact]
    public void ASingleChargeRecordIsNotAChargeSkill()
    {
        var rig = new Rig();
        rig.GiveSkills((49, 1960, 0, 1, 3, 8), (50, 1230, 1, 1, 0, 8));

        Assert.All(rig.Reader.ReadSkills(BarOf)!, s => Assert.Equal((0, 0), (s.Charges, s.MaxCharges)));
    }

    [Fact]
    public void SlotFollowsTheBarAndIsMinusOneOffTheBar()
    {
        var rig = new Rig();
        rig.GiveSkills((51, 2300, 1, 1, 0, 0), (48, 2040, 3, 3, 0, 0), (52, 2210, 0, 0, 0, 0), (50, 1230, 0, 0, 0, 0), (49, 1960, 0, 0, 0, 0), (12, 80000, 0, 0, 0, 0));

        var slots = rig.Reader.ReadSkills(BarOf)!.ToDictionary(s => s.CfgId, s => s.Slot);

        Assert.Equal(new Dictionary<int, int> { [2300] = 0, [2040] = 1, [2210] = 2, [1230] = 3, [1960] = 4, [80000] = -1 }, slots);
    }

    [Fact]
    public void AnEmptyBarSlotMatchesNoSkill()
    {
        var rig = new Rig();
        rig.GiveSkills((12, 0, 0, 0, 0, 0));

        Assert.Equal(-1, Assert.Single(rig.Reader.ReadSkills(BarOf)!).Slot);
    }

    [Fact]
    public void AnUnreadableBarIsNullNotOffTheBar()
    {
        var rig = new Rig();
        rig.GiveSkills((50, 1230, 0, 0, 5, 8));

        Assert.Null(rig.Reader.ReadSkills(() => null));
    }

    [Fact]
    public void AnEmptyCooldownMapIsEmptyNotNull()
    {
        var rig = new Rig();
        rig.GiveSkills();

        Assert.Empty(rig.Reader.ReadSkills(BarOf)!);
    }

    [Fact]
    public void AnUnreadableCooldownMapIsNullNotAnEmptyList()
    {
        var rig = new Rig();
        var component = rig.GiveSkills((50, 1230, 0, 0, 5, 8));
        rig.World.Memory.Write(component + Offsets.CooldownMap, FakeWorld.Unmapped);

        Assert.Null(rig.Reader.ReadSkills(BarOf));
    }

    [Fact]
    public void AnUnreadableCooldownNodeIsNullNotAShorterList()
    {
        var rig = new Rig();
        var component = rig.GiveSkills((50, 1230, 0, 0, 5, 8), (49, 1960, 0, 0, 5, 8));
        rig.World.Memory.Write(rig.FirstCooldownNode(component) + Offsets.CooldownNodeNext, FakeWorld.Unmapped);

        Assert.Null(rig.Reader.ReadSkills(BarOf));
    }

    [Fact]
    public void ASkillEntityMissingFromTheFightWorldIsNull()
    {
        var rig = new Rig();
        rig.World.AddFightEntity(Player, (Offsets.ComponentSkill, rig.World.AddCooldownComponent((77u, 0, 0, 1, 8))));

        Assert.Null(rig.Reader.ReadSkills(BarOf));
    }

    [Fact]
    public void ASkillWithoutAConfigComponentIsNull()
    {
        var rig = new Rig();
        rig.World.AddFightEntity(50, (5, rig.World.Alloc(0x100)));
        rig.World.AddFightEntity(Player, (Offsets.ComponentSkill, rig.World.AddCooldownComponent((50u, 0, 0, 1, 8))));

        Assert.Null(rig.Reader.ReadSkills(BarOf));
    }

    [Fact]
    public void NonFiniteTimesAreNull()
    {
        var rig = new Rig();
        rig.GiveSkills((50, 1230, 0, 0, double.NaN, 8));

        Assert.Null(rig.Reader.ReadSkills(BarOf));
    }

    [Fact]
    public void MoreRecordsThanMaxSkillsIsNull()
    {
        var rig = new Rig();
        rig.GiveSkills([.. Enumerable.Range(0, Offsets.MaxSkills + 5).Select(i => ((uint)(100 + i), 1000 + i, 0, 0, 1.0, 8.0))]);

        Assert.Null(rig.Reader.ReadSkills(BarOf));
    }

    [Fact]
    public void IsNullWhenThePlayerHasNoSkillComponent()
    {
        var rig = new Rig();
        rig.GivePlayerBuffs();

        Assert.Null(rig.Reader.ReadSkills(BarOf));
    }

    [Fact]
    public void SkillsAndBuffsRefreshIndependently()
    {
        var rig = new Rig();
        rig.GiveSkills((50, 1230, 0, 0, 5, 8));
        Assert.Null(rig.Reader.ReadBuffs());

        Assert.Single(rig.Reader.ReadSkills(BarOf)!);
    }

    [Fact]
    public void SkillsHoldTheCacheBetweenRefreshes()
    {
        var rig = new Rig();
        var component = rig.GiveSkills((50, 1230, 0, 0, 5, 8));
        Assert.Equal(5, rig.Reader.ReadSkills(BarOf)!.Single().CooldownRemaining);

        rig.World.Memory.Write(rig.FirstCooldownNode(component) + Offsets.CooldownRemaining, 4.0);
        Assert.Equal(5, rig.Reader.ReadSkills(BarOf)!.Single().CooldownRemaining);

        rig.Now += System.Diagnostics.Stopwatch.Frequency;
        Assert.Equal(4, rig.Reader.ReadSkills(BarOf)!.Single().CooldownRemaining);
    }
}
