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
    public void LabelAnchorIsOnThePathWhenOffsetIsZero() =>
        Check.Equal<Vector2?>(new Vector2(600, 0), Main.LabelAnchor(Straight(1000), Vector2.Zero, 600, 0));

    [Test]
    public void LabelAnchorShiftsSideways()
    {
        var anchor = Main.LabelAnchor(Straight(1000), Vector2.Zero, 600, 120)!.Value;
        var other = Main.LabelAnchor(Straight(1000), Vector2.Zero, 600, -120)!.Value;

        Check.Equal(600f, anchor.X);
        Check.Equal(120f, MathF.Abs(anchor.Y));
        Check.Equal(-anchor.Y, other.Y);
    }

    [Test]
    public void LabelAnchorClampsToPathEnd() =>
        Check.Equal<Vector2?>(new Vector2(300, 0), Main.LabelAnchor(Straight(300), Vector2.Zero, 600, 0));

    [Test]
    public void LabelAnchorNullWhenUnreachableOrOnTarget()
    {
        Check.Equal<Vector2?>(null, Main.LabelAnchor(Unreachable(1, "x"), Vector2.Zero, 600, 0));
        Check.Equal<Vector2?>(null, Main.LabelAnchor(Reachable(1, "x", [Vector2.Zero], 0), Vector2.Zero, 600, 0));
    }

    [Test]
    public void LabelAnchorCountsFromThePlayerNotPathStart() =>
        Check.Equal<Vector2?>(new Vector2(500, 0), Main.LabelAnchor(Straight(1000), new Vector2(200, 5), 300, 0));

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
