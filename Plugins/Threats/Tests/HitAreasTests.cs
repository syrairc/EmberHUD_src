using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Numerics;
using EmberHUD;
using EmberHUD.Testing;
using EmberHUD.Ui;
using Hexa.NET.ImGui;

namespace Threats;

public sealed class HitAreasTests
{
    static readonly Vector2 Far = new(5000, 5000);
    static readonly Vector2 Centre = new(1000, 0);
    static readonly Color AreaColour = Color.FromArgb(200, 11, 22, 33);
    static readonly Color DangerColour = Color.FromArgb(255, 44, 55, 66);

    static Settings Configured() => new() { AreaColour = AreaColour, DangerColour = DangerColour, FillOpacity = 0.5f, ShowCountdown = false };

    static uint AreaFill(Settings settings) => EColor.U32(Groups.Fill(settings.AreaColour, settings));

    static uint DangerFill(Settings settings) => EColor.U32(Groups.Fill(settings.DangerColour, settings));

    static FightEntity Fighter(FightShape shape, int id = 50, Vector2? at = null, int owner = 1) =>
        new(id, new Vector3(at ?? Centre, 0), Vector2.UnitX, FightArchetype.Anchor, []) { Shape = shape, Camp = FightCamps.Hostile, OwnerId = owner };

    static FightEntity Circle(int radius = 300) => Fighter(new FightShape(1, (uint)radius, 0, 0));

    static FightEntity Rect() => Fighter(new FightShape(2, 0, 600, 200));

    static FightEntity Sector() => Fighter(new FightShape(4, 500, 0, 0, AngleA: -40, AngleB: 40));

    static FightEntity Ring(uint inner = 200) => Fighter(new FightShape(5, 400, 0, 0, Inner: inner));

    static ImDrawListPtr Frame(Settings settings, Action draw)
    {
        TestGame.Set(new GameState { Camera = Overdraw.Looking });
        ImDrawListPtr list = default;
        TestFrame.Run(() =>
        {
            list = Draw.Background;
            Groups.Declare(settings);
            draw();
        });
        return list;
    }

    static float Filled(FightEntity fighter, Vector2 player, uint colour, Settings settings, bool? inside = null)
    {
        bool? result = null;
        var list = Frame(settings, () => result = HitAreas.Draw(fighter, settings, player));
        if (inside is { } expected) Check.Equal(expected, result!.Value);
        return Overdraw.Area(list, colour);
    }

    [Test]
    public void EligibleNeedsAHostileAreaShape()
    {
        var none = new HashSet<int>();
        Check.That(HitAreas.Eligible(Circle(), none), "circle");
        Check.That(HitAreas.Eligible(Rect(), none), "rect");
        Check.That(HitAreas.Eligible(Sector(), none), "sector");
        Check.That(HitAreas.Eligible(Ring(), none), "ring");
        Check.That(HitAreas.Eligible(Circle() with { Camp = FightCamps.BossArea }, none), "boss area camp");
        Check.That(HitAreas.Eligible(Circle() with { Archetype = FightArchetype.Unknown }, none), "unknown archetype");
        Check.That(HitAreas.Eligible(Fighter(new FightShape(0x0101, 300, 0, 0)), none), "kind high bits");
    }

    [Test]
    public void EligibleRejectsEverythingElse()
    {
        var none = new HashSet<int>();
        Check.That(!HitAreas.Eligible(Circle() with { Camp = FightCamps.Player }, none), "player camp");
        Check.That(!HitAreas.Eligible(Circle() with { Camp = null }, none), "no camp");
        Check.That(!HitAreas.Eligible(Circle() with { Shape = null }, none), "no shape");
        Check.That(!HitAreas.Eligible(Fighter(new FightShape(3, 300, 100, 100)), none), "polygon");
        Check.That(!HitAreas.Eligible(Fighter(new FightShape(0, 300, 0, 0)), none), "kind 0");
        Check.That(!HitAreas.Eligible(Circle() with { Archetype = FightArchetype.Monster }, none), "monster");
        Check.That(!HitAreas.Eligible(Circle() with { Archetype = FightArchetype.BuffArea }, none), "buff area");
        Check.That(!HitAreas.Eligible(Circle(), new HashSet<int> { 50 }), "already a ground hazard");
        Check.That(!HitAreas.Eligible(Circle() with { Position = new Vector3(float.NaN, 0, 0) }, none), "NaN position");
        Check.That(!HitAreas.Eligible(Circle() with { Position = new Vector3(0, 1e9f, 0) }, none), "huge position");
    }

    [Test]
    public void EveryShapeKindDrawsAnArea()
    {
        var settings = Configured();
        foreach (var (name, fighter) in new[] { ("circle", Circle()), ("rect", Rect()), ("sector", Sector()), ("ring", Ring()) })
            Check.That(Filled(fighter, Far, AreaFill(settings), settings, false) > 0, $"{name} drew nothing");
    }

    [Test]
    public void StandingInsideTurnsItToDanger()
    {
        var settings = Configured();
        Check.That(Filled(Circle(), Centre, DangerFill(settings), settings, true) > 0, "no danger fill");
        Check.Equal(0f, Filled(Circle(), Centre, AreaFill(settings), settings, true));
        Check.Equal(0f, Filled(Circle(), Far, DangerFill(settings), settings, false));
        Filled(Rect(), Centre, 0, settings, true);
        Filled(Sector(), Vector2.Zero, 0, settings, false);
        Filled(Ring(), Centre, 0, settings, false);
        Filled(Ring(), Centre + new Vector2(300, 0), 0, settings, true);
    }

    [Test]
    public void RingKeepsItsHole()
    {
        var settings = Configured();
        var band = Filled(Ring(300), Far, AreaFill(settings), settings);
        var solid = Filled(Ring(0), Far, AreaFill(settings), settings);
        Check.That(band > 0, "band drew nothing");
        Check.That(solid > 0 && band / solid < 0.6f, $"hole not cut {band} vs {solid}");
    }

    static int White(FightEntity fighter, Settings settings)
    {
        var list = Frame(settings, () => HitAreas.Draw(fighter, settings, Far));
        var white = EColor.U32(Color.White);
        var count = 0;
        for (var i = 0; i < list.VtxBuffer.Size; i++)
            if (list.VtxBuffer[i].Col == white) count++;
        return count;
    }

    [Test]
    public void PendingAttackShowsItsCountdown()
    {
        var pending = Circle() with { Attack = new FightAttack(1.5, 0.5, 1, 0) };
        Check.That(White(pending, Configured()) > 0, "no countdown text");
        Check.Equal(0, White(Circle(), Configured()));
        Check.Equal(0, White(Circle() with { Attack = new FightAttack(0, 0.5, 1, 0) }, Configured()));
        Check.Equal(0, White(Circle() with { Attack = new FightAttack(double.NaN, 0.5, 1, 0) }, Configured()));
        Check.Equal(0, White(pending, new Settings { AreaColour = AreaColour, HazardTimers = false }));
    }

    [Test]
    public void GarbageDrawsNothing()
    {
        var settings = Configured();
        FightEntity[] bad =
        [
            Circle() with { Position = new Vector3(float.NaN, 0, 0) },
            Fighter(new FightShape(5, 400, 0, 0, Inner: 100, OffsetX: double.NaN)),
            Fighter(new FightShape(2, 0, 600, 200)) with { Direction = new Vector2(float.NaN, 0) },
            Circle(0),
        ];
        foreach (var fighter in bad)
        {
            var drawn = true;
            var list = Frame(settings, () => drawn = HitAreas.Draw(fighter, settings, Centre));
            Check.That(!drawn, "covered the player");
            Check.Equal(0, list.VtxBuffer.Size);
        }
    }

    static Entity Caster() => new(1, 1, new Vector3(500, -300, 0), EntityKind.Monster, Rarity.Rare)
    {
        Cast = new Cast(1, 1, 0.5f, 2, new Vector2(500, 0), Vector2.UnitX),
    };

    static int Rendered(Action<Settings>? configure, FightEntity[]? fighters, GroundHazard[]? hazards = null, bool withCaster = true)
    {
        var main = new Main { Clock = () => 1000 };
        main.Settings.ShowCountdown = false;
        configure?.Invoke(main.Settings);
        TestGame.Set(GameState.Empty with
        {
            Camera = Overdraw.Looking,
            Player = new Player(new Vector3(5000, 5000, 0), Vector2.UnitX),
            Entities = withCaster ? [Caster()] : [],
            Fighters = fighters,
            GroundHazards = hazards ?? [],
        });
        try
        {
            ImDrawListPtr list = default;
            TestFrame.Run(() =>
            {
                list = Draw.Background;
                main.Render();
            });
            return list.VtxBuffer.Size;
        }
        finally
        {
            TestGame.Set(GameState.Empty);
        }
    }

    [Test]
    public void RenderDrawsHitAreasOnlyWithFightDataAndTheSetting()
    {
        FightEntity[] areas = [Circle()];
        Check.That(Rendered(null, areas, withCaster: false) > 0, "control drew nothing");
        Check.Equal(0, Rendered(s => s.HitAreas = false, areas, withCaster: false));
        Check.Equal(0, Rendered(s => s.UseFightData = false, areas, withCaster: false));
        Check.Equal(0, Rendered(null, null, withCaster: false));
    }

    [Test]
    public void RenderSkipsAreasThatAreAlreadyGroundHazards()
    {
        Check.Equal(0, Rendered(s => s.GroundAreas = false, [Circle()], [new GroundHazard(50, HazardKind.Area, Far, 10)], false));
        Check.That(Rendered(s => s.GroundAreas = false, [Circle()], [new GroundHazard(51, HazardKind.Area, Far, 10)], false) > 0, "other id");
    }

    [Test]
    public void ARealAreaReplacesThePlaceholderOfItsCaster()
    {
        var owned = Rendered(null, [Circle()]);
        var areaOnly = Rendered(null, [Circle()], withCaster: false);
        var strangers = Rendered(null, [Circle() with { OwnerId = 2 }]);
        Check.That(Rendered(null, null) > 0, "placeholder control drew nothing");
        Check.Equal(areaOnly, owned);
        Check.That(strangers > owned, "another owner must not hide the placeholder");
    }

    static int Standing(bool alert)
    {
        var main = new Main { Clock = () => 1000 };
        main.Settings.MoveAlert = alert;
        TestGame.Set(GameState.Empty with { Camera = Overdraw.Looking, Player = new Player(new Vector3(Centre, 0), Vector2.UnitX), Fighters = [Circle()] });
        try
        {
            ImDrawListPtr list = default;
            TestFrame.Run(() =>
            {
                list = Draw.Background;
                main.Render();
            });
            return list.VtxBuffer.Size;
        }
        finally
        {
            TestGame.Set(GameState.Empty);
        }
    }

    [Test]
    public void StandingInAHitAreaRaisesTheAlert()
    {
        Check.That(Standing(true) > Standing(false), "no move alert");
    }
}
