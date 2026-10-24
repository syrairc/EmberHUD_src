using System.Numerics;
using System.Linq;
using EmberHUD;
using EmberHUD.Testing;

namespace EmberAuras;

public sealed class GroupLayoutTests
{
    static Vector2[] Squares(int count) => Enumerable.Repeat(new Vector2(10, 10), count).ToArray();

    static Group At(Growth growth, GroupAlign align = GroupAlign.Start, int spacing = 0) =>
        new() { X = 0, Y = 0, Growth = growth, Align = align, Spacing = spacing };

    static float[] Xs(Vector2[] positions) => positions.Select(p => p.X).ToArray();

    static float[] Ys(Vector2[] positions) => positions.Select(p => p.Y).ToArray();

    static void Same(float[] expected, float[] actual)
    {
        Check.Equal(expected.Length, actual.Length);
        for (var i = 0; i < expected.Length; i++) Check.Equal(expected[i], actual[i]);
    }

    [Test]
    public void RightStartGrowsFromAnchor() =>
        Same([0, 10, 20], Xs(GroupLayout.Place(At(Growth.Right), Squares(3))));

    [Test]
    public void RightEndSitsLeftOfAnchor() =>
        Same([-30, -20, -10], Xs(GroupLayout.Place(At(Growth.Right, GroupAlign.End), Squares(3))));

    [Test]
    public void RightCentreIsCentred() =>
        Same([-15, -5, 5], Xs(GroupLayout.Place(At(Growth.Right, GroupAlign.Center), Squares(3))));

    [Test]
    public void LeftStartPutsFirstAuraRightmost() =>
        Same([20, 10, 0], Xs(GroupLayout.Place(At(Growth.Left), Squares(3))));

    [Test]
    public void OutwardPlacesOddsLeft()
    {
        Same([-5, -15, 5], Xs(GroupLayout.Place(At(Growth.OutwardHorizontal), Squares(3))));
        Same([0, -10, 10, -20], Xs(GroupLayout.Place(At(Growth.OutwardHorizontal), Squares(4))));
    }

    [Test]
    public void OutwardIgnoresAlign() =>
        Same([-5, -15, 5], Xs(GroupLayout.Place(At(Growth.OutwardHorizontal, GroupAlign.End), Squares(3))));

    [Test]
    public void DownStartGrowsDown() =>
        Same([0, 10, 20], Ys(GroupLayout.Place(At(Growth.Down), Squares(3))));

    [Test]
    public void UpEndPutsFirstAuraBottom() =>
        Same([-10, -20, -30], Ys(GroupLayout.Place(At(Growth.Up, GroupAlign.End), Squares(3))));

    [Test]
    public void SpacingIsAdded() =>
        Same([0, 14, 28], Xs(GroupLayout.Place(At(Growth.Right, spacing: 4), Squares(3))));

    [Test]
    public void MixedSizesAreTopAlignedOnCrossAxis()
    {
        var positions = GroupLayout.Place(At(Growth.Right), [new Vector2(10, 30), new Vector2(20, 10)]);
        Same([0, 0], Ys(positions));
        Same([0, 10], Xs(positions));
    }

    [Test]
    public void EmptyIsEmpty() =>
        Check.Equal(0, GroupLayout.Place(At(Growth.Right), []).Length);

    [Test]
    public void BoundsCoverAll()
    {
        var sizes = new[] { new Vector2(10, 10), new Vector2(20, 30) };
        var (min, max) = GroupLayout.Bounds([new Vector2(5, 5), new Vector2(20, 0)], sizes);
        Check.Equal(new Vector2(5, 0), min);
        Check.Equal(new Vector2(40, 30), max);
        Check.Equal((Vector2.Zero, Vector2.Zero), GroupLayout.Bounds([], []));
    }
}
