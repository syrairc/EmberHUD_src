using System.Drawing;
using System.Numerics;
using EmberHUD.Testing;
using EmberHUD.Ui;

namespace EmberHUD.Api.Tests;

public class IconBarTests
{
    static readonly Vector2 Origin = new(10, 10);
    static readonly Vector2 Bar = new(100, 20);

    [Fact]
    public void LeftIconShiftsBarRight()
    {
        var rects = IconBar.Layout(Origin, Bar, 24, Controls.IconSide.Left, 4);
        Assert.Equal(38, rects.BarMin.X);
        Assert.Equal(new Vector2(10, 8), rects.IconMin);
    }

    [Fact]
    public void RightIconSitsAfterBar()
    {
        var rects = IconBar.Layout(Origin, Bar, 24, Controls.IconSide.Right, 4);
        Assert.Equal(114, rects.IconMin.X);
        Assert.Equal(new Vector2(10, 10), rects.BarMin);
    }

    [Fact]
    public void ZeroIconAddsNoGap()
    {
        var rects = IconBar.Layout(Origin, Bar, 0, Controls.IconSide.Left, 4);
        Assert.Equal(Origin, rects.BarMin);
        Assert.Equal(110, rects.Max.X);
    }

    [Fact]
    public void BoundsCoverIconAndBar()
    {
        var rects = IconBar.Layout(Origin, Bar, 24, Controls.IconSide.Left, 4);
        Assert.Equal(new Vector2(10, 8), rects.Min);
        Assert.Equal(new Vector2(138, 32), rects.Max);
    }

    [Fact]
    public void DrawBarFillsFromTheLeftAndClampsTheFraction() => TestFrame.Run(() =>
    {
        var rects = IconBar.Layout(Origin, Bar, 0, Controls.IconSide.Left);
        foreach (var fill in new[] { 2f, -1f })
        {
            var list = Draw.Background;
            var before = list.VtxBuffer.Size;
            IconBar.DrawBar(rects, fill, Color.Gold, Color.Black, 1);
            var xs = Enumerable.Range(before, list.VtxBuffer.Size - before).Select(v => list.VtxBuffer[v].Pos.X).ToList();
            Assert.True(xs.Max() - xs.Min() <= Bar.X + 1e-3f, $"fill {fill} drew {xs.Max() - xs.Min()} wide");
        }
    });

    [Fact]
    public void DrawBarSurvivesOpacityOutsideZeroToOne() => TestFrame.Run(() =>
    {
        var rects = IconBar.Layout(Origin, Bar, 0, Controls.IconSide.Left);
        foreach (var opacity in new[] { 2f, -1f }) IconBar.DrawBar(rects, 0.5f, Color.Gold, Color.Black, opacity);
    });
}
