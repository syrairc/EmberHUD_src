using System.Numerics;
using EmberHUD;
using EmberHUD.Testing;

namespace Reactions;

public sealed class MainTests
{
    static GameState Hurt() => new()
    {
        Status = "live",
        Paused = false,
        ChatOpen = false,
        Player = new Player(Vector3.Zero, Vector2.UnitX) { Life = 10, MaxLife = 100 },
    };

    static Main Plugin(params Condition[] conditions)
    {
        var plugin = new Main();
        plugin.Settings.Reactions = [new Reaction { Trigger = new Hotkey(Key.D1), Conditions = [.. conditions] }];
        plugin.Initialize();
        return plugin;
    }

    static void Replace(Main plugin, Settings settings) =>
        typeof(Plugin<Settings>).GetProperty("Settings")!.GetSetMethod(true)!.Invoke(plugin, [settings]);

    static Condition Health => new() { Kind = ConditionKind.Health, Percent = 50 };

    [Test]
    public void TickSendsOnePressWhenConditionHolds()
    {
        var sent = TestSend.Record();
        TestGame.Set(Hurt());
        Plugin(Health).Tick();
        Check.Equal(2, sent.Count);
        Check.That(sent[0].Down && sent[0].Key == Key.D1, "down");
        Check.That(!sent[1].Down && sent[1].Key == Key.D1, "up");
    }

    [Test]
    public void TickSendsNothingWhenNotAllowed()
    {
        var sent = TestSend.Record(allowed: false);
        TestGame.Set(Hurt());
        Plugin(Health).Tick();
        Check.Equal(0, sent.Count);
    }

    [Test]
    public void TickAsksForCharacterOnlyForBuffOrSkillConditions()
    {
        TestSend.Record();
        TestGame.Set(Hurt());
        var buff = Plugin(new Condition { Kind = ConditionKind.Buff, Text = "x" });
        Check.That(TestGame.CharacterAskedDuring(buff.Tick), "buff asks");
        var health = Plugin(Health);
        Check.That(!TestGame.CharacterAskedDuring(health.Tick), "health does not ask");
    }

    [Test]
    public void TickUsesTheReplacedReactionsAndSurvivesNulls()
    {
        var sent = TestSend.Record();
        TestGame.Set(Hurt());
        var plugin = Plugin(Health);
        plugin.Tick();
        sent.Clear();
        Replace(plugin, new Settings { Reactions = [null!, new Reaction { Trigger = new Hotkey(Key.D2), Conditions = null! }, new Reaction { Trigger = new Hotkey(Key.D3), Conditions = [null!, Health] }] });
        plugin.Tick();
        Check.Equal(0, sent.Count, "null condition entry is dropped, so the reaction with it never throws");
    }

    [Test]
    public void TickFiresTheNewSettingsReaction()
    {
        var sent = TestSend.Record();
        TestGame.Set(Hurt());
        var plugin = Plugin(Health);
        plugin.Tick();
        sent.Clear();
        System.Threading.Thread.Sleep(300);
        Replace(plugin, new Settings { Reactions = [new Reaction { Trigger = new Hotkey(Key.D2), Conditions = [Health] }] });
        plugin.Tick();
        Check.Equal(2, sent.Count);
        Check.Equal(Key.D2, sent[0].Key);
    }

    [Test]
    public void RenderDrawsRangeOnlyWhenShowRangeIsOn()
    {
        TestGame.Set(Hurt() with { Camera = new Camera(new Vector3(-1000, 0, 1000), new Vector3(-45, 0, 0), 90) });
        var range = new Condition { Kind = ConditionKind.NearbyEnemies, Radius = 300 };
        var plugin = Plugin(range);
        Check.Equal(0, TestFrame.Vertices(plugin.Render));
        range.ShowRange = true;
        Check.That(TestFrame.Vertices(plugin.Render) > 0);
        TestGame.Set(Hurt());
        Check.Equal(0, TestFrame.Vertices(plugin.Render), "no camera");
    }
}
