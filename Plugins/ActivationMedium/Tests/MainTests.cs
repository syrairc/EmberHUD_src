using System.Numerics;
using EmberHUD;
using EmberHUD.Testing;

namespace ActivationMedium;

public sealed class MainTests
{
    static GameState Live(float enemyX = float.NaN, bool fullScreen = false, ScreenRect?[]? slots = null) => new()
    {
        Status = "live",
        Paused = false,
        ChatOpen = false,
        Ui = new GameUi(fullScreen ? [new GamePanel("map", default, Vector2.Zero, Vector2.One, true)] : []),
        Player = new Player(Vector3.Zero, Vector2.UnitX),
        Entities = float.IsNaN(enemyX) ? [] : [new Entity(1, 100, new Vector3(enemyX, 0, 0), EntityKind.Monster, Rarity.Normal) { Address = 0x1000 }],
        SkillSlots = slots,
    };

    [Test]
    public void ActiveWhenAnEnemyIsNearOrToggled()
    {
        var settings = new Settings { Radius = 500 };
        Check.That(Main.Active(Live(100), settings, false), "near");
        Check.That(!Main.Active(Live(900), settings, false), "far");
        Check.That(!Main.Active(Live(), settings, false), "none");
        Check.That(Main.Active(Live(), settings, true), "toggled");
        Check.That(!Main.Active(Live(100), new Settings { NearbyEnabled = false }, false), "nearby off");
    }

    [Test]
    public void NeverActiveWhilePausedCoveredOrWithoutAPlayer()
    {
        Check.That(!Main.Active(Live(fullScreen: true), new Settings(), true), "full screen panel");
        Check.That(!Main.Active(Live() with { Paused = true }, new Settings(), true), "paused");
        Check.That(!Main.Active(Live() with { Paused = null }, new Settings(), true), "unknown pause");
        Check.That(!Main.Active(Live() with { Player = null }, new Settings(), true), "no player");
        Check.That(!Main.Active(Live() with { Status = "loading" }, new Settings(), true), "not live");
    }

    [Test]
    public void YieldsOnlyToAnotherKeyThatIsDown()
    {
        var space = new Hotkey(Key.Space);
        Check.That(Main.Yields(space, new Hotkey(Key.D1), true), "held");
        Check.That(!Main.Yields(space, new Hotkey(Key.D1), false), "not held");
        Check.That(!Main.Yields(default, new Hotkey(Key.D1), true), "no yield key");
        Check.That(!Main.Yields(space, space, true), "same as the hold key, that is our own press");
    }

    [Test]
    public void SlotRectIsOneBasedAndNullOutOfRange()
    {
        var rect = new ScreenRect(new Vector2(1, 2), new Vector2(3, 4));
        var state = Live(slots: [null, rect]);
        Check.That(Main.SlotRect(state, 2) == rect, "slot 2");
        Check.That(Main.SlotRect(state, 1) is null, "unread slot");
        Check.That(Main.SlotRect(state, 0) is null && Main.SlotRect(state, 3) is null, "out of range");
        Check.That(Main.SlotRect(Live(), 1) is null, "no slots");
    }

    [Test]
    public void HoldsOnceWhileAnEnemyIsNearAndReleasesWhenItLeaves()
    {
        var sent = TestSend.Record();
        var plugin = new Main();
        TestGame.Set(Live(100));
        plugin.Tick();
        plugin.Tick();
        Check.Equal(1, sent.Count, "one key down, not one per tick");
        Check.That(sent[0].Down && sent[0].Key == Key.D1, "down");
        TestGame.Set(Live());
        plugin.Tick();
        Check.Equal(2, sent.Count);
        Check.That(!sent[1].Down && sent[1].Key == Key.D1, "up");
    }

    [Test]
    public void PressesAgainAfterTheGameLosesFocus()
    {
        var sent = TestSend.Record();
        var plugin = new Main();
        TestGame.Set(Live(100));
        plugin.Tick();
        TestSend.Record(allowed: false);
        plugin.Tick();
        sent = TestSend.Record();
        plugin.Tick();
        Check.Equal(1, sent.Count, "held again once the game is back");
    }

    [Test]
    public void ChangingTheHoldKeyReleasesTheOldOne()
    {
        var sent = TestSend.Record();
        var plugin = new Main();
        TestGame.Set(Live(100));
        plugin.Tick();
        plugin.Settings.HoldKey = new Hotkey(Key.D2);
        plugin.Tick();
        Check.That(sent.Count == 3 && !sent[1].Down && sent[1].Key == Key.D1 && sent[2].Down && sent[2].Key == Key.D2, "up old, down new");
    }

    [Test]
    public void NoHoldKeySendsNothing()
    {
        var sent = TestSend.Record();
        var plugin = new Main();
        plugin.Settings.HoldKey = default;
        TestGame.Set(Live(100));
        plugin.Tick();
        Check.Equal(0, sent.Count);
    }

    [Test]
    public void TickAsksForTheCharacterSoSlotsAreRead()
    {
        TestSend.Record();
        TestGame.Set(Live());
        Check.That(TestGame.CharacterAskedDuring(new Main().Tick));
    }

    [Test]
    public void DisableReleasesTheKey()
    {
        var sent = TestSend.Record();
        var plugin = new Main();
        TestGame.Set(Live(100));
        plugin.Tick();
        plugin.OnDisable();
        Check.That(sent.Count == 2 && !sent[1].Down, "up on disable");
    }
}
