using System;
using System.Drawing;
using System.Numerics;
using EmberHUD;
using EmberHUD.Testing;

namespace NavigationPlugin;

public sealed class LegendTests
{
    static PathState Reachable(int id, string name, Vector2[] points, float length) => new(id, name, Color.Red, points[^1], points, length);

    static PathState Unreachable(int id, string name) => new(id, name, Color.Blue, Vector2.Zero, null, 0);

    static PathState Straight(float length) => Reachable(1, "Boss", [Vector2.Zero, new Vector2(length, 0)], length);

    [Test]
    public void OneRowPerPath()
    {
        var rows = Legend.Rows([Straight(100), Unreachable(2, "Chest"), Straight(300)]);

        Check.Equal(3, rows.Count);
        Check.Equal("Boss", rows[0].Name);
        Check.Equal("Chest", rows[1].Name);
        Check.Equal(Color.Red, rows[0].Colour);
    }

    [Test]
    public void UnreachableRowIsDimmedWithDash()
    {
        var row = Legend.Rows([Unreachable(1, "Chest")])[0];

        Check.Equal("-", row.Distance);
        Check.That(row.Dimmed, "dimmed");
        Check.That(!Legend.Rows([Straight(10)])[0].Dimmed, "reachable is not dimmed");
    }

    [Test]
    public void DistanceIsPathLengthNotStraightLine()
    {
        var bent = Reachable(1, "Boss", [Vector2.Zero, new Vector2(100, 0), new Vector2(100, 100)], 200.4f);

        Check.Equal("200", Legend.Rows([bent])[0].Distance);
    }

    [Test]
    public void NoPathsNoRows() => Check.Equal(0, Legend.Rows([]).Count);

    [Test]
    public void PointAheadIsOnThePath() =>
        Check.Equal<Vector2?>(new Vector2(600, 0), Main.PointAhead(Straight(1000), Vector2.Zero, 600));

    [Test]
    public void PointAheadClampsToPathEnd() =>
        Check.Equal<Vector2?>(new Vector2(300, 0), Main.PointAhead(Straight(300), Vector2.Zero, 600));

    [Test]
    public void PointAheadIsNullWithoutAPath()
    {
        Check.Equal<Vector2?>(null, Main.PointAhead(Unreachable(1, "x"), Vector2.Zero, 600));
        Check.Equal<Vector2?>(null, Main.PointAhead(Reachable(1, "x", [Vector2.Zero], 0), Vector2.Zero, 600));
    }

    [Test]
    public void PointAheadCountsFromThePlayerNotPathStart() =>
        Check.Equal<Vector2?>(new Vector2(500, 0), Main.PointAhead(Straight(1000), new Vector2(200, 5), 300));

    static PathState LShape() => Reachable(1, "Boss", [Vector2.Zero, new Vector2(1000, 0), new Vector2(1000, 1000)], 2000);

    [Test]
    public void CompassPointsAlongThePathNotStraightAtTheTarget()
    {
        var direction = Main.CompassDirection(LShape(), Vector2.Zero, 600, 250)!.Value;

        Check.That(Vector2.Distance(direction, Vector2.UnitX) < 0.001f, "follows the first leg");
    }

    [Test]
    public void CompassPointsStraightAtAnUnreachableTarget()
    {
        var path = new PathState(1, "Chest", Color.Blue, new Vector2(0, 900), null, 0);

        Check.That(Vector2.Distance(Main.CompassDirection(path, Vector2.Zero, 600, 250)!.Value, Vector2.UnitY) < 0.001f, "straight at target");
    }

    [Test]
    public void CompassHidesWhenArrived()
    {
        Check.Equal<Vector2?>(null, Main.CompassDirection(Straight(1000), new Vector2(900, 0), 600, 250));
        Check.Equal<Vector2?>(null, Main.CompassDirection(Unreachable(1, "x"), new Vector2(100, 0), 600, 250));
    }

    [Test]
    public void CompassIsNullForZeroLength()
    {
        var path = new PathState(1, "x", Color.Red, new Vector2(500, 0), null, 0);

        Check.Equal<Vector2?>(null, Main.CompassDirection(path, new Vector2(500, 0), 600, 0));
        Check.Equal<Vector2?>(null, Main.CompassDirection(path, new Vector2(float.NaN, 0), 600, 250));
    }

    [Test]
    public void LegendHiddenWhenSettingOff()
    {
        var settings = new Settings();
        var open = new GameState { Ui = GameUi.Empty };
        var full = new GameState { Ui = new GameUi([new GamePanel("Map", PanelKind.WorldMap, default, new Vector2(100, 100), true)]) };

        Check.That(!Main.LegendHidden(settings, open), "shown by default");
        Check.That(Main.LegendHidden(settings, full), "hidden behind full-screen panel");
        settings.Legend.HideBehindPanels = false;
        Check.That(!Main.LegendHidden(settings, full), "panel ignored when setting off");
        settings.Legend.Show = false;
        Check.That(Main.LegendHidden(settings, open), "hidden when Show off");
    }
}
