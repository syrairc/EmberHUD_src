using System;
using System.Collections.Generic;
using System.Drawing;
using System.Globalization;
using System.Linq;
using System.Numerics;
using System.Text.Json.Nodes;
using System.Threading;
using EmberHUD;

namespace Mcp;

internal sealed record Mark(int Id, string Shape, int? Entity, Vector3? World, Vector2? Screen, float Size, Color Color, string? Label, long? Until, Vector2? Extent = null, Vector2[]? Points = null, bool Fill = false, float Thickness = 2, bool TopLeft = false);

internal sealed class Marks
{
    public const int Max = 100;
    readonly Lock _gate = new();
    readonly List<Mark> _marks = [];
    Dictionary<int, Vector3> _scene = [];
    long _sceneAt = long.MinValue;
    int _next = 1;

    public Vector2 Viewport { get; set; }

    public int Count
    {
        get
        {
            lock (_gate) return _marks.Count;
        }
    }

    internal Mark[] All
    {
        get
        {
            lock (_gate) return [.. _marks];
        }
    }

    public (int Id, Vector2? Screen) Add(Mark draft, long now)
    {
        lock (_gate)
        {
            if (_marks.Count >= Max) throw new ToolError($"at most {Max} marks at once, clear some first");
            if (draft.Entity is { } id && FindInState(id, Game.State) is null)
            {
                Walk(now);
                if (!_scene.ContainsKey(id)) throw new ToolError($"no entity {id} in Game.State entities, projectiles or the scene");
            }
            var mark = draft with { Id = _next++ };
            _marks.Add(mark);
            return (mark.Id, Locate(mark, Game.State));
        }
    }

    public int Clear(int? id)
    {
        lock (_gate)
        {
            if (id is null)
            {
                var all = _marks.Count;
                _marks.Clear();
                return all;
            }
            return _marks.RemoveAll(mark => mark.Id == id) is var removed and > 0 ? removed : throw new ToolError($"no mark with id {id}");
        }
    }

    public void Tick(long now)
    {
        lock (_gate)
        {
            _marks.RemoveAll(mark => mark.Until <= now);
            var state = Game.State;
            if (now >= _sceneAt + 100 &&_marks.Any(mark => mark.Entity is { } id && FindInState(id, state) is null)) Walk(now);
        }
    }

    public void Render(Vector2 viewport)
    {
        lock (_gate)
        {
            Viewport = viewport;
            var state = Game.State;
            foreach (var mark in _marks)
            {
                if (Locate(mark, state) is not { } at || DrawShape(mark, state, at) is not { } bounds) continue;
                if (mark.Label is { } label) Draw.Text(label, new Vector2((bounds.Min.X + bounds.Max.X) / 2, mark.Shape == "text" ? at.Y : bounds.Max.Y + 2), mark.Color, Align.Center, list: Draw.Foreground);
            }
        }
    }

    (Vector2 Min, Vector2 Max)? DrawShape(Mark mark, GameState state, Vector2 at)
    {
        var list = Draw.Foreground;
        var half = mark.Size / 2;
        var fill = Color.FromArgb(60, mark.Color);
        switch (mark.Shape)
        {
            case "box" or "rect":
                var (min, max) = mark.Points is { Length: 2 } corners && Corners(mark, corners, state) is { } both ? both : Box(at, mark.Extent ?? new Vector2(mark.Size), mark.TopLeft);
                if (mark.Fill) Draw.RectFilled(min, max, fill, list: list);
                Draw.Rect(min, max, mark.Color, mark.Thickness, list: list);
                return (min, max);
            case "circle":
                if (mark.Fill) Draw.CircleFilled(at, half, fill, list: list);
                Draw.Circle(at, half, mark.Color, mark.Thickness, list: list);
                return (at - new Vector2(half), at + new Vector2(half));
            case "ellipse":
                var (low, high) = Box(at, mark.Extent!.Value, mark.TopLeft);
                var middle = (low + high) / 2;
                var radii = (high - low) / 2;
                var ring = new Vector2[48];
                for (var i = 0; i < ring.Length; i++) ring[i] = middle + radii * new Vector2(MathF.Cos(i * MathF.Tau / ring.Length), MathF.Sin(i * MathF.Tau / ring.Length));
                if (mark.Fill) Draw.ConvexPolyFilled(ring, fill, list: list);
                Draw.Polyline(ring, mark.Color, mark.Thickness, true, list);
                return (low, high);
            case "cross":
                Draw.Line(at - new Vector2(half, 0), at + new Vector2(half, 0), mark.Color, mark.Thickness, list);
                Draw.Line(at - new Vector2(0, half), at + new Vector2(0, half), mark.Color, mark.Thickness, list);
                return (at - new Vector2(half), at + new Vector2(half));
            case "line" or "polygon":
                var points = new Vector2[mark.Points!.Length];
                for (var i = 0; i < points.Length; i++)
                    if (Project(mark, mark.Points[i], state) is { } projected) points[i] = projected;
                    else return null;
                var closed = mark.Shape == "polygon";
                if (closed && mark.Fill) Draw.ConvexPolyFilled(points, fill, list: list);
                Draw.Polyline(points, mark.Color, mark.Thickness, closed, list);
                return (new Vector2(points.Min(p => p.X), points.Min(p => p.Y)), new Vector2(points.Max(p => p.X), points.Max(p => p.Y)));
            default:
                return (at, at);
        }
    }

    (Vector2, Vector2)? Corners(Mark mark, Vector2[] corners, GameState state) =>
        Project(mark, corners[0], state) is { } a && Project(mark, corners[1], state) is { } b ? (Vector2.Min(a, b), Vector2.Max(a, b)) : null;

    internal static (Vector2 Min, Vector2 Max) Box(Vector2 at, Vector2 extent, bool topLeft) =>
        topLeft ? (at, at + extent) : (at - extent / 2, at + extent / 2);

    internal Vector2? Locate(Mark mark, GameState state)
    {
        if (mark.Points is { Length: > 0 } points) return Project(mark, points[0], state);
        if (mark.Screen is { } pixels) return pixels;
        if (Viewport == Vector2.Zero) return null;
        return (mark.World ?? Find(mark.Entity!.Value, state)) is { } world ? state.Camera?.ToScreen(world, Viewport) : null;
    }

    internal Vector2? Project(Mark mark, Vector2 point, GameState state)
    {
        if (mark.Screen is not null) return point;
        if (Viewport == Vector2.Zero) return null;
        return state.Camera?.ToScreen(new Vector3(point, mark.World?.Z ?? 0), Viewport);
    }

    void Walk(long now)
    {
        _sceneAt = now;
        var scene = new Dictionary<int, Vector3>();
        foreach (var entity in Scene.Entities())
            if (entity.Position is { } at) scene[entity.Id] = at;
        _scene = scene;
    }

    Vector3? Find(int id, GameState state) => FindInState(id, state) ?? (_scene.TryGetValue(id, out var at) ? at : null);

    static Vector3? FindInState(int id, GameState state)
    {
        foreach (var entity in state.Entities)
            if (entity.Id == id) return entity.Position;
        foreach (var projectile in state.Projectiles)
            if (projectile.Id == id) return new Vector3(projectile.Position, state.Player?.Position.Z ?? 0);
        return null;
    }
}

internal static class MarkTools
{
    public static Tool[] Create(Marks marks, Func<long> clock) =>
    [
        new("draw", "Draw a mark on the overlay so the user can see what you mean, e.g. a box around the entity you think is the projectile, then ask them. Anchor is entity (id from get_entities / scene_entities / get_state projectiles, the mark follows it and hides while it is gone), or x + y in world units (z defaults to the player's height, the mark stays on that spot), or x + y with screen true (pixels). shape box (default, a square of size), circle, text (needs a label), cross (size), rect (width + height around x,y, or pivot topleft to hang them off x,y, or corner to corner with x2 + y2), ellipse (width + height), line (x,y to x2,y2) or polygon (points [[x,y],...] in the same space as x,y, at least 3, closed, fill is only right for convex ones). Lines, polygons and corner rects take no entity. size in pixels, default 40. thickness of the outline 1 to 20, default 2. fill tints the inside. color is a name like red / cyan or #RRGGBB, default magenta. seconds makes it expire, default it stays until draw_clear. An area change clears every mark. At most 100. Returns the id and where it is on screen right now (null when off screen or no camera).",
            Schema.Of([], ("entity", "integer", "entity id to follow"), ("x", "number", "world x, or pixels with screen"), ("y", "number", "world y, or pixels with screen"), ("z", "number", "world z, default the player's"), ("screen", "boolean", "x and y are screen pixels"), ("shape", "string", "box, circle, text, cross, rect, ellipse, line or polygon"), ("size", "number", "pixels, 4 to 2000"), ("width", "number", "rect and ellipse width in pixels"), ("height", "number", "rect and ellipse height in pixels"), ("pivot", "string", "center (default) or topleft, where x,y sits on a rect or ellipse"), ("x2", "number", "second x for line, or the opposite corner of a rect"), ("y2", "number", "second y, same space as x2"), ("points", "array", "polygon points [[x,y],...], 3 to 256"), ("thickness", "number", "outline pixels, 1 to 20"), ("fill", "boolean", "tint the inside"), ("color", "string", "name or #RRGGBB"), ("label", "string", "text next to the mark"), ("seconds", "number", "expire after this long")),
            args => Add(args, marks, clock()), ReadOnly: false, Idempotent: false),
        new("draw_clear", "Remove one mark by id, or every mark when no id is given. Returns how many went.",
            Schema.Of([], ("id", "integer", "mark id from draw")), args => new JsonObject { ["cleared"] = marks.Clear(args.OptInt("id")) }, ReadOnly: false, Idempotent: false),
    ];

    static JsonObject Add(Args args, Marks marks, long now)
    {
        var entity = args.OptInt("entity");
        var x = args.OptFloat("x");
        var y = args.OptFloat("y");
        var x2 = args.OptFloat("x2");
        var y2 = args.OptFloat("y2");
        var shape = args.OptStr("shape") ?? "box";
        if (shape is not ("box" or "circle" or "text" or "cross" or "rect" or "ellipse" or "line" or "polygon")) throw new ToolError("shape must be box, circle, text, cross, rect, ellipse, line or polygon");
        var label = args.OptStr("label");
        if (shape == "text" && string.IsNullOrEmpty(label)) throw new ToolError("shape text needs a label");
        var size = args.OptFloat("size") ?? 40;
        if (size is < 4 or > 2000) throw new ToolError("size must be between 4 and 2000");
        var thickness = args.OptFloat("thickness") ?? 2;
        if (thickness is < 1 or > 20) throw new ToolError("thickness must be between 1 and 20");
        var seconds = args.OptFloat("seconds");
        if (seconds is <= 0) throw new ToolError("seconds must be above 0");
        var color = Parse(args.OptStr("color") ?? "magenta");
        if (entity is not null && (shape is "line" or "polygon" || shape == "rect" && x2 is not null)) throw new ToolError($"{shape} with points or corners takes no entity");
        var (extent, points) = ShapeGeometry(args, shape, x, y, x2, y2);
        if (points is null)
        {
            if (entity is not null && (x is not null || y is not null)) throw new ToolError("give entity or x and y, not both");
            if (entity is null && (x is null || y is null)) throw new ToolError("give entity, or x and y");
        }
        var screenSpace = args.Bool("screen", false);
        var z = args.OptFloat("z") ?? Game.State.Player?.Position.Z ?? 0;
        var anchor = points?[0] ?? (x is { } ax && y is { } ay ? new Vector2(ax, ay) : (Vector2?)null);
        Vector3? world = null;
        Vector2? screen = null;
        if (entity is null && screenSpace) screen = anchor;
        else if (entity is null) world = new Vector3(anchor!.Value, z);
        var until = seconds is { } s ? now + (long)(s * 1000) : (long?)null;
        var pivotTopLeft = (args.OptStr("pivot") ?? "center") switch { "center" => false, "topleft" => true, _ => throw new ToolError("pivot must be center or topleft") };
        var (id, at) = marks.Add(new Mark(0, shape, entity, world, screen, size, color, label, until, extent, points, args.Bool("fill", false), thickness, pivotTopLeft), now);
        return new JsonObject { ["id"] = id, ["screen"] = at is { } p ? new JsonObject { ["x"] = Num.F(p.X), ["y"] = Num.F(p.Y) } : null };
    }

    static (Vector2? Extent, Vector2[]? Points) ShapeGeometry(Args args, string shape, float? x, float? y, float? x2, float? y2)
    {
        var width = args.OptFloat("width");
        var height = args.OptFloat("height");
        var polygon = args.OptPoints("points", 256);
        if (width is <= 0 or > 4000 || height is <= 0 or > 4000) throw new ToolError("width and height must be between 0 and 4000");
        Vector2? extent = width is { } w && height is { } h ? new Vector2(w, h) : null;
        var corners = x is { } ax && y is { } ay && x2 is { } bx && y2 is { } by ? new[] { new Vector2(ax, ay), new Vector2(bx, by) } : null;
        switch (shape)
        {
            case "rect" when corners is null && extent is null:
                throw new ToolError("rect needs width and height, or x2 and y2");
            case "rect":
                return (extent, corners);
            case "ellipse":
                return extent is null ? throw new ToolError("ellipse needs width and height") : (extent, null);
            case "line":
                return corners is null ? throw new ToolError("line needs x, y, x2 and y2") : (null, corners);
            case "polygon":
                return polygon is { Length: >= 3 } ? (null, polygon) : throw new ToolError("polygon needs at least 3 points");
            default:
                return (null, null);
        }
    }

    static Color Parse(string text)
    {
        if (text.StartsWith('#') && text.Length == 7 && int.TryParse(text[1..], NumberStyles.AllowHexSpecifier, CultureInfo.InvariantCulture, out var rgb)) return Color.FromArgb(255, Color.FromArgb(rgb));
        var named = Color.FromName(text);
        return named.IsKnownColor ? named : throw new ToolError("color must be a name like red or #RRGGBB");
    }
}
