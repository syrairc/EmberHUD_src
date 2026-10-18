using EmberHUD.Host;

namespace EmberHUD.Tests;

public sealed class GameUiReaderBossBarTests
{
    [Fact]
    public void ReadsRectAndPercentOfTheBossBar()
    {
        var world = new FakeWorld();
        new FakeUi(world).AddBossBar(0.364f, new Vector2(1388, 75), new Vector2(1037, 42), 0.64f);

        var bar = world.UiReader().ReadBossBar();

        Assert.NotNull(bar);
        Assert.Equal(new Vector2(1388, 75), bar.Rect.Min);
        Assert.Equal(1037 * 0.64f, bar.Rect.Width, 0.01f);
        Assert.Equal(42 * 0.64f, bar.Rect.Height, 0.01f);
        Assert.Equal(0.364f, bar.Percent, 0.0001f);
    }

    [Fact]
    public void RectIsRelativeToTheOverlayOrigin()
    {
        var world = new FakeWorld();
        new FakeUi(world, new Vector2(100, 50)).AddBossBar(0.5f, new Vector2(1388, 75), new Vector2(1000, 40));

        var bar = world.UiReader().ReadBossBar();

        Assert.Equal(new Vector2(1288, 25), bar!.Rect.Min);
    }

    [Fact]
    public void FindsTheBarInAHostedHud()
    {
        var world = new FakeWorld();
        new FakeUi(world).AddBossBar(0.5f, new Vector2(10, 20), new Vector2(300, 30), hosted: true);

        Assert.NotNull(world.UiReader().ReadBossBar());
    }

    [Fact]
    public void NoBossViewMeansNoBar()
    {
        var world = new FakeWorld();
        new FakeUi(world).MapHud(["line"]);

        Assert.Null(world.UiReader().ReadBossBar());
    }

    [Theory]
    [InlineData((byte)1, (byte)0)]
    [InlineData((byte)2, (byte)0)]
    [InlineData((byte)4, (byte)1)]
    public void HiddenViewOrBarMeansNoBar(byte viewVisibility, byte barVisibility)
    {
        var world = new FakeWorld();
        new FakeUi(world).AddBossBar(0.5f, new Vector2(10, 20), new Vector2(300, 30), viewVisibility: viewVisibility, barVisibility: barVisibility);

        Assert.Null(world.UiReader().ReadBossBar());
    }

    [Theory]
    [InlineData(float.NaN)]
    [InlineData(-0.5f)]
    [InlineData(1.5f)]
    public void GarbagePercentMeansNoBar(float percent)
    {
        var world = new FakeWorld();
        new FakeUi(world).AddBossBar(percent, new Vector2(10, 20), new Vector2(300, 30));

        Assert.Null(world.UiReader().ReadBossBar());
    }

    [Fact]
    public void NoHudMeansNoBar()
    {
        var world = new FakeWorld();
        _ = new FakeUi(world);

        Assert.Null(world.UiReader().ReadBossBar());
    }
}
