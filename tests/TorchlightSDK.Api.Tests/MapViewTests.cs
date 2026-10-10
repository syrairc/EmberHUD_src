namespace TorchlightSDK.Api.Tests;

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
    public void WorldPlusXIsScreenRightAndWorldPlusYIsScreenUp()
    {
        var view = MapView.Fit(new Vector2(-10, -10), new Vector2(10, 10), new Vector2(200, 200), 0);

        var origin = view.ToScreen(Vector3.Zero);
        var plusX = view.ToScreen(new Vector3(1, 0, 0));
        var plusY = view.ToScreen(new Vector3(0, 1, 0));

        Assert.Equal(new Vector2(100, 100), origin);
        Assert.Equal(new Vector2(110, 100), plusX);
        Assert.Equal(new Vector2(100, 90), plusY);
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

        Assert.Equal(view.ToScreen(new Vector2(2, -5)), view.ToScreen(new Vector3(2, 5, 3)));
    }
}
