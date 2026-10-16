using System;
using System.Drawing;
using System.Numerics;
using Hexa.NET.ImGui;
using EmberHUD;
using EmberHUD.Ui;

namespace NavigationPlugin;

public sealed class Main : Plugin<Settings>
{
    const int TrailDots = 3;
    const float Gap = 4;

    readonly Overlay _panel = new();

    internal Paths Paths = new(new Random());
    internal Func<double> Seconds = () => Environment.TickCount64 / 1000.0;
    readonly Vector2[] _particles = new Vector2[256];

    public override void Initialize() => RegisterHotkey("Toggle paths", "Shows or hides the quest paths, legend and labels", () => Settings.General.Show = !Settings.General.Show);

    public override void Tick() => Paths.Update(Game.State, Environment.TickCount64);

    internal static bool LegendHidden(Settings settings, GameState state) =>
        !settings.Legend.Show || settings.Legend.HideBehindPanels && state.Ui.AnyFullScreen;

    internal static Vector2? LabelAnchor(PathState path, Vector2 player, float alongDistance, float sideOffset)
    {
        if (path.Points is not { Length: > 1 } points) return null;
        var segment = 0;
        var fraction = 0f;
        var nearest = float.MaxValue;
        for (var i = 0; i < points.Length - 1; i++)
        {
            var run = points[i + 1] - points[i];
            var t = run == Vector2.Zero ? 0 : Math.Clamp(Vector2.Dot(player - points[i], run) / run.LengthSquared(), 0, 1);
            var gap = Vector2.DistanceSquared(player, points[i] + run * t);
            if (gap >= nearest) continue;
            (nearest, segment, fraction) = (gap, i, t);
        }

        var at = points[segment] + (points[segment + 1] - points[segment]) * fraction;
        var remaining = alongDistance;
        for (var i = segment; i < points.Length - 1; i++)
        {
            var run = points[i + 1] - at;
            var length = run.Length();
            if (length > 0 && remaining <= length) return at + run / length * remaining + Sideways(run / length) * sideOffset;
            remaining -= length;
            at = points[i + 1];
        }

        var last = points[^1] - points[^2];
        return at + Sideways(last == Vector2.Zero ? Vector2.UnitX : Vector2.Normalize(last)) * sideOffset;
    }

    static Vector2 Sideways(Vector2 direction) => new(-direction.Y, direction.X);

    public override void Render()
    {
        var state = Game.State;
        var general = Settings.General;
        if (!general.Show || Paths.Current.Count == 0) return;
        if (!state.Ui.AnyFullScreen) DrawPaths(state);
        if (!LegendHidden(Settings, state)) DrawLegend();
    }

    void DrawPaths(GameState state)
    {
        var general = Settings.General;
        var seconds = Seconds();
        var height = state.Player?.Position.Z ?? 0;
        foreach (var path in Paths.Current)
        {
            if (path.Points is not { Length: > 1 } points) continue;
            if (general.Ground && state.Camera is not null) DrawGround(path, points, seconds);
            if (general.Minimap && Map.View is { } map) DrawMinimap(path, points, seconds, map, height);
            if (Settings.Labels.Show && state.Camera is { } camera && state.Player is { } player) DrawLabel(path, camera, player.Position);
        }
    }

    void DrawLabel(PathState path, Camera camera, Vector3 player)
    {
        var labels = Settings.Labels;
        if (LabelAnchor(path, new Vector2(player.X, player.Y), labels.AlongDistance, labels.SideOffset) is not { } anchor) return;
        if (camera.ToScreen(new Vector3(anchor, player.Z), ImGui.GetIO().DisplaySize) is not { } screen) return;
        var style = labels.Label.Clone();
        style.Color = path.Colour;
        var text = labels.ShowDistance ? $"{path.Name}  {Legend.Distance(path)}" : path.Name;
        Draw.Text(text, screen, style, Align.Center | Align.Middle);
    }

    void DrawLegend()
    {
        var legend = Settings.Legend;
        var rows = Legend.Rows(Paths.Current);
        var rowHeight = Draw.MeasureText("Ag", legend.Text).Y + Gap;
        var swatch = rowHeight - Gap;

        _panel.Locked = legend.Lock;
        _panel.MoveHeld = legend.MoveKey.Key != Key.None && Input.IsDown(legend.MoveKey.Key);
        var origin = new Vector2(legend.X, legend.Y);
        var size = new Vector2(legend.Width, rowHeight * rows.Count);
        var (hovered, edge, active) = _panel.Handle("navigation", ref origin, size, ref legend.X, ref legend.Y, ref legend.Width, LegendTab.MinWidth, LegendTab.MaxWidth);
        if (hovered || active) Overlay.ClickBlocker("navigation", origin, size);

        var at = origin;
        foreach (var row in rows)
        {
            var fade = row.Dimmed ? 0.45f : 1;
            var text = legend.Text.Clone();
            text.Color = Faded(text.Color, fade);
            Draw.RectFilled(at, at + new Vector2(swatch, swatch), Faded(row.Colour, fade));
            Draw.Text(row.Name, at + new Vector2(swatch + Gap, 0), text);
            Draw.Text(row.Distance, new Vector2(at.X + size.X, at.Y), text, Align.Right);
            at.Y += rowHeight;
        }
        if (_panel.Movable) Overlay.DragHint(origin, origin + size, active, hovered, edge);
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
