using System;
using System.Collections.Generic;
using System.Numerics;
using EmberHUD;
using EmberHUD.Testing;

namespace Reactions;

public sealed class EngineTests
{
    static GameState Hurt(bool? paused = false, bool fullScreen = false, bool player = true) => new()
    {
        Status = "live",
        Paused = paused,
        Ui = new GameUi([new GamePanel("bag", default, Vector2.Zero, Vector2.One, fullScreen)]),
        Player = player ? new Player(Vector3.Zero, Vector2.UnitX) { Life = 10, MaxLife = 100 } : null,
    };

    static Reaction Heal(Key key = Key.D1, int cooldownMs = 1000, bool enabled = true) => new()
    {
        Enabled = enabled,
        Trigger = new Hotkey(key),
        CooldownMs = cooldownMs,
        Conditions = [new Condition { Kind = ConditionKind.Health, Percent = 50 }],
    };

    sealed record Rig(Engine Engine, Settings Settings)
    {
        public Reaction? Step(GameState state, long nowMs, Func<Key, bool> press) => Engine.Step(Settings, state, nowMs, press);
    }

    static Rig With(int globalMs, params Reaction[] reactions) => new(new Engine(), new Settings { GlobalCooldownMs = globalMs, Reactions = [.. reactions] });

    static readonly List<Key> Pressed = [];

    static bool Accept(Key key)
    {
        Pressed.Add(key);
        return true;
    }

    [Test]
    public void FiresWhenConditionsHold()
    {
        var reaction = Heal();
        Check.That(ReferenceEquals(With(0, reaction).Step(Hurt(), 0, _ => true), reaction));
    }

    [Test]
    public void DoesNotFireWhenPausedIsNullOrTrue()
    {
        var engine = With(0, Heal());
        Check.That(engine.Step(Hurt(paused: null), 0, _ => true) is null, "unknown");
        Check.That(engine.Step(Hurt(paused: true), 0, _ => true) is null, "paused");
    }

    [Test]
    public void DoesNotFireWithFullScreenPanelOrNoPlayerOrNotPlaying()
    {
        var engine = With(0, Heal());
        Check.That(engine.Step(Hurt(fullScreen: true), 0, _ => true) is null, "full screen");
        Check.That(engine.Step(Hurt(player: false), 0, _ => true) is null, "no player");
        Check.That(engine.Step(Hurt() with { Status = "read failed" }, 0, _ => true) is null, "not playing");
    }

    [Test]
    public void FiresAgainOnlyAfterReactionCooldown()
    {
        var engine = With(0, Heal(cooldownMs: 1000));
        Check.That(engine.Step(Hurt(), 0, _ => true) is not null);
        Check.That(engine.Step(Hurt(), 999, _ => true) is null, "still cooling");
        Check.That(engine.Step(Hurt(), 1000, _ => true) is not null, "elapsed");
    }

    [Test]
    public void GlobalCooldownBlocksSecondReaction()
    {
        var first = Heal(Key.D1);
        var second = Heal(Key.D2);
        var engine = With(500, first, second);
        Check.That(ReferenceEquals(engine.Step(Hurt(), 0, _ => true), first));
        Check.That(ReferenceEquals(engine.Step(Hurt(), 499, _ => true), null), "global cooling");
        Check.That(ReferenceEquals(engine.Step(Hurt(), 500, _ => true), second), "global elapsed, first still cooling");
    }

    [Test]
    public void ListOrderIsPriority()
    {
        var first = Heal(Key.D1);
        var second = Heal(Key.D2);
        Check.That(ReferenceEquals(With(0, first, second).Step(Hurt(), 0, _ => true), first));
    }

    [Test]
    public void RefusedPressStartsNoCooldownAndRetries()
    {
        var reaction = Heal();
        var engine = With(500, reaction);
        Check.That(engine.Step(Hurt(), 0, _ => false) is null, "refused");
        Check.That(ReferenceEquals(engine.Step(Hurt(), 1, _ => true), reaction), "retry next step");
    }

    [Test]
    public void RefusedPressFallsThroughToNextReaction()
    {
        var first = Heal(Key.D1);
        var second = Heal(Key.D2);
        Check.That(ReferenceEquals(With(0, first, second).Step(Hurt(), 0, key => key == Key.D2), second));
    }

    [Test]
    public void NonPositiveCooldownIsClampedToFiftyMs()
    {
        var engine = With(0, Heal(cooldownMs: 0));
        Check.That(engine.Step(Hurt(), 0, _ => true) is not null);
        Check.That(engine.Step(Hurt(), 49, _ => true) is null, "own clamp");
        Check.That(engine.Step(Hurt(), 50, _ => true) is not null);
        var negative = With(-5, Heal(cooldownMs: 1000), Heal(Key.D2, cooldownMs: -5));
        Check.That(negative.Step(Hurt(), 0, _ => true) is not null);
        Check.That(negative.Step(Hurt(), 49, _ => true) is null, "global clamp");
        Check.That(negative.Step(Hurt(), 50, _ => true) is not null);
    }

    [Test]
    public void KeyNoneAndDisabledNeverFire()
    {
        Check.That(With(0, Heal(Key.None)).Step(Hurt(), 0, _ => true) is null, "none");
        Check.That(With(0, Heal(enabled: false)).Step(Hurt(), 0, _ => true) is null, "disabled");
    }

    [Test]
    public void PressesOnlyTheTriggerKey()
    {
        Pressed.Clear();
        var reaction = Heal();
        reaction.Trigger = new Hotkey(Key.D3, KeyModifiers.Ctrl);
        With(0, reaction).Step(Hurt(), 0, Accept);
        Check.Equal(1, Pressed.Count);
        Check.Equal(Key.D3, Pressed[0]);
    }

    [Test]
    public void ReplacedReactionListFiresTheNewReactionsOnly()
    {
        var old = Heal(Key.D1);
        var rig = With(0, old);
        Check.That(ReferenceEquals(rig.Step(Hurt(), 0, _ => true), old));
        var replacement = Heal(Key.D2);
        rig.Settings.Reactions = [replacement];
        Check.That(ReferenceEquals(rig.Step(Hurt(), 100, _ => true), replacement));
    }
}
