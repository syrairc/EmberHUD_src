namespace EmberHUD.Api.Tests;

public class ExploredTrailTests
{
    [Fact]
    public void ThePointsConstructorCountsEveryPoint()
    {
        var trail = new ExploredTrail([new Vector2(1, 2), new Vector2(3, 4)], 250);

        Assert.Equal(2, trail.Count);
        Assert.Equal(250, trail.Radius);
        Assert.Equal(new Vector2(3, 4), trail.Buffer[1]);
    }

    [Fact]
    public void ABufferLongerThanTheCountIsKeptAsGiven()
    {
        var buffer = new Vector2[8];

        var trail = new ExploredTrail(buffer, 3, 100);

        Assert.Same(buffer, trail.Buffer);
        Assert.Equal(3, trail.Count);
    }
}
