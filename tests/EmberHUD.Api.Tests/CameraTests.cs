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

    static Vector2? Reference(Vector3 location, Vector3 rotation, float fov, Vector3 world, Vector2 viewport)
    {
        var (sp, cp) = MathF.SinCos(rotation.X * MathF.PI / 180);
        var (sy, cy) = MathF.SinCos(rotation.Y * MathF.PI / 180);
        var (sr, cr) = MathF.SinCos(rotation.Z * MathF.PI / 180);
        var forward = new Vector3(cp * cy, cp * sy, sp);
        var right = new Vector3(sr * sp * cy - cr * sy, sr * sp * sy + cr * cy, -sr * cp);
        var up = new Vector3(-(cr * sp * cy + sr * sy), cy * sr - cr * sp * sy, cr * cp);
        var delta = world - location;
        var depth = Vector3.Dot(delta, forward);
        if (depth < 1f) return null;
        var centre = viewport / 2;
        var focal = centre.X / MathF.Tan(fov * MathF.PI / 360);
        return new Vector2(centre.X + Vector3.Dot(delta, right) * focal / depth, centre.Y - Vector3.Dot(delta, up) * focal / depth);
    }

    [Theory]
    [InlineData(-45, 0, 0, 90)]
    [InlineData(-30, 120, 10, 70)]
    [InlineData(20, -80, -15, 100)]
    public void ManyPointsAgreeWithTheDirectFormula(float pitch, float yaw, float roll, float fov)
    {
        var location = new Vector3(-1200, 300, 900);
        var rotation = new Vector3(pitch, yaw, roll);
        var camera = new Camera(location, rotation, fov);
        for (var x = -2000; x <= 2000; x += 400)
            for (var y = -2000; y <= 2000; y += 400)
            {
                var world = new Vector3(x, y, 0);
                var expected = Reference(location, rotation, fov, world, Screen);
                var actual = camera.ToScreen(world, Screen);
                Assert.Equal(expected is null, actual is null);
                if (expected is { } e && actual is { } a)
                {
                    Assert.Equal(e.X, a.X, 0.01f);
                    Assert.Equal(e.Y, a.Y, 0.01f);
                }
            }
    }

    [Fact]
    public void TwoCamerasBuiltFromTheSameValuesAreEqual()
    {
        var a = new Camera(new Vector3(1, 2, 3), new Vector3(-45, 90, 0), 80);
        var b = new Camera(new Vector3(1, 2, 3), new Vector3(-45, 90, 0), 80);
        Assert.Equal(a, b);
        Assert.NotEqual(a, new Camera(new Vector3(1, 2, 3), new Vector3(-45, 91, 0), 80));
    }
}
