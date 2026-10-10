using System;
using System.Drawing;
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

        if (view.Show.Value) Map.View = ViewFor(Settings, state.Player?.Position, _walls, ImGui.GetIO().DisplaySize, state.MinimapRotation);
    }

    public static MapView? ViewFor(Settings settings, Vector3? player, Walls walls, Vector2 viewport, float? gameRotation = null)
    {
        var view = settings.View;
        var rotation = (gameRotation ?? 0) + view.Rotation.Value;
        var map = view.FollowPlayer.Value && player is { } at && float.IsFinite(at.X) && float.IsFinite(at.Y)
            ? MapView.Follow(Map.Project(at), viewport, view.Zoom.Value, rotation, view.VerticalScale.Value)
            : MapView.Fit(walls.Min, walls.Max, viewport, 40, rotation, view.VerticalScale.Value);
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
            if (HasIcon(state.MapIcons, entity.Id)) continue;
            if (Settings.MarkerFor(entity) is not { } style) continue;
            var at = map.ToScreen(entity.Position);
            if (!Visible(at, at, viewport)) continue;
            Draw.Icon(Settings.IconFor(entity), at, style.Radius.Value, style.Colour.Value, null, accent: style.Accent.Value);
        }

        foreach (var icon in state.MapIcons) DrawIcon(icon, map, viewport);

        if (state.Player is { } player) DrawPlayer(map.ToScreen(player.Position), map.ToScreenDirection(Map.Project(new Vector3(player.Forward, 0))), viewport);
    }

    internal static bool HasIcon(MapIcon[] icons, int entityId)
    {
        if (entityId == 0) return false;
        foreach (var icon in icons)
            if (icon.EntityId == entityId && float.IsFinite(icon.Position.X) && float.IsFinite(icon.Position.Y)) return true;
        return false;
    }

    void DrawIcon(MapIcon icon, MapView map, Vector2 viewport)
    {
        var at = map.ToScreen(new Vector3(icon.Position, 0));
        if (!Visible(at, at, viewport)) return;
        var view = Settings.View;
        var size = view.IconSize.Value * (float.IsFinite(icon.Size) && icon.Size > 0 ? icon.Size : 1);
        if (view.UseGameIcons.Value && !string.IsNullOrEmpty(icon.Texture) && Draw.TryGameIcon(icon.Texture, out var sprite))
            Draw.Image(sprite, at - new Vector2(size / 2), at + new Vector2(size / 2), view.IconTint.Value);
        else
            Draw.Marker(at, size / 4, Color.FromArgb(0xF9, 0xDD, 0x75), Settings.FallbackShape(icon.Type));
    }

    void DrawPlayer(Vector2 at, Vector2 forward, Vector2 viewport)
    {
        if (!Visible(at, at, viewport)) return;
        var view = Settings.View;
        var size = view.PlayerSize.Value;
        var colour = view.PlayerColour.Value;
        if (ArrowPoints(at, forward, size) is not { } arrow)
        {
            Draw.Marker(at, size / 2, colour);
            return;
        }

        if (view.UseGameIcons.Value && Draw.TryGameIcon("UI_MiniMap_Player1", out var sprite)) Draw.ImageRotated(sprite, at, new Vector2(view.IconSize.Value), new Vector2(forward.Y, -forward.X), view.IconTint.Value);
        else Draw.TriangleFilled(arrow.Tip, arrow.Left, arrow.Right, colour);
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
