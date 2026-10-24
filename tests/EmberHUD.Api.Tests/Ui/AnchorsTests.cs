using System.Numerics;
using EmberHUD.Ui;

namespace EmberHUD.Api.Tests;

public class AnchorsTests
{
    static readonly Vector2 Min = Vector2.Zero;
    static readonly Vector2 Max = new(100, 50);
    static readonly Vector2 Size = new(20, 10);

    [Fact]
    public void TopLeftIsInsetByPad() =>
        Assert.Equal(new Vector2(2, 2), Anchors.Place(Min, Max, Size, Anchor.TopLeft, 2f));

    [Fact]
    public void BottomRightIsInsetByPad() =>
        Assert.Equal(new Vector2(78, 38), Anchors.Place(Min, Max, Size, Anchor.BottomRight, 2f));

    [Fact]
    public void CenterIsCentred() =>
        Assert.Equal(new Vector2(40, 20), Anchors.Place(Min, Max, Size, Anchor.Center, 2f));

    [Fact]
    public void TopIsCentredHorizontally() =>
        Assert.Equal(new Vector2(40, 2), Anchors.Place(Min, Max, Size, Anchor.Top, 2f));

    [Fact]
    public void LeftIsCentredVertically() =>
        Assert.Equal(new Vector2(2, 20), Anchors.Place(Min, Max, Size, Anchor.Left, 2f));
}
