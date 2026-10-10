using System;
using System.Numerics;
using Hexa.NET.ImGui;
using TorchlightSDK;

namespace Minimap;

public sealed class Main : Plugin<Settings>
{
    internal BackgroundJob<Walls> WallJob = new();
    Walls _walls = Walls.Empty;

    public override void Tick()
    {
        var view = Settings.View;
        if (view.ToggleMap.Pressed) view.Show.Value = !view.Show.Value;
        if (view.ZoomIn.Pressed) view.Zoom.Value = MathF.Min(view.Zoom.Value * 1.25f, view.Zoom.Max);
        if (view.ZoomOut.Pressed) view.Zoom.Value = MathF.Max(view.Zoom.Value / 1.25f, view.Zoom.Min);

        var state = Game.State;
        if (WallJob.TakeFinished() is { } done) _walls = done.GetAwaiter().GetResult();
        if (_walls.AreaId != state.AreaId) _walls = Walls.Empty;
        if (!WallJob.Running && !_walls.Matches(state.AreaId, state.Terrain))
        {
            var area = state.AreaId;
            var terrain = state.Terrain;
            WallJob.Start(() => Walls.Build(area, terrain));
        }

        if (view.Show.Value) Map.View = ViewFor(Settings, state.Player?.Position, _walls, ImGui.GetIO().DisplaySize);
    }

    public static MapView? ViewFor(Settings settings, Vector3? player, Walls walls, Vector2 viewport)
    {
        var view = settings.View;
        var map = view.FollowPlayer.Value && player is { } at && float.IsFinite(at.X) && float.IsFinite(at.Y)
            ? MapView.Follow(Map.Project(at), viewport, view.Zoom.Value, view.Rotation.Value, view.VerticalScale.Value)
            : MapView.Fit(walls.Min, walls.Max, viewport, 40, view.Rotation.Value, view.VerticalScale.Value);
        return map.Scale > 0 ? map with { Offset = map.Offset + new Vector2(view.OffsetX.Value, view.OffsetY.Value) } : null;
    }

    public override void Render()
    {
        if (!Settings.View.Show.Value || Map.View is not { } map) return;
        var viewport = ImGui.GetIO().DisplaySize;
        var state = Game.State;

        var wallColour = Settings.View.WallColour.Value;
        var wallThickness = Settings.View.WallThickness.Value;
        using (Draw.Layer(DrawLayer.Back))
        {
            for (var i = 0; i + 1 < _walls.Lines.Length; i += 2)
            {
                var a = map.ToScreen(_walls.Lines[i]);
                var b = map.ToScreen(_walls.Lines[i + 1]);
                if (Visible(a, b, viewport)) Draw.Line(a, b, wallColour, wallThickness);
            }
        }

        foreach (var entity in state.Entities)
        {
            if (Settings.MarkerFor(entity) is not { } style) continue;
            var at = map.ToScreen(entity.Position);
            if (!Visible(at, at, viewport)) continue;
            Draw.Marker(at, style.Radius.Value, style.Colour.Value, Settings.ShapeFor(entity.Kind));
        }

        if (state.Player is { } player) DrawPlayer(map.ToScreen(player.Position), map.ToScreenDirection(Map.Project(new Vector3(player.Forward, 0))), viewport);
    }

    void DrawPlayer(Vector2 at, Vector2 forward, Vector2 viewport)
    {
        if (!Visible(at, at, viewport)) return;
        var size = Settings.View.PlayerSize.Value;
        var colour = Settings.View.PlayerColour.Value;
        if (ArrowPoints(at, forward, size) is { } arrow) Draw.TriangleFilled(arrow.Tip, arrow.Left, arrow.Right, colour);
        else Draw.Marker(at, size / 2, colour);
    }

    internal static bool Visible(Vector2 a, Vector2 b, Vector2 viewport) =>
        float.IsFinite(a.X) && float.IsFinite(a.Y) && float.IsFinite(b.X) && float.IsFinite(b.Y)
        && MathF.Max(a.X, b.X) >= 0 && MathF.Min(a.X, b.X) <= viewport.X
        && MathF.Max(a.Y, b.Y) >= 0 && MathF.Min(a.Y, b.Y) <= viewport.Y;

    internal static (Vector2 Tip, Vector2 Left, Vector2 Right)? ArrowPoints(Vector2 at, Vector2 forward, float size)
    {
        if (!(float.IsFinite(forward.X) && float.IsFinite(forward.Y) && forward.LengthSquared() > 1e-6f)) return null;
        var direction = Vector2.Normalize(forward);
        var side = new Vector2(-direction.Y, direction.X);
        var back = at - direction * size * 0.6f;
        return (at + direction * size, back + side * size * 0.6f, back - side * size * 0.6f);
    }
}
