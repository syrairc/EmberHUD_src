namespace EmberHUD.Api.Tests;

public sealed class CameraTests
{
    static readonly Vector2 Screen = new(1920, 1080);

    [Fact]
    public void APointStraightAheadLandsInTheMiddle() =>
        Assert.Equal(new Vector2(960, 540), new Camera(Vector3.Zero, Vector3.Zero, 90).ToScreen(new Vector3(500, 0, 0), Screen));

    [Fact]
    public void RightAndUpMoveRightAndUpOnScreen()
    {
        var at = new Camera(Vector3.Zero, Vector3.Zero, 90).ToScreen(new Vector3(1000, 100, 50), Screen)!.Value;

        Assert.Equal(1056, at.X, 0.01f);
        Assert.Equal(492, at.Y, 0.01f);
    }

    [Fact]
    public void BehindTheCameraIsNull() =>
        Assert.Null(new Camera(Vector3.Zero, Vector3.Zero, 90).ToScreen(new Vector3(-10, 0, 0), Screen));

    [Fact]
    public void BadFovIsNull()
    {
        Assert.Null(new Camera(Vector3.Zero, Vector3.Zero, 0).ToScreen(new Vector3(500, 0, 0), Screen));
        Assert.Null(new Camera(Vector3.Zero, Vector3.Zero, float.NaN).ToScreen(new Vector3(500, 0, 0), Screen));
    }

    [Fact]
    public void LookingDownAtFortyFiveDegreesCentresTheTargetAndNearerGroundSitsLower()
    {
        var camera = new Camera(new Vector3(-1000, 0, 1000), new Vector3(-45, 0, 0), 90);

        var target = camera.ToScreen(Vector3.Zero, Screen)!.Value;
        var nearer = camera.ToScreen(new Vector3(-100, 0, 0), Screen)!.Value;

        Assert.Equal(960, target.X, 0.01f);
        Assert.Equal(540, target.Y, 0.01f);
        Assert.True(nearer.Y > 540);
    }

    [Fact]
    public void YawTurnsTheView()
    {
        var at = new Camera(Vector3.Zero, new Vector3(0, 90, 0), 90).ToScreen(new Vector3(0, 500, 0), Screen)!.Value;

        Assert.Equal(960, at.X, 0.01f);
        Assert.Equal(540, at.Y, 0.01f);
    }
}
