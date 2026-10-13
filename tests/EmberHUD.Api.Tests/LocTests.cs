namespace EmberHUD.Api.Tests;

public sealed class LocTests : IDisposable
{
    public void Dispose()
    {
        Loc.Getter = null;
        Loc.Finder = null;
    }

    [Fact]
    public void NoSourceGivesNullAndNothingFound()
    {
        Assert.Null(Loc.Get("level|name|4403"));
        Assert.Empty(Loc.Find("Rainforest"));
    }

    [Fact]
    public void GetAndFindPassThroughToTheSource()
    {
        (string, int)? asked = null;
        Loc.Getter = key => key == "level|name|4403" ? "Rainforest of Divine Legacy" : null;
        Loc.Finder = (needle, max) => { asked = (needle, max); return ([("level|name|4403", "Rainforest of Divine Legacy")], false); };

        Assert.Equal("Rainforest of Divine Legacy", Loc.Get("level|name|4403"));
        Assert.Null(Loc.Get("level|name|1"));
        Assert.Equal([("level|name|4403", "Rainforest of Divine Legacy")], Loc.Find("rainforest"));
        Assert.Equal(("rainforest", 50), asked);
        Loc.Find("x", 3);
        Assert.Equal(("x", 3), asked);
    }

    [Fact]
    public void FindSaysWhenTheSourceStoppedEarly()
    {
        Assert.Empty(Loc.Find("x", 50, out var truncated));
        Assert.False(truncated);
        Loc.Finder = (_, _) => ([("a", "x")], true);
        Assert.Equal([("a", "x")], Loc.Find("x", 50, out truncated));
        Assert.True(truncated);
    }
}
