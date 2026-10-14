namespace EmberHUD.Tests;

public class PlayerSmootherTests
{
    private const long Hz = 1000;

    [Fact]
    public void TheFirstSampleIsShownAsIs()
    {
        var smoother = new PlayerSmoother();

        Assert.Equal(new Vector3(5, 0, 5), smoother.Update(new Vector3(5, 0, 5), 100, 100, Hz));
    }

    [Fact]
    public void ANewSampleIsReachedOverTheLastSampleInterval()
    {
        var smoother = new PlayerSmoother();
        smoother.Update(new Vector3(0, 0, 0), 1000, 1000, Hz);

        smoother.Update(new Vector3(4, 0, 0), 1040, 1040, Hz);

        Assert.Equal(new Vector3(2, 0, 0), smoother.Update(new Vector3(4, 0, 0), 1040, 1060, Hz));
        Assert.Equal(new Vector3(4, 0, 0), smoother.Update(new Vector3(4, 0, 0), 1040, 1080, Hz));
        Assert.Equal(new Vector3(4, 0, 0), smoother.Update(new Vector3(4, 0, 0), 1040, 1200, Hz));
    }

    [Fact]
    public void ASampleArrivingMidGlideContinuesFromWhereTheDotIs()
    {
        var smoother = new PlayerSmoother();
        smoother.Update(new Vector3(0, 0, 0), 1000, 1000, Hz);
        smoother.Update(new Vector3(4, 0, 0), 1040, 1040, Hz);
        var mid = smoother.Update(new Vector3(4, 0, 0), 1040, 1060, Hz);

        var next = smoother.Update(new Vector3(8, 0, 0), 1060, 1060, Hz);

        Assert.Equal(mid, next);
    }

    [Fact]
    public void ATeleportSnaps()
    {
        var smoother = new PlayerSmoother();
        smoother.Update(new Vector3(0, 0, 0), 1000, 1000, Hz);

        Assert.Equal(new Vector3(100, 0, 0), smoother.Update(new Vector3(100, 0, 0), 1040, 1040, Hz));
    }

    private static Vector3 With(int axis, float value) => axis switch
    {
        0 => new Vector3(value, 5, 5),
        1 => new Vector3(5, value, 5),
        _ => new Vector3(5, 5, value),
    };

    [Theory]
    [InlineData(0, float.NaN)]
    [InlineData(1, float.NaN)]
    [InlineData(2, float.NaN)]
    [InlineData(0, float.PositiveInfinity)]
    [InlineData(1, float.PositiveInfinity)]
    [InlineData(2, float.PositiveInfinity)]
    [InlineData(0, float.NegativeInfinity)]
    [InlineData(1, float.NegativeInfinity)]
    [InlineData(2, float.NegativeInfinity)]
    public void NonFiniteOnAnyAxisKeepsTheLastGoodPosition(int axis, float value)
    {
        var smoother = new PlayerSmoother();
        var good = smoother.Update(new Vector3(10, 20, 30), 0, 0, 1000);

        Assert.Equal(good, smoother.Update(With(axis, value), 1, 1, 1000));
    }

    [Theory]
    [InlineData(0, float.NaN)]
    [InlineData(1, float.PositiveInfinity)]
    [InlineData(2, float.NegativeInfinity)]
    public void NonFiniteFirstSampleReturnsNoPlayer(int axis, float value)
    {
        var smoother = new PlayerSmoother();

        Assert.Null(smoother.Update(With(axis, value), 0, 0, 1000));
    }

    [Fact]
    public void FirstFiniteSampleAfterNonFiniteOnesIsShownAsIs()
    {
        var smoother = new PlayerSmoother();
        Assert.Null(smoother.Update(new Vector3(float.NaN, 0, 0), 0, 0, 1000));

        Assert.Equal(new Vector3(7, 8, 9), smoother.Update(new Vector3(7, 8, 9), 1, 1, 1000));
    }

    [Fact]
    public void AFiniteSampleAfterANonFiniteOneGlidesFromTheLastGoodPosition()
    {
        var smoother = new PlayerSmoother();
        var good = smoother.Update(new Vector3(10, 20, 0), 1000, 1000, 1000);
        smoother.Update(new Vector3(float.NaN, float.NaN, float.NaN), 1010, 1010, 1000);

        var recovered = smoother.Update(new Vector3(12, 20, 0), 1040, 1040, 1000);

        Assert.True(recovered is { X: var x, Y: var y, Z: var z } && float.IsFinite(x) && float.IsFinite(y) && float.IsFinite(z));
        Assert.Equal(good, recovered);
        Assert.Equal(new Vector3(12, 20, 0), smoother.Update(new Vector3(12, 20, 0), 1040, 1100, 1000));
    }
}
