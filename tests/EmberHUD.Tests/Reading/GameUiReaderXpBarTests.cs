using EmberHUD.Host;

namespace EmberHUD.Tests;

public sealed class GameUiReaderXpBarTests
{
    [Fact]
    public void ReadsTheBarRect()
    {
        var world = new FakeWorld();
        new FakeUi(world).AddXpBar(new Vector2(700, 1380), new Vector2(2040, 8), 0.5f);

        var rect = world.UiReader().ReadXpBar();

        Assert.NotNull(rect);
        Assert.Equal(new Vector2(700, 1380), rect.Value.Min);
        Assert.Equal(1020, rect.Value.Width, 0.01f);
        Assert.Equal(4, rect.Value.Height, 0.01f);
    }

    [Theory]
    [InlineData((byte)1, (byte)0)]
    [InlineData((byte)4, (byte)1)]
    public void HiddenBarIsNull(byte viewVisibility, byte barVisibility)
    {
        var world = new FakeWorld();
        new FakeUi(world).AddXpBar(new Vector2(700, 1380), new Vector2(2040, 8), viewVisibility: viewVisibility, barVisibility: barVisibility);

        Assert.Null(world.UiReader().ReadXpBar());
    }

    [Fact]
    public void NoHudIsNull()
    {
        var world = new FakeWorld();
        _ = new FakeUi(world);

        Assert.Null(world.UiReader().ReadXpBar());
    }

    [Fact]
    public void CollapsedHighLightSiblingIsNotReturned()
    {
        var world = new FakeWorld();
        new FakeUi(world).AddXpBar(new Vector2(700, 1380), new Vector2(2040, 8));

        Assert.Equal(new Vector2(700, 1380), world.UiReader().ReadXpBar()!.Value.Min);
    }

    [Fact]
    public void HiddenBarIsNotReplacedByTheHighLight()
    {
        var world = new FakeWorld();
        new FakeUi(world).AddXpBar(new Vector2(700, 1380), new Vector2(2040, 8), barVisibility: 1);

        Assert.Null(world.UiReader().ReadXpBar());
    }
}
