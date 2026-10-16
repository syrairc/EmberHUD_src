using System;
using System.Drawing;
using System.Numerics;
using Hexa.NET.ImGui;
using EmberHUD;

namespace NavigationPlugin;

public sealed class Main : Plugin<Settings>
{
    const int TrailDots = 3;

    internal Paths Paths = new(new Random());
    internal Func<double> Seconds = () => Environment.TickCount64 / 1000.0;
    readonly Vector2[] _particles = new Vector2[256];

    public override void Tick() => Paths.Update(Game.State, Environment.TickCount64);

    public override void Render()
    {
        var state = Game.State;
        var general = Settings.General;
        if (!general.Show || state.Ui.AnyFullScreen || Paths.Current.Count == 0) return;
        var seconds = Seconds();
        var height = state.Player?.Position.Z ?? 0;
        foreach (var path in Paths.Current)
        {
            if (path.Points is not { Length: > 1 } points) continue;
            if (general.Ground && state.Camera is not null) DrawGround(path, points, seconds);
            if (general.Minimap && Map.View is { } map) DrawMinimap(path, points, seconds, map, height);
        }
    }

    void DrawGround(PathState path, Vector2[] points, double seconds)
    {
        var style = Settings.GroundStyle;
        if (style.BaseLine > 0) Ground.Polyline(points, Faded(path.Colour, style.Opacity * 0.4f), style.BaseLine);
        Dots(path, points, style, seconds, (at, scale, alpha) => Ground.Dot(at, style.Size * scale, Faded(path.Colour, style.Opacity * alpha)));
    }

    void DrawMinimap(PathState path, Vector2[] points, double seconds, MapView map, float height)
    {
        var style = Settings.MinimapStyle;
        var viewport = ImGui.GetIO().DisplaySize;
        using var layer = Draw.Layer(DrawLayer.Middle);
        if (style.BaseLine > 0)
        {
            var screen = new Vector2[points.Length];
            for (var i = 0; i < points.Length; i++) screen[i] = map.ToScreen(new Vector3(points[i], height));
            Draw.Polyline(screen, Faded(path.Colour, style.Opacity * 0.4f), style.BaseLine);
        }
        Dots(path, points, style, seconds, (world, scale, alpha) =>
        {
            var at = map.ToScreen(new Vector3(world, height));
            var radius = style.Size * scale;
            if (at.X < -radius || at.Y < -radius || at.X > viewport.X + radius || at.Y > viewport.Y + radius) return;
            Draw.CircleFilled(at, radius, Faded(path.Colour, style.Opacity * alpha), 12);
        });
    }

    void Dots(PathState path, Vector2[] points, SurfaceStyle style, double seconds, Action<Vector2, float, float> dot)
    {
        var count = Particles.Positions(points, path.Length, style.Spacing, style.Speed, seconds, _particles);
        for (var i = 0; i < count; i++)
        {
            dot(_particles[i], 1, 1);
            if (style.Trail <= 0) continue;
            var head = Particles.Wrap(i * style.Spacing + style.Speed * seconds, path.Length);
            for (var step = 1; step <= TrailDots; step++)
            {
                var behind = head - style.Trail * step / TrailDots;
                if (behind < 0) break;
                var fade = 1 - (float)step / (TrailDots + 1);
                dot(Particles.PointAt(points, behind), fade, fade * 0.6f);
            }
        }
    }

    static Color Faded(Color colour, float opacity) => Color.FromArgb((int)Math.Clamp(colour.A * opacity, 0, 255), colour);
}
