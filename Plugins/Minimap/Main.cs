using System;
using System.Drawing;
using System.Numerics;
using Hexa.NET.ImGui;
using Aember;

namespace Minimap;

public sealed class Main : Plugin<Settings>
{
    internal BackgroundJob<Walls> WallJob = new();
    internal BackgroundJob<Region> RegionJob = new();
    internal Func<long> Clock = () => Environment.TickCount64;
    Walls _walls = Walls.Empty;
    Region _region = Region.Empty;
    string _regionKey = "";
    long _regionAt;
    Vector2[] _screen = [];

    bool WallsStyle => Settings.View.DrawStyle.Value == "Walls";

    public override void Initialize()
    {
        RegisterHotkey("Toggle map", "Shows or hides the minimap", () => Settings.View.Show.Value = !Settings.View.Show.Value, new Hotkey(Key.F10));
        RegisterHotkey("Zoom in", "Zooms the map in", () => SetZoom(Settings.View.Zoom.Value * 1.25f), new Hotkey(Key.PageUp));
        RegisterHotkey("Zoom out", "Zooms the map out", () => SetZoom(Settings.View.Zoom.Value / 1.25f), new Hotkey(Key.PageDown));
        RegisterHotkey("Re-check rotation", "Drops the cached game minimap widget and finds it again, use when the map stops following the game's rotation", Game.RescanMinimap, new Hotkey(Key.F9));
    }

    void SetZoom(float value) => Settings.View.Zoom.Value = Math.Clamp(value, Settings.View.Zoom.Min, Settings.View.Zoom.Max);

    public override void Tick()
    {
        var view = Settings.View;
        var state = Game.State;
        if (WallJob.TakeFinished() is { } done) _walls = done.GetAwaiter().GetResult();
        if (_walls.AreaId != state.AreaId) _walls = Walls.Empty;
        if (!WallJob.Running && !_walls.Matches(state.AreaId, state.Terrain))
        {
            var area = state.AreaId;
            var terrain = state.Terrain;
            WallJob.Start(() => Walls.Build(area, terrain));
        }
        if (view.Show.Value && !WallsStyle) UpdateRegion(state);

        if (view.Show.Value) Map.View = ViewFor(Settings, state.Player?.Position, _walls, ImGui.GetIO().DisplaySize, state.MinimapRotation);
    }

    void UpdateRegion(GameState state)
    {
        if (RegionJob.TakeFinished() is { } done)
        {
            var built = done.GetAwaiter().GetResult();
            if (built.Matches(state.AreaId, state.Terrain)) Show(built);
        }
        if (RegionJob.Running || state.Terrain is not { } terrain) return;

        var view = Settings.View;
        var player = state.Player is { } at ? new Vector2(at.Position.X, at.Position.Y) : (Vector2?)null;
        var now = Clock();
        if (_region.Matches(state.AreaId, terrain) && (now - _regionAt < 250 || !_region.Stale(state.Explored, Region.CellOf(terrain, player), view.RegionFill.Value, view.ExploredFill.Value))) return;

        _regionAt = now;
        var area = state.AreaId;
        var trail = state.Explored;
        var regionFill = view.RegionFill.Value;
        var exploredFill = view.ExploredFill.Value;
        var previous = _region.Matches(area, terrain) ? _region : null;
        RegionJob.Start(() => Region.Build(area, terrain, trail, player, regionFill, exploredFill, previous));
    }

    void Show(Region built)
    {
        var key = $"region-{built.AreaId}";
        if (_regionKey.Length > 0 && _regionKey != key) Textures.Release(_regionKey);
        if (built.Pixels.Length > 0) Textures.Upload(key, built.Pixels, built.Width, built.Height);
        _regionKey = key;
        _region = built;
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

        using (Draw.Layer(DrawLayer.Back))
        {
            if (WallsStyle) DrawWalls(map, viewport);
            else DrawRegion(map, state);
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

    void DrawWalls(MapView map, Vector2 viewport)
    {
        var colour = Settings.View.WallColour.Value;
        var thickness = Settings.View.WallThickness.Value;
        for (var i = 0; i + 1 < _walls.Lines.Length; i += 2)
        {
            var a = map.ToScreen(_walls.Lines[i]);
            var b = map.ToScreen(_walls.Lines[i + 1]);
            if (Visible(a, b, viewport)) Draw.Line(a, b, colour, thickness);
        }
    }

    void DrawRegion(MapView map, GameState state)
    {
        var view = Settings.View;
        var current = _region.Matches(state.AreaId, state.Terrain);
        if (current && _region.Corners.Length == 4 && Textures.TryGet(_regionKey, out var fill))
            Draw.ImageQuad(fill, map.ToScreen(_region.Corners[0]), map.ToScreen(_region.Corners[1]), map.ToScreen(_region.Corners[2]), map.ToScreen(_region.Corners[3]));
        DrawOutline(_walls.RegionOutline, map, view.RegionOutline.Value, view.RegionOutlineWidth.Value);
        if (current) DrawOutline(_region.ExploredOutline, map, view.ExploredOutline.Value, view.ExploredOutlineWidth.Value);
    }

    void DrawOutline(Polyline[] lines, MapView map, Color colour, float width)
    {
        foreach (var line in lines)
        {
            if (_screen.Length < line.Points.Length) _screen = new Vector2[line.Points.Length];
            for (var i = 0; i < line.Points.Length; i++) _screen[i] = map.ToScreen(line.Points[i]);
            Draw.Polyline(_screen.AsSpan(0, line.Points.Length), colour, width, line.Closed);
        }
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
