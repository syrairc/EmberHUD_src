namespace EmberHUD.Api.Tests;

public sealed class ItemsTests : IDisposable
{
    public void Dispose()
    {
        Items.BaseGetter = null;
        Items.UniqueGetter = null;
    }

    [Fact]
    public void NoSourceFindsNothing()
    {
        Assert.False(Items.TryGet(5080, out var info));
        Assert.Equal(default, info);
        Assert.False(Items.FromUnique(112258, out _));
        Assert.False(Items.IsUnique(112258));
    }

    [Fact]
    public void LookupsPassThroughToTheSource()
    {
        var wanted = new ItemInfo(5080, 2, 1, 2, 3, 4, 5, 6, false);
        Items.BaseGetter = id => id == 5080 ? wanted : null;
        Items.UniqueGetter = id => id == 112258 ? wanted with { BaseId = 3610, IsUnique = true } : null;

        Assert.True(Items.TryGet(5080, out var info));
        Assert.Equal(wanted, info);
        Assert.False(Items.TryGet(1, out _));
        Assert.True(Items.FromUnique(112258, out var unique));
        Assert.Equal(3610, unique.BaseId);
        Assert.True(unique.IsUnique);
        Assert.True(Items.IsUnique(112258));
        Assert.False(Items.IsUnique(1));
    }
}
