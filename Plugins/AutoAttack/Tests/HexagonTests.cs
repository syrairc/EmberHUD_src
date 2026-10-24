using System.Numerics;
using EmberHUD.Testing;

namespace AutoAttack;

public sealed class HexagonTests
{
    [Test]
    public void PointsTouchEveryEdgeOfTheBoundsAndAreSymmetric()
    {
        var points = Hexagon.Points(new Vector2(100, 200), new Vector2(160, 280));
        Check.Equal(6, points.Length);
        Check.That(points[0] == new Vector2(130, 200) && points[3] == new Vector2(130, 280), "top and bottom point");
        Check.That(points[1] == new Vector2(160, 220) && points[2] == new Vector2(160, 260), "right side");
        Check.That(points[5] == new Vector2(100, 220) && points[4] == new Vector2(100, 260), "left side");
    }
}
