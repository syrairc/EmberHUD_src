using System;
using System.Drawing;
using System.Numerics;
using EmberHUD;
using EmberHUD.Testing;
using EmberHUD.Ui;

namespace EmberAuras;

public sealed class MainTests
{
    static GameState Alive(bool fullScreen = false, params Buff[] buffs) => new()
    {
        Player = new Player(Vector3.Zero, Vector2.UnitX) { Life = 100, MaxLife = 100 },
        Buffs = buffs,
        Ui = fullScreen ? new GameUi([new GamePanel("map", PanelKind.WorldMap, Vector2.Zero, new Vector2(100), true)]) : GameUi.Empty,
    };

    static Aura Life(WhenInactive whenInactive = WhenInactive.Hide, float percent = 50, Direction direction = Direction.Below) => new()
    {
        WhenInactive = whenInactive,
        Conditions = [new Condition { Kind = ConditionKind.Health, Percent = percent, Direction = direction }],
    };

    static Main Plugin(params Aura[] auras)
    {
        var plugin = new Main();
        plugin.Settings.Groups = [new Group { Auras = [.. auras] }];
        plugin.Initialize();
        return plugin;
    }

    static int Drawn(Main plugin, GameState state)
    {
        TestGame.Set(state);
        return TestFrame.Vertices(() =>
        {
            plugin.Tick();
            plugin.Render();
        });
    }

    [Test]
    public void ShouldDrawIsFalseWithoutPlayer() => Check.That(!Main.ShouldDraw(new GameState(), new Settings()));

    [Test]
    public void ShouldDrawIsFalseOnFullScreenWhenHiding() => Check.That(!Main.ShouldDraw(Alive(true), new Settings { HideOnFullScreen = true }));

    [Test]
    public void ShouldDrawIsTrueOnFullScreenWhenSettingOff() => Check.That(Main.ShouldDraw(Alive(true), new Settings { HideOnFullScreen = false }));

    [Test]
    public void TickAsksForCharacterOnlyWithBuffOrSkillCondition()
    {
        var buff = Plugin(new Aura { Conditions = [new Condition { Kind = ConditionKind.Buff, Text = "x" }] });
        var health = Plugin(Life());
        var disabled = Plugin(new Aura { Enabled = false, Conditions = [new Condition { Kind = ConditionKind.SkillCooldown }] });
        TestGame.Set(Alive());
        Check.That(TestGame.CharacterAskedDuring(buff.Tick), "buff asks");
        Check.That(!TestGame.CharacterAskedDuring(health.Tick), "health does not ask");
        Check.That(!TestGame.CharacterAskedDuring(disabled.Tick), "disabled does not ask");
    }

    [Test]
    public void RenderDrawsNothingOnFullScreen()
    {
        var plugin = Plugin(Life(percent: 50, direction: Direction.Above));
        Check.That(Drawn(plugin, Alive()) > 0, "control draws");
        Check.Equal(0, Drawn(plugin, Alive(true)));
    }

    [Test]
    public void LockedEmptyGroupDrawsNothingAndUnlockedDrawsPlaceholder()
    {
        var plugin = Plugin(Life(percent: 50, direction: Direction.Below));
        Check.Equal(0, Drawn(plugin, Alive()));
        plugin.Settings.Lock = false;
        Check.That(Drawn(plugin, Alive()) > 0, "placeholder");
        Check.Equal(0, Drawn(plugin, Alive(true)));
    }

    [Test]
    public void SkippedFramesDoNotThrow()
    {
        var plugin = Plugin(Life(percent: 50, direction: Direction.Below));
        plugin.Settings.Groups.Add(new Group { Enabled = false });
        Drawn(plugin, Alive(true));
        Drawn(plugin, Alive());
    }

    [Test]
    public void RenderDrawsSomethingForShownAura() => Check.That(Drawn(Plugin(Life(percent: 50, direction: Direction.Above)), Alive()) > 0);

    static GameState LifeAt(double life) => new()
    {
        Player = new Player(Vector3.Zero, Vector2.UnitX) { Life = life, MaxLife = 100 },
        Ui = GameUi.Empty,
    };

    static int DrawnAura(Aura aura, GameState state)
    {
        var plugin = Plugin(aura);
        foreach (var text in aura.Texts) text.Enabled = false;
        return Drawn(plugin, state);
    }

    static Aura Styled(IconShape shape, int border = 0, Color? backdrop = null, WhenInactive whenInactive = WhenInactive.Hide, Direction direction = Direction.Above, float percent = 40)
    {
        var aura = Life(whenInactive, percent, direction);
        aura.Shape = shape;
        aura.BorderWidth = border;
        aura.BackdropColor = backdrop ?? Color.Transparent;
        return aura;
    }

    static int Outlined(IconShape shape, Action<Vector2[]> draw) => TestFrame.Vertices(() => draw(IconShapes.Outline(shape, Vector2.Zero, new Vector2(40), 8)));

    static void Fill(Vector2[] polygon) => IconShapes.FillPolygon(polygon, Color.White);

    [Test]
    public void CircleWithBorderDrawsMoreThanPlainSquare()
    {
        var plain = DrawnAura(Styled(IconShape.Circle), LifeAt(100));
        var bordered = DrawnAura(Styled(IconShape.Circle, 3), LifeAt(100));
        var stroke = Outlined(IconShape.Circle, o => IconShapes.StrokePolygon(o, Color.White, 3));
        Check.That(stroke > 0, "stroke measures");
        Check.Equal(plain + stroke, bordered);
    }

    [Test]
    public void BackdropOnlyAddsGeometryWhenAlphaIsAboveZero()
    {
        var plain = DrawnAura(Styled(IconShape.Square), LifeAt(100));
        Check.Equal(plain, DrawnAura(Styled(IconShape.Square, 0, Color.FromArgb(0, 9, 9, 9)), LifeAt(100)));
        var backdrop = Outlined(IconShape.Square, Fill);
        Check.Equal(plain + backdrop, DrawnAura(Styled(IconShape.Square, 0, Color.FromArgb(128, 9, 9, 9)), LifeAt(100)));
    }

    [Test]
    public void SweepIsClippedToTheShape()
    {
        foreach (var shape in new[] { IconShape.Square, IconShape.Circle, IconShape.Hexagon })
        {
            var unswept = DrawnAura(Styled(shape), LifeAt(100));
            var swept = DrawnAura(Styled(shape), LifeAt(60));
            var sweep = Outlined(shape, o => Fill(IconShapes.ClipTop(o, 16)));
            Check.That(sweep > 0, $"{shape} sweep measures");
            Check.Equal(unswept + sweep, swept, shape.ToString());
        }
        var square = Outlined(IconShape.Square, o => Fill(IconShapes.ClipTop(o, 16)));
        var circle = Outlined(IconShape.Circle, o => Fill(IconShapes.ClipTop(o, 16)));
        Check.That(square != circle, "the circle sweep is its own polygon");
    }

    [Test]
    public void DimmedStyledAuraStillDraws()
    {
        var plain = DrawnAura(Styled(IconShape.Hexagon, 0, null, WhenInactive.Dim, Direction.Below, 80), LifeAt(100));
        var styled = DrawnAura(Styled(IconShape.Hexagon, 2, Color.FromArgb(200, 0, 0, 0), WhenInactive.Dim, Direction.Below, 80), LifeAt(100));
        Check.That(plain > 0, "dim draws");
        Check.That(styled > plain, "dim keeps border and backdrop");
    }

    static int Bar(BarIcon icon, int border)
    {
        var aura = Styled(IconShape.Circle, border);
        aura.Style = AuraStyle.Bar;
        aura.BarIcon = icon;
        return DrawnAura(aura, LifeAt(50));
    }

    [Test]
    public void BarWithNoIconDrawsNoBorder() => Check.Equal(Bar(BarIcon.None, 0), Bar(BarIcon.None, 4));

    [Test]
    public void BarIconGetsTheBorder() => Check.That(Bar(BarIcon.Left, 4) > Bar(BarIcon.Left, 0));

    [Test]
    public void HugeOrNegativeBorderWidthIsClamped()
    {
        Check.Equal(DrawnAura(Styled(IconShape.Rounded, 0), LifeAt(100)), DrawnAura(Styled(IconShape.Rounded, -5), LifeAt(100)));
        Check.Equal(DrawnAura(Styled(IconShape.Rounded, 16), LifeAt(100)), DrawnAura(Styled(IconShape.Rounded, 100000), LifeAt(100)));
    }

    [Test]
    public void DefaultsDrawLikeBefore()
    {
        var aura = Life(percent: 50, direction: Direction.Above);
        var drawn = Drawn(Plugin(aura), Alive());
        var square = TestFrame.Vertices(() => IconShapes.FillPolygon(IconShapes.Outline(IconShape.Square, Vector2.Zero, new Vector2(40)), aura.FillColor));
        var text = TestFrame.Vertices(() => Draw.Text("100%", Vector2.Zero, Color.White));
        Check.Equal(square + text, drawn);
    }

    [Test]
    public void RenderSkipsHiddenAndKeepsDimmed()
    {
        var unmet = Life(percent: 50, direction: Direction.Below);
        Check.Equal(0, Drawn(Plugin(unmet), Alive()));
        var dimmed = Life(WhenInactive.Dim, 50, Direction.Below);
        Check.That(Drawn(Plugin(dimmed), Alive()) > 0);
    }
}
