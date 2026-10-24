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

    static GameState HalfLife() => new()
    {
        Player = new Player(Vector3.Zero, Vector2.UnitX) { Life = 50, MaxLife = 100 },
        Ui = GameUi.Empty,
    };

    static int DrawnStyled(IconShape shape, int border = 0, Color? backdrop = null, WhenInactive whenInactive = WhenInactive.Hide, GameState? state = null)
    {
        var aura = Life(whenInactive, 80, Direction.Below);
        aura.Shape = shape;
        aura.BorderWidth = border;
        aura.BackdropColor = backdrop ?? Color.Transparent;
        return Drawn(Plugin(aura), state ?? HalfLife());
    }

    [Test]
    public void CircleWithBorderDrawsMoreThanPlainSquare() =>
        Check.That(DrawnStyled(IconShape.Circle, 3) > DrawnStyled(IconShape.Square));

    [Test]
    public void BackdropOnlyAddsGeometryWhenAlphaIsAboveZero()
    {
        var plain = DrawnStyled(IconShape.Square);
        Check.Equal(plain, DrawnStyled(IconShape.Square, 0, Color.FromArgb(0, 9, 9, 9)));
        Check.That(DrawnStyled(IconShape.Square, 0, Color.FromArgb(128, 9, 9, 9)) > plain, "visible backdrop adds");
    }

    [Test]
    public void SweepIsClippedToTheShape()
    {
        var square = DrawnStyled(IconShape.Square);
        var circle = DrawnStyled(IconShape.Circle);
        Check.That(circle != square, "circle sweep is not the square sweep");
        var full = DrawnStyled(IconShape.Circle, state: Alive());
        Check.That(circle > full, "a half sweep adds geometry over a full icon");
    }

    [Test]
    public void DimmedStyledAuraStillDraws()
    {
        Check.That(DrawnStyled(IconShape.Hexagon, 2, Color.FromArgb(200, 0, 0, 0), whenInactive: WhenInactive.Dim, state: Alive()) > 0);
    }

    [Test]
    public void BarWithNoIconDrawsNoBorder()
    {
        int Bar(int border)
        {
            var aura = Life(WhenInactive.Hide, 80, Direction.Below);
            aura.Style = AuraStyle.Bar;
            aura.BarIcon = BarIcon.None;
            aura.BorderWidth = border;
            aura.Shape = IconShape.Circle;
            return Drawn(Plugin(aura), HalfLife());
        }
        Check.Equal(Bar(0), Bar(4));
    }

    [Test]
    public void HugeOrNegativeBorderWidthIsHarmless()
    {
        Check.That(DrawnStyled(IconShape.Rounded, -5) > 0);
        Check.That(DrawnStyled(IconShape.Rounded, 100000) > 0);
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
