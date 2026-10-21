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
}
