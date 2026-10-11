namespace Aember.Api.Tests;

public class MapViewTests
{
    [Fact]
    public void FitCentresTheMapAndKeepsItsAspect()
    {
        var view = MapView.Fit(new Vector2(-10, -20), new Vector2(10, 20), new Vector2(1000, 500), 50);

        Assert.Equal(10f, view.Scale);
        Assert.Equal(new Vector2(500, 250), view.ToScreen(Vector2.Zero));
        Assert.Equal(new Vector2(500, 50), view.ToScreen(new Vector2(0, -20)));
        Assert.Equal(new Vector2(600, 450), view.ToScreen(new Vector2(10, 20)));
    }

    [Fact]
    public void FitCentresAnOffCentreMap()
    {
        var view = MapView.Fit(new Vector2(100, 100), new Vector2(200, 150), new Vector2(400, 300), 0);

        Assert.Equal(4f, view.Scale);
        Assert.Equal(new Vector2(200, 150), view.ToScreen(new Vector2(150, 125)));
        Assert.Equal(new Vector2(0, 50), view.ToScreen(new Vector2(100, 100)));
    }

    [Fact]
    public void AnEmptyMapOrATinyViewportFitsNothing()
    {
        Assert.Equal(0f, MapView.Fit(Vector2.Zero, Vector2.Zero, new Vector2(800, 600), 40).Scale);
        Assert.Equal(0f, MapView.Fit(new Vector2(-1, -1), new Vector2(1, 1), new Vector2(50, 50), 40).Scale);
    }

    [Fact]
    public void WorldPlusXIsScreenRightAndWorldPlusYIsScreenDown()
    {
        var view = MapView.Fit(new Vector2(-10, -10), new Vector2(10, 10), new Vector2(200, 200), 0);

        var origin = view.ToScreen(Vector3.Zero);
        var plusX = view.ToScreen(new Vector3(1, 0, 0));
        var plusY = view.ToScreen(new Vector3(0, 1, 0));

        Assert.Equal(new Vector2(100, 100), origin);
        Assert.Equal(new Vector2(110, 100), plusX);
        Assert.Equal(new Vector2(100, 110), plusY);
    }

    [Fact]
    public void FollowPutsTheCentreAtTheViewportMiddle()
    {
        var view = MapView.Follow(new Vector2(30, -10), new Vector2(800, 600), 5);

        Assert.Equal(new Vector2(400, 300), view.ToScreen(new Vector2(30, -10)));
        Assert.Equal(new Vector2(405, 300), view.ToScreen(new Vector2(31, -10)));
        Assert.Equal(0f, MapView.Follow(Vector2.Zero, new Vector2(800, 600), 0).Scale);
    }

    [Fact]
    public void ToScreenTakesAWorldPointThroughTheProjection()
    {
        var view = MapView.Follow(new Vector2(30, -10), new Vector2(800, 600), 5);

        Assert.Equal(view.ToScreen(new Vector2(2, 5)), view.ToScreen(new Vector3(2, 5, 3)));
    }

    static void Near(Vector2 expected, Vector2 actual)
    {
        Assert.Equal(expected.X, actual.X, 3);
        Assert.Equal(expected.Y, actual.Y, 3);
    }

    [Fact]
    public void RotationTurnsClockwiseOnScreen()
    {
        var view = new MapView(Vector2.Zero, 2, 90);

        Near(new Vector2(0, 2), view.ToScreen(new Vector2(1, 0)));
        Near(new Vector2(-2, 0), view.ToScreen(new Vector2(0, 1)));
        Near(new Vector2(2, 0), view.ToScreen(new Vector2(0, -1)));
    }

    [Fact]
    public void VerticalScaleSquashesAfterRotationAndScale()
    {
        var view = new MapView(new Vector2(100, 50), 2, 90, 0.5f);

        Near(new Vector2(100, 51), view.ToScreen(new Vector2(1, 0)));
        Near(new Vector2(98, 50), view.ToScreen(new Vector2(0, 1)));
        Near(new Vector2(104, 54), new MapView(new Vector2(100, 50), 2, 0, 0.5f).ToScreen(new Vector2(2, 4)));
    }

    [Fact]
    public void ToScreenDirectionIgnoresTheOffset()
    {
        var view = new MapView(new Vector2(300, 400), 3, 90, 0.5f);

        Near(new Vector2(0, 1.5f), view.ToScreenDirection(new Vector2(1, 0)));
        Near(Vector2.Zero, view.ToScreenDirection(Vector2.Zero));
    }

    [Fact]
    public void TwoArgumentConstructorIsTheOldPlainView()
    {
        var view = new MapView(new Vector2(10, 20), 4);

        Assert.Equal(new Vector2(14, 28), view.ToScreen(new Vector2(1, 2)));
        Assert.Equal(0f, view.RotationDegrees);
        Assert.Equal(1f, view.VerticalScale);
    }

    [Theory]
    [InlineData(0f, 1f)]
    [InlineData(90f, 1f)]
    [InlineData(37f, 0.5f)]
    [InlineData(-135f, 0.7f)]
    public void FollowKeepsTheCentreAtTheViewportMiddleUnderRotation(float rotation, float vertical)
    {
        var view = MapView.Follow(new Vector2(30, -10), new Vector2(800, 600), 5, rotation, vertical);

        Near(new Vector2(400, 300), view.ToScreen(new Vector2(30, -10)));
        Assert.Equal(rotation, view.RotationDegrees);
        Assert.Equal(vertical, view.VerticalScale);
    }

    [Fact]
    public void FitFitsTheRotatedBounds()
    {
        var view = MapView.Fit(new Vector2(-20, -10), new Vector2(20, 10), new Vector2(200, 400), 0, 90);

        Assert.Equal(10f, view.Scale, 3);
        Near(new Vector2(100, 200), view.ToScreen(Vector2.Zero));
        Near(new Vector2(100, 0), view.ToScreen(new Vector2(-20, 0)));
        Near(new Vector2(200, 200), view.ToScreen(new Vector2(0, -10)));
    }

    [Fact]
    public void FitCentresAnOffCentreBoxUnderRotationAndSquash()
    {
        var view = MapView.Fit(new Vector2(100, 100), new Vector2(140, 120), new Vector2(400, 300), 20, 30, 0.5f);

        var corners = new[] { new Vector2(100, 100), new Vector2(140, 100), new Vector2(100, 120), new Vector2(140, 120) }.Select(view.ToScreen).ToArray();
        Assert.Equal(400 - corners.Max(c => c.X), corners.Min(c => c.X), 2);
        Assert.Equal(300 - corners.Max(c => c.Y), corners.Min(c => c.Y), 2);
        Assert.True(corners.Min(c => c.X) >= 20 - 0.01f);
        Assert.True(corners.Min(c => c.Y) >= 20 - 0.01f);
        Assert.True(Math.Abs(corners.Min(c => c.X) - 20) < 0.01f || Math.Abs(corners.Min(c => c.Y) - 20) < 0.01f);
    }

    [Fact]
    public void InvalidScaleRotationOrVerticalScaleGiveNoView()
    {
        var viewport = new Vector2(800, 600);

        Assert.Equal(default, MapView.Follow(Vector2.Zero, viewport, 0, 10));
        Assert.Equal(default, MapView.Follow(Vector2.Zero, viewport, float.NaN));
        Assert.Equal(default, MapView.Follow(Vector2.Zero, viewport, 1, float.NaN));
        Assert.Equal(default, MapView.Follow(Vector2.Zero, viewport, 1, float.PositiveInfinity));
        Assert.Equal(default, MapView.Follow(Vector2.Zero, viewport, 1, 0, 0));
        Assert.Equal(default, MapView.Follow(Vector2.Zero, viewport, 1, 0, -1));
        Assert.Equal(default, MapView.Follow(Vector2.Zero, viewport, 1, 0, float.NaN));
        Assert.Equal(default, MapView.Fit(new Vector2(-1, -1), new Vector2(1, 1), viewport, 0, float.NaN));
        Assert.Equal(default, MapView.Fit(new Vector2(-1, -1), new Vector2(1, 1), viewport, 0, 0, 0));
        Assert.Equal(default, MapView.Fit(new Vector2(-1, -1), new Vector2(1, 1), viewport, 0, 0, float.NaN));
    }
}
