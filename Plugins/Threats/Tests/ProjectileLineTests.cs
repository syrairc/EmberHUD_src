using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Numerics;
using EmberHUD;
using EmberHUD.Testing;
using EmberHUD.Ui;

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
    };

    static Projectile Shot(Vector2? direction = null, float radius = 100) => new(1, 1, Rarity.Rare, new Vector2(0, 500), new Vector2(0, 500), direction ?? Vector2.UnitX, radius, 600);

    static List<(Vector2 Pos, uint Col)> Drawn(Camera? camera, Projectile projectile, Settings settings, Vector2 player, double seconds, bool? inside = null)
    {
        TestGame.Set(new GameState { Camera = camera });
        var found = new List<(Vector2, uint)>();
        bool? result = null;
        TestFrame.Run(() =>
        {
            var list = Draw.Background;
            var first = list.VtxBuffer.Size;
            result = ProjectileLine.Draw(projectile, settings, player, seconds);
            for (var i = first; i < list.VtxBuffer.Size; i++) found.Add((list.VtxBuffer[i].Pos, list.VtxBuffer[i].Col));
        });
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
}
