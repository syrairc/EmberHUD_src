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

public sealed class ProjectileLineTests
{
    static readonly Color Line = Color.FromArgb(255, 11, 22, 33);
    static readonly Color Danger = Color.FromArgb(255, 77, 88, 99);
    static readonly Camera Forward = new(Vector3.Zero, Vector3.Zero, 90);
    static readonly Vector2 Far = new(5000, 5000);

    static Settings Configured(LineStyle style = LineStyle.Solid) => new()
    {
        Style = style,
        LineLength = 1000,
        LineWidth = 4,
        RadiusScale = 0.5f,
        LineColour = Line,
        DangerColour = Danger,
        AnimationSpeed = 100,
        ShowHitWidth = false,
    };

    static Projectile Shot(Vector2? direction = null, float radius = 100) => new(1, 1, Rarity.Rare, new Vector2(0, 500), new Vector2(0, 500), direction ?? Vector2.UnitX, radius, 600);

    static List<(Vector2 Pos, uint Col)> Drawn(Camera? camera, Projectile projectile, Settings settings, Vector2 player, double seconds, bool? inside = null)
    {
        TestGame.Set(new GameState { Camera = camera });
        var found = new List<(Vector2, uint)>();
        bool? result = null;
        ImDrawListPtr list = default;
        TestFrame.Run(() =>
        {
            list = Draw.Background;
            Groups.Declare(settings);
            result = ProjectileLine.Draw(projectile, settings, player, seconds);
        });
        for (var i = 0; i < list.VtxBuffer.Size; i++) found.Add((list.VtxBuffer[i].Pos, list.VtxBuffer[i].Col));
        if (inside is not null) Check.Equal(inside, result);
        return found;
    }

    static uint Alpha(uint colour) => colour >> 24;

    [Test]
    public void InsideUsesTheRadiusScaleAndSlack()
    {
        var settings = Configured();
        Check.That(ProjectileLine.Inside(Shot(), settings, new Vector2(500, 500 + 69)), "just inside 50 + 20");
        Check.That(!ProjectileLine.Inside(Shot(), settings, new Vector2(500, 500 + 71)), "just outside");
        Check.That(ProjectileLine.Inside(Shot(), settings, new Vector2(500, 570)), "exactly on the edge");
        Check.That(ProjectileLine.Inside(Shot(), settings, new Vector2(500, 500)), "on the line");
    }

    [Test]
    public void PlayerBehindTheProjectileIsOutside()
    {
        Check.That(!ProjectileLine.Inside(Shot(), Configured(), new Vector2(-200, 500)), "behind");
    }

    [Test]
    public void PlayerBeyondTheLineEndIsOutside()
    {
        Check.That(!ProjectileLine.Inside(Shot(), Configured(), new Vector2(1200, 500)), "beyond");
    }

    [Test]
    public void SolidDrawsOneColour()
    {
        var vertices = Drawn(Forward, Shot(), Configured(), Far, 0, inside: false);
        Check.That(vertices.Count > 0, "drawn");
        Check.That(vertices.All(v => v.Col == EColor.U32(Line)), "single colour");
    }

    [Test]
    public void GradientFadesWithDistance()
    {
        var vertices = Drawn(Forward, Shot(), Configured(LineStyle.Gradient), Far, 0);
        Check.That(vertices.Count > 0, "drawn");
        Check.That(Alpha(vertices.First().Col) > Alpha(vertices.Last().Col), "alpha does not fade");
    }

    [Test]
    public void AnimatedPhaseMovesTheDashes()
    {
        var settings = Configured(LineStyle.Animated);
        var a = Drawn(Forward, Shot(), settings, Far, 0).Select(v => v.Pos).ToArray();
        var b = Drawn(Forward, Shot(), settings, Far, 0.4).Select(v => v.Pos).ToArray();
        Check.That(a.Length > 0 && b.Length > 0 && !a.SequenceEqual(b), "dashes did not move");
    }

    [Test]
    public void DangerColourReplacesTheLineColourWhenInside()
    {
        var settings = Configured();
        var hit = Drawn(Forward, Shot(), settings, new Vector2(500, 500), 0, inside: true);
        Check.That(hit.Count > 0 && hit.All(v => v.Col == EColor.U32(Danger)), "danger colour");
    }

    [Test]
    public void ZeroLengthDirectionDrawsNothing()
    {
        Check.Equal(0, Drawn(Forward, Shot(Vector2.Zero), Configured(), Far, 0, inside: false).Count);
    }

    [Test]
    public void GarbageReadsDrawNothingAndDoNotThrow()
    {
        var settings = Configured(LineStyle.Gradient);
        Check.Equal(0, Drawn(Forward, Shot(new Vector2(float.NaN, 1)), settings, Far, 0, inside: false).Count);
        Check.Equal(0, Drawn(Forward, Shot(radius: float.PositiveInfinity), settings, Far, 0, inside: false).Count);
        var bad = Configured(LineStyle.Animated);
        bad.LineLength = float.NaN;
        bad.LineWidth = float.NaN;
        bad.AnimationSpeed = float.NaN;
        Drawn(Forward, Shot(), bad, Far, double.NaN);
    }

    [Test]
    public void NoCameraStillReportsInside()
    {
        Check.Equal(0, Drawn(null, Shot(), Configured(), new Vector2(500, 500), 0, inside: true).Count);
    }

    [Test]
    public void HitWidthIsOnByDefault()
    {
        Check.That(new Settings().ShowHitWidth, "hit width off by default");
    }

    [Test]
    public void HitWidthDrawsAFaintBandInTheLineColour()
    {
        var settings = Configured();
        var plain = Drawn(Forward, Shot(), settings, Far, 0);
        settings.ShowHitWidth = true;
        var banded = Drawn(Forward, Shot(), settings, Far, 0, inside: false);
        Check.That(banded.Count > plain.Count, "no band drawn");
        Check.That(banded.Any(v => v.Col == EColor.U32(Color.FromArgb(Line.A / 4, Line))), "no faint line coloured band");
    }

    [Test]
    public void HitWidthBandTurnsDangerWhenInside()
    {
        var settings = Configured();
        settings.ShowHitWidth = true;
        var hit = Drawn(Forward, Shot(), settings, new Vector2(500, 500), 0, inside: true);
        Check.That(hit.Any(v => v.Col == EColor.U32(Color.FromArgb(Danger.A / 4, Danger))), "band not danger coloured");
    }

    static Projectile Orbiter() => Shot() with { Position = new Vector2(1000, 0) } with { OrbitCentre = Vector2.Zero };

    [Test]
    public void OrbitingProjectilesAreInsideNearTheRingNotNearTheProjectile()
    {
        var settings = Configured();
        Check.That(ProjectileLine.Inside(Orbiter(), settings, new Vector2(-1000, 0)), "far side of the ring");
        Check.That(ProjectileLine.Inside(Orbiter(), settings, new Vector2(0, 1069)), "just inside 50 + 20");
        Check.That(!ProjectileLine.Inside(Orbiter(), settings, new Vector2(0, 1071)), "just outside");
        Check.That(!ProjectileLine.Inside(Orbiter(), settings, Vector2.Zero), "centre");
    }

    [Test]
    public void OrbitingProjectilesSharingARingDrawItOnce()
    {
        var settings = Configured();
        var rings = new HashSet<(int, int)>();
        var once = 0;
        var twice = 0;
        TestGame.Set(new GameState { Camera = Forward });
        TestFrame.Run(() =>
        {
            var list = Draw.Background;
            var first = list.VtxBuffer.Size;
            ProjectileLine.Draw(Orbiter(), settings, Far, 0, rings);
            once = list.VtxBuffer.Size - first;
            ProjectileLine.Draw(Orbiter() with { Position = new Vector2(0, 1000) }, settings, Far, 0, rings);
            twice = list.VtxBuffer.Size - first;
        });
        Check.That(once > 0, "ring drawn");
        Check.Equal(once, twice);
    }

    static float LineArea(Settings settings, params Projectile[] shots)
    {
        TestGame.Set(new GameState { Camera = Overdraw.Looking });
        ImDrawListPtr list = default;
        TestFrame.Run(() =>
        {
            list = Draw.Background;
            Groups.Declare(settings);
            foreach (var shot in shots) ProjectileLine.Draw(shot, settings, Far, 0);
        });
        return Overdraw.Area(list, EColor.U32(settings.LineColour));
    }

    [Test]
    public void SolidLinesMerge()
    {
        var settings = Configured();
        settings.LineColour = Color.FromArgb(200, 11, 22, 33);
        var single = LineArea(settings, Shot());
        var stack = new Projectile[5];
        for (var i = 0; i < stack.Length; i++) stack[i] = Shot() with { Position = new Vector2(0, 500 + i * 0.5f) };
        var merged = LineArea(settings, stack);
        Check.That(single > 0, "nothing drawn");
        Check.That(merged < single * 2, $"lines stacked {merged} vs {single}");
    }

    [Test]
    public void GradientStaysUngrouped()
    {
        var vertices = Drawn(Forward, Shot(), Configured(LineStyle.Gradient), Far, 0);
        Check.Equal(24 * 4, vertices.Count);
    }

    [Test]
    public void ZeroRadiusDrawsNoBand()
    {
        var settings = Configured();
        settings.ShowHitWidth = true;
        Check.Equal(Drawn(Forward, Shot(radius: 0), Configured(), Far, 0).Count, Drawn(Forward, Shot(radius: 0), settings, Far, 0).Count);
    }
}
