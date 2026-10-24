using System.Numerics;
using EmberHUD;
using EmberHUD.Testing;

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
    public void RenderDrawsNothingOnFullScreen() => Check.Equal(0, Drawn(Plugin(Life(percent: 100, direction: Direction.Below)), Alive(true)));

    [Test]
    public void RenderDrawsSomethingForShownAura() => Check.That(Drawn(Plugin(Life(percent: 50, direction: Direction.Above)), Alive()) > 0);

    [Test]
    public void RenderSkipsHiddenAndKeepsDimmed()
    {
        var unmet = Life(percent: 50, direction: Direction.Below);
        Check.Equal(0, Drawn(Plugin(unmet), Alive()));
        var dimmed = Life(WhenInactive.Dim, 50, Direction.Below);
        Check.That(Drawn(Plugin(dimmed), Alive()) > 0);
    }
}
