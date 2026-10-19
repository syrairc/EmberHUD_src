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

public sealed class TelegraphTests
{
    static readonly Vector2 Viewport = new(1920, 1080);
    static readonly Color Outline = Color.FromArgb(255, 11, 22, 33);
    static readonly Color FillBase = Color.FromArgb(200, 44, 55, 66);
    static readonly Color Danger = Color.FromArgb(255, 77, 88, 99);
    static readonly Camera Forward = new(Vector3.Zero, Vector3.Zero, 90);
    static readonly Vector2 Target = new(500, 0);
    static readonly Vector2 Caster = new(500, -300);
    static readonly Vector2 Far = new(5000, 5000);

    static Settings Configured() => new()
    {
        TelegraphColour = Outline,
        TelegraphFillColour = FillBase,
        DangerColour = Danger,
        FillOpacity = 0.5f,
        PlaceholderRadius = 250,
        ShowCountdown = false,
    };

    static Cast CastAt(float progress, float secondsLeft = 2, byte state = 1) => new(1, state, progress, secondsLeft, Target, Vector2.UnitX);

    static Entity CasterWith(Cast? cast) => new(1, 1, new Vector3(Caster, 0), EntityKind.Monster, Rarity.Rare) { Cast = cast };

    static uint FillColour(Color color, float opacity) => EColor.U32(Color.FromArgb((int)(color.A * opacity), color));

    static List<(Vector2 Pos, uint Col, Vector2 Uv)> Drawn(Camera? camera, Action draw)
    {
        TestGame.Set(new GameState { Camera = camera });
        var found = new List<(Vector2, uint, Vector2)>();
        ImDrawListPtr list = default;
        TestFrame.Run(() =>
        {
            list = Draw.Background;
            draw();
        });
        for (var i = 0; i < list.VtxBuffer.Size; i++) found.Add((list.VtxBuffer[i].Pos, list.VtxBuffer[i].Col, list.VtxBuffer[i].Uv));
        return found;
    }

    static List<(Vector2 Pos, uint Col, Vector2 Uv)> Telegraphed(Camera? camera, Cast cast, Settings settings, Vector2 player, bool? inside = null)
    {
        bool? result = null;
        var vertices = Drawn(camera, () =>
        {
            Groups.Declare(settings);
            result = Telegraph.Draw(CasterWith(cast), settings, player);
        });
        if (inside is not null) Check.Equal(inside, result);
        return vertices;
    }

    static float Extent(IEnumerable<Vector2> points) => points.Max(p => p.X) - points.Min(p => p.X);

    static Vector2[] With(List<(Vector2 Pos, uint Col, Vector2 Uv)> vertices, uint colour) => [.. vertices.Where(v => v.Col == colour).Select(v => v.Pos)];

    [Test]
    public void ActiveOnlyInTheWindup()
    {
        Check.That(Telegraph.Active(CastAt(0.5f, 1, 0)), "state 0");
        Check.That(Telegraph.Active(CastAt(0.5f, 1, 1)), "state 1");
        Check.That(!Telegraph.Active(CastAt(0.5f, 1, 2)), "state 2");
        Check.That(!Telegraph.Active(CastAt(0.5f, 0, 1)), "no time left");
    }

    [Test]
    public void AreaIsACircleAtTheTarget()
    {
        Span<Vector2> area = stackalloc Vector2[96];
        var count = Telegraph.Area(CastAt(0.5f), 250, area);
        Check.Equal(48, count);
        for (var i = 0; i < count; i++) Check.That(MathF.Abs(Vector2.Distance(area[i], Target) - 250) < 0.01f, "vertex off the circle");
    }

    [Test]
    public void InsideUsesTheAreaEdgeInclusively()
    {
        var settings = Configured();
        Span<Vector2> area = stackalloc Vector2[96];
        Telegraph.Area(CastAt(0.5f), 250, area);
        Check.That(Telegraph.Inside(CastAt(0.5f), settings, Target), "centre");
        Check.That(Telegraph.Inside(CastAt(0.5f), settings, area[0]), "on the edge");
        Check.That(!Telegraph.Inside(CastAt(0.5f), settings, Target + new Vector2(260, 0)), "outside");
    }

    [Test]
    public void FillGrowsFromTheCaster()
    {
        var settings = Configured();
        var fill = FillColour(FillBase, 0.5f);
        Check.Equal(0, With(Telegraphed(Forward, CastAt(0), settings, Far), fill).Length);
        var half = With(Telegraphed(Forward, CastAt(0.5f), settings, Far), fill);
        var full = With(Telegraphed(Forward, CastAt(1), settings, Far), fill);
        var ratio = Extent(half) / Extent(full);
        Check.That(ratio > 0.35f && ratio < 0.6f, $"half fill extent ratio {ratio}");
        Check.That(MathF.Abs(half.Min(p => p.X) - full.Min(p => p.X)) < 2 && half.Max(p => p.X) < full.Max(p => p.X) - 100, "fill does not start on the caster side");
    }

    [Test]
    public void DangerColourReplacesBothColoursWhenInside()
    {
        var settings = Configured();
        var outline = EColor.U32(Outline);
        var fill = FillColour(FillBase, 0.5f);
        var safe = Telegraphed(Forward, CastAt(1), settings, Far, inside: false);
        Check.That(safe.Any(v => v.Col == outline) && safe.Any(v => v.Col == fill), "normal colours when outside");
        Check.That(!safe.Any(v => v.Col == EColor.U32(Danger) || v.Col == FillColour(Danger, 0.5f)), "no danger when outside");
        var hit = Telegraphed(Forward, CastAt(1), settings, Target, inside: true);
        Check.That(!hit.Any(v => v.Col == outline || v.Col == fill), "normal colours leaked while inside");
        Check.That(hit.Any(v => v.Col == EColor.U32(Danger)) && hit.Any(v => v.Col == FillColour(Danger, 0.5f)), "danger outline and fill");
    }

    [Test]
    public void ProgressFillIsGroupedOutlineIsNot()
    {
        var settings = Configured();
        var fill = FillColour(FillBase, 0.5f);
        var second = CasterWith(CastAt(0.5f) with { Target = Target + new Vector2(100, 0) });
        var vertices = Drawn(Forward, () =>
        {
            Groups.Declare(settings);
            Telegraph.Draw(CasterWith(CastAt(0.5f)), settings, Far);
            Telegraph.Draw(second, settings, Far);
        });
        Check.That(vertices.Any(v => v.Col == fill), "fill missing");
        Check.That(vertices.All(v => v.Col >> 24 <= fill >> 24 || v.Col == EColor.U32(Outline)), "fill alpha stacked");
        Check.That(vertices.Any(v => v.Col == EColor.U32(Outline)), "dashed outline missing");
    }

    [Test]
    public void OutlineIsDashed()
    {
        var settings = Configured();
        settings.PlaceholderRadius = 600;
        Span<Vector2> area = stackalloc Vector2[96];
        Telegraph.Area(CastAt(0), 600, area);
        var dashEnd = Vector2.Lerp(area[0], area[1], 30 / Vector2.Distance(area[0], area[1]));
        var projected = Forward.ToScreen(new Vector3(dashEnd, 0), Viewport)!.Value;
        var outline = With(Telegraphed(Forward, CastAt(0), settings, Far), EColor.U32(Outline));
        Check.That(outline.Length > 0, "outline drawn");
        Check.That(outline.Any(p => Vector2.Distance(p, projected) < 4), "no dash end where the first dash should stop");
    }

    [Test]
    public void CountdownTextShowsSecondsLeft()
    {
        var settings = Configured();
        settings.ShowCountdown = true;
        settings.CountdownSize = 100;
        var white = EColor.U32(Color.White);
        var drawn = Telegraphed(Forward, CastAt(0, 1.26f), settings, Far).Where(v => v.Col == white).Select(v => (v.Pos, v.Uv)).ToArray();
        var expected = Drawn(Forward, () => Ground.Text("1.3", Target, 100, 90, Color.White)).Select(v => (v.Pos, v.Uv)).ToArray();
        var wrong = Drawn(Forward, () => Ground.Text("1.2", Target, 100, 90, Color.White)).Select(v => (v.Pos, v.Uv)).ToArray();
        Check.That(drawn.Length > 0 && drawn.SequenceEqual(expected), "text is not 1.3");
        Check.That(!drawn.SequenceEqual(wrong), "text matches 1.2");
    }

    [Test]
    public void CountdownReadsLeftToRightForAnyCameraYaw()
    {
        var settings = Configured();
        settings.ShowCountdown = true;
        settings.CountdownSize = 100;
        var white = EColor.U32(Color.White);
        foreach (var yaw in new float[] { 0, 30, 90, 180, -120 })
        {
            var forward = new Vector2(MathF.Cos(yaw * MathF.PI / 180), MathF.Sin(yaw * MathF.PI / 180));
            var camera = new Camera(new Vector3(Target - forward * 1000, 1000), new Vector3(-45, yaw, 0), 90);
            var digits = With(Telegraphed(camera, CastAt(0, 1.26f), settings, Far), white);
            Check.That(digits.Length >= 12, $"yaw {yaw}: text missing");
            var first = digits.Take(4).Average(p => p.X);
            var last = digits.TakeLast(4).Average(p => p.X);
            Check.That(first < last, $"yaw {yaw}: first glyph at {first} is not left of last glyph at {last}");
        }
    }

    [Test]
    public void NoCameraDrawsNothingAndDoesNotThrow()
    {
        var settings = Configured();
        settings.ShowCountdown = true;
        Check.Equal(0, Telegraphed(null, CastAt(0.5f), settings, Target).Count);
    }

    [Test]
    public void InactiveCastDrawsNothing()
    {
        var settings = Configured();
        Check.Equal(0, Telegraphed(Forward, CastAt(0.5f, 1, 2), settings, Target, inside: false).Count);
        Check.Equal(0, Telegraphed(Forward, CastAt(0.5f, 0), settings, Target, inside: false).Count);
        TestGame.Set(new GameState { Camera = Forward });
        Check.Equal(0, TestFrame.Vertices(() => Check.That(!Telegraph.Draw(CasterWith(null), settings, Target), "no cast")));
    }

    [Test]
    public void ShortCastsAreHiddenOnlyWhenTheOptionIsOnAndTheTotalCastTimeIsBelowTheMinimum()
    {
        var settings = Configured();
        settings.MinCastSeconds = 1;
        Check.That(Telegraphed(Forward, CastAt(0, 0.5f), settings, Target).Count > 0, "hidden while the option is off");
        settings.HideShortCasts = true;
        Check.Equal(0, Telegraphed(Forward, CastAt(0, 0.5f), settings, Target, inside: false).Count);
        Check.That(Telegraphed(Forward, CastAt(0, 1.5f), settings, Target).Count > 0, "long cast hidden");
        Check.That(Telegraphed(Forward, CastAt(0, 1), settings, Target).Count > 0, "a cast exactly at the minimum is hidden");
        Check.That(Telegraphed(Forward, CastAt(0.5f, 0.4f), settings, Target).Count == 0, "total 0.8s from progress still drawn");
    }

    [Test]
    public void TheTotalCastTimeComesFromProgressSoALongCastIsNotHiddenLateInTheWindup()
    {
        var settings = Configured();
        settings.HideShortCasts = true;
        settings.MinCastSeconds = 1;
        Check.That(!settings.TooShort(CastAt(0.9f, 0.2f)), "total 2s, 0.2s left");
        Check.That(settings.TooShort(CastAt(0.5f, 0.4f)), "total 0.8s");
        Check.That(!settings.TooShort(CastAt(0.5f, 0.6f)), "total 1.2s");
    }

    [Test]
    public void ZeroDurationIsNeverHiddenAndBadValuesNeverHide()
    {
        var settings = Configured();
        settings.HideShortCasts = true;
        settings.MinCastSeconds = 1;
        Check.That(!settings.TooShort(CastAt(0, 0)), "zero duration");
        Check.That(!settings.TooShort(CastAt(float.NaN, float.NaN)), "nan");
        settings.MinCastSeconds = float.NaN;
        Check.That(!settings.TooShort(CastAt(0, 0.5f)), "nan minimum");
    }

    [Test]
    public void BadFillOpacityDoesNotThrow()
    {
        foreach (var opacity in new[] { float.NaN, float.PositiveInfinity, -2f, 7f })
        {
            var settings = Configured();
            settings.FillOpacity = opacity;
            Telegraphed(Forward, CastAt(1), settings, Far);
        }
    }

    [Test]
    public void BadProgressDrawsNoFillAndClampsAboveOne()
    {
        var settings = Configured();
        var fill = FillColour(FillBase, 0.5f);
        Check.Equal(0, With(Telegraphed(Forward, CastAt(float.NaN), settings, Far), fill).Length);
        Check.Equal(0, With(Telegraphed(Forward, CastAt(-1), settings, Far), fill).Length);
        Check.Equal(Extent(With(Telegraphed(Forward, CastAt(1), settings, Far), fill)), Extent(With(Telegraphed(Forward, CastAt(9), settings, Far), fill)));
    }

    [Test]
    public void AreaWithATooSmallBufferReturnsZero()
    {
        Span<Vector2> small = stackalloc Vector2[10];
        Check.Equal(0, Telegraph.Area(CastAt(0.5f), 250, small));
    }
}
