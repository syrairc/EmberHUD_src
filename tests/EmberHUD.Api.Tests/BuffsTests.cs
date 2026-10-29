namespace EmberHUD.Api.Tests;

public sealed class BuffsTests : IDisposable
{
    public void Dispose() => Buffs.Getter = null;

    [Fact]
    public void NoSourceFindsNothing()
    {
        Assert.False(Buffs.TryGet(15180, out var info));
        Assert.Equal(default, info);
    }

    [Fact]
    public void LookupPassesThroughToTheSource()
    {
        Buffs.Getter = id => id == 15180 ? new BuffInfo(15180, "UI_Buff_AttackEnhance_Icon_32") : null;

        Assert.True(Buffs.TryGet(15180, out var info));
        Assert.Equal("UI_Buff_AttackEnhance_Icon_32", info.Icon);
        Assert.False(Buffs.TryGet(1, out _));
    }

    [Fact]
    public void TwoArgumentInfoKeepsTheDefaults()
    {
        var info = new BuffInfo(7, "icon");
        Assert.Null(info.Name);
        Assert.Equal(BuffKind.None, info.Kind);
        Assert.Equal(CrowdControl.None, info.Control);
        Assert.Equal(0, info.MaxStacks);
    }

    [Fact]
    public void GetReturnsTheInfoOrNull()
    {
        Buffs.Getter = id => id == 110 ? new BuffInfo(110, null, "Freeze", BuffKind.Control, Control: CrowdControl.Freeze) : null;

        Assert.Equal("Freeze", Buffs.Get(110)!.Value.Name);
        Assert.Null(Buffs.Get(1));
    }

    [Fact]
    public void PlayerControlIsTheUnionOverActiveBuffs()
    {
        Buffs.Getter = id => id switch
        {
            110 => new BuffInfo(110, null, Control: CrowdControl.Freeze),
            300 => new BuffInfo(300, null, Control: CrowdControl.Root | CrowdControl.CantMove),
            _ => new BuffInfo(id, null),
        };
        var state = GameState.Empty with { Buffs = [new Buff(110, null, 1, 2), new Buff(300, null, 1, 2), new Buff(5, null, 1, 2)] };

        Assert.Equal(CrowdControl.Freeze | CrowdControl.Root | CrowdControl.CantMove, state.Control);
    }

    [Fact]
    public void PlayerControlIsNoneWithoutBuffsOrSource()
    {
        Assert.Equal(CrowdControl.None, GameState.Empty.Control);
        Assert.Equal(CrowdControl.None, (GameState.Empty with { Buffs = [new Buff(110, null, 1, 2)] }).Control);
    }

    [Fact]
    public void FightEntityControlIsTheUnionOverItsBuffs()
    {
        Buffs.Getter = id => id == 4 ? new BuffInfo(4, null, Control: CrowdControl.Stun) : null;
        var entity = new FightEntity(1, default, default, FightArchetype.Monster, []) { Buffs = [new FightBuff(4, 1, 2), new FightBuff(9, 1, 2)] };

        Assert.Equal(CrowdControl.Stun, entity.Control);
        Assert.Equal(CrowdControl.None, (entity with { Buffs = [] }).Control);
    }
}
