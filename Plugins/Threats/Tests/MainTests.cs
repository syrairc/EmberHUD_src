using System;
using System.Diagnostics;
using System.Numerics;
using System.Reflection;
using EmberHUD;
using EmberHUD.Testing;
using EmberHUD.Ui;

namespace Threats;

public sealed class MainTests
{
    static readonly Camera Forward = new(Vector3.Zero, Vector3.Zero, 90);
    static readonly Vector2 Target = new(500, 0);
    static readonly Player Standing = new(new Vector3(500, 0, 0), Vector2.UnitX);
    static readonly Player Away = new(new Vector3(5000, 5000, 0), Vector2.UnitX);

    static Entity Caster(Rarity rarity = Rarity.Rare, bool cast = true) => new(1, 1, new Vector3(500, -300, 0), EntityKind.Monster, rarity)
    {
        Cast = cast ? new Cast(1, 1, 0.5f, 2, Target, Vector2.UnitX) : null,
    };

    static Projectile Shot(Rarity rarity = Rarity.Rare) => new(1, 1, rarity, new Vector2(0, 500), new Vector2(0, 500), Vector2.UnitX, 100, 600);

    static GameState State(Player? player, Entity[]? entities = null, Projectile[]? projectiles = null, GameUi? ui = null) => GameState.Empty with
    {
        Camera = Forward,
        Player = player,
        Entities = entities ?? [],
        Projectiles = projectiles ?? [],
        Ui = ui ?? GameUi.Empty,
    };

    static Main Plugin(Action<Settings>? configure = null, Func<long>? clock = null)
    {
        var main = new Main { Clock = clock ?? (() => 1000) };
        main.Settings.ShowCountdown = false;
        configure?.Invoke(main.Settings);
        return main;
    }

    static int Frame(Main main, GameState state)
    {
        TestGame.Set(state);
        try
        {
            return TestFrame.Vertices(main.Render);
        }
        finally
        {
            TestGame.Set(GameState.Empty);
        }
    }

    static int Frame(GameState state, Action<Settings>? configure = null) => Frame(Plugin(configure), state);

    static PropertyInfo Asked(string property) => typeof(Game).GetProperty(property, BindingFlags.NonPublic | BindingFlags.Static)!;

    [Test]
    public void RingIsGone()
    {
        Check.Equal(0, Frame(State(Away, [Caster(Rarity.Boss, false)])));
    }

    [Test]
    public void DrawsATelegraphForAnAllowedRarity()
    {
        Check.That(Frame(State(Away, [Caster(Rarity.Rare)])) > 0, "rare telegraph missing");
    }

    [Test]
    public void SkipsADisabledRarity()
    {
        Check.Equal(0, Frame(State(Away, [Caster(Rarity.Normal)])));
        Check.That(Frame(State(Away, [Caster(Rarity.Normal)]), s => s.TelegraphNormal = true) > 0, "enabled normal telegraph missing");
        Check.Equal(0, Frame(State(Away, [Caster(Rarity.Rare)]), s => s.TelegraphRare = false));
    }

    [Test]
    public void SkipsFriendlyAndHiddenMonsters()
    {
        Check.That(Frame(State(Away, [Caster()])) > 0, "control drew nothing");
        Check.Equal(0, Frame(State(Away, [Caster() with { Friendly = true }])));
        Check.Equal(0, Frame(State(Away, [Caster() with { Hidden = true }])));
    }

    [Test]
    public void OnlyMonstersTelegraph()
    {
        var ally = new Entity(1, 1, new Vector3(500, -300, 0), EntityKind.Ally, Rarity.Rare) { Cast = Caster().Cast };
        Check.Equal(0, Frame(State(Away, [ally])));
    }

    [Test]
    public void ShowOffDrawsNothing()
    {
        Check.Equal(0, Frame(State(Away, [Caster()], [Shot()]), s => s.Show = false));
    }

    [Test]
    public void ProjectilesFollowTheirOwnerRarity()
    {
        Check.That(Frame(State(Away, null, [Shot(Rarity.Rare)])) > 0, "rare line missing");
        Check.Equal(0, Frame(State(Away, null, [Shot(Rarity.Normal)])));
        Check.That(Frame(State(Away, null, [Shot(Rarity.Normal)]), s => s.LinesNormal = true) > 0, "enabled normal line missing");
        Check.Equal(0, Frame(State(Away, null, [Shot(Rarity.Boss)]), s => s.LinesBoss = false));
    }

    [Test]
    public void NothingDrawsWithoutAPlayer()
    {
        Check.Equal(0, Frame(State(null, [Caster()], [Shot()]), s => s.MoveAlert = true));
    }

    [Test]
    public void FullScreenPanelHidesEverything()
    {
        var panel = new GamePanel("map", PanelKind.WorldMap, Vector2.Zero, new Vector2(1920, 1080), true);
        var state = State(Away, [Caster()], null, new GameUi([panel]));
        Check.Equal(0, Frame(state));
        Check.That(Frame(state, s => s.HideBehindPanels = false) > 0, "panels hid telegraphs with HideBehindPanels off");
    }

    [Test]
    public void AlertShowsWhileInsideAndHoldsPointThreeSeconds()
    {
        var now = 1000L;
        var inside = State(Standing, [Caster()]);
        var outside = State(Away, [Caster()]);
        var withoutAlert = Frame(inside);
        var quiet = Frame(outside);
        var main = Plugin(s => s.MoveAlert = true, () => now);
        Check.That(Frame(main, inside) > withoutAlert, "no alert while inside");
        now += Stopwatch.Frequency / 10;
        Check.That(Frame(main, outside) > quiet, "alert dropped before 0.3 s");
        now += Stopwatch.Frequency / 5 + 1;
        Check.Equal(quiet, Frame(main, outside));
    }

    [Test]
    public void AlertIsOffWhenDisabled()
    {
        var inside = State(Standing, [Caster()]);
        var off = Frame(inside);
        Check.Equal(off, Frame(inside, s => s.MoveAlert = false));
        Check.That(Frame(inside, s => s.MoveAlert = true) > off, "alert did not draw when enabled");
    }

    [Test]
    public void NoAlertWhenOnlyOutside()
    {
        var outside = State(Away, [Caster()]);
        Check.Equal(Frame(outside), Frame(outside, s => s.MoveAlert = true));
    }

    [Test]
    public void CallsBothWantMethods()
    {
        var casts = Asked("CastsAsked");
        var projectiles = Asked("ProjectilesAsked");
        casts.SetValue(null, 0L);
        projectiles.SetValue(null, 0L);
        Frame(State(Away));
        Check.That((long)casts.GetValue(null)! != 0, "WantCasts was not called");
        Check.That((long)projectiles.GetValue(null)! != 0, "WantProjectiles was not called");
    }
}
