namespace EmberHUD.Api.Tests;

public sealed class SkillsTests : IDisposable
{
    public void Dispose() => Skills.Getter = null;

    [Fact]
    public void NoSourceFindsNothing()
    {
        Assert.False(Skills.TryGet(2210, out var info));
        Assert.Equal(default, info);
    }

    [Fact]
    public void LookupPassesThroughToTheSource()
    {
        Skills.Getter = id => id == 2210 ? new SkillInfo(2210, "UI_Skill_WhirlingBlade_Icon_128") : null;

        Assert.True(Skills.TryGet(2210, out var info));
        Assert.Equal("UI_Skill_WhirlingBlade_Icon_128", info.Icon);
        Assert.False(Skills.TryGet(1, out _));
    }
}
