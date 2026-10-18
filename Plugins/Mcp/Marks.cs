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

internal sealed record Mark(int Id, string Shape, int? Entity, Vector3? World, Vector2? Screen, float Size, Color Color, string? Label, long? Until);

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
                if (Locate(mark, state) is not { } at) continue;
                var half = mark.Size / 2;
                if (mark.Shape == "box") Draw.Rect(at - new Vector2(half), at + new Vector2(half), mark.Color, 2, list: Draw.Foreground);
                else if (mark.Shape == "circle") Draw.Circle(at, half, mark.Color, 2, list: Draw.Foreground);
                if (mark.Label is { } label) Draw.Text(label, at + new Vector2(0, mark.Shape == "text" ? 0 : half + 2), mark.Color, Align.Center, list: Draw.Foreground);
            }
        }
    }

    internal Vector2? Locate(Mark mark, GameState state)
    {
        if (mark.Screen is { } pixels) return pixels;
        if (Viewport == Vector2.Zero) return null;
        return (mark.World ?? Find(mark.Entity!.Value, state)) is { } world ? state.Camera?.ToScreen(world, Viewport) : null;
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
        new("draw", "Draw a mark on the overlay so the user can see what you mean, e.g. a box around the entity you think is the projectile, then ask them. Anchor is entity (id from get_entities / scene_entities / get_state projectiles, the mark follows it and hides while it is gone), or x + y in world units (z defaults to the player's height, the mark stays on that spot), or x + y with screen true (pixels). shape box (default), circle or text (text needs a label). size in pixels, default 40. color is a name like red / cyan or #RRGGBB, default magenta. seconds makes it expire, default it stays until draw_clear. An area change clears every mark. At most 100. Returns the id and where it is on screen right now (null when off screen or no camera).",
            Schema.Of([], ("entity", "integer", "entity id to follow"), ("x", "number", "world x, or pixels with screen"), ("y", "number", "world y, or pixels with screen"), ("z", "number", "world z, default the player's"), ("screen", "boolean", "x and y are screen pixels"), ("shape", "string", "box, circle or text"), ("size", "number", "pixels, 4 to 2000"), ("color", "string", "name or #RRGGBB"), ("label", "string", "text next to the mark"), ("seconds", "number", "expire after this long")),
            args => Add(args, marks, clock()), ReadOnly: false, Idempotent: false),
        new("draw_clear", "Remove one mark by id, or every mark when no id is given. Returns how many went.",
            Schema.Of([], ("id", "integer", "mark id from draw")), args => new JsonObject { ["cleared"] = marks.Clear(args.OptInt("id")) }, ReadOnly: false, Idempotent: false),
    ];

    static JsonObject Add(Args args, Marks marks, long now)
    {
        var entity = args.OptInt("entity");
        var x = args.OptFloat("x");
        var y = args.OptFloat("y");
        if (entity is not null && (x is not null || y is not null)) throw new ToolError("give entity or x and y, not both");
        if (entity is null && (x is null || y is null)) throw new ToolError("give entity, or x and y");
        var shape = args.OptStr("shape") ?? "box";
        if (shape is not ("box" or "circle" or "text")) throw new ToolError("shape must be box, circle or text");
        var label = args.OptStr("label");
        if (shape == "text" && string.IsNullOrEmpty(label)) throw new ToolError("shape text needs a label");
        var size = args.OptFloat("size") ?? 40;
        if (size is < 4 or > 2000) throw new ToolError("size must be between 4 and 2000");
        var seconds = args.OptFloat("seconds");
        if (seconds is <= 0) throw new ToolError("seconds must be above 0");
        var color = Parse(args.OptStr("color") ?? "magenta");
        Vector3? world = null;
        Vector2? screen = null;
        if (entity is null && args.Bool("screen", false)) screen = new Vector2(x!.Value, y!.Value);
        else if (entity is null) world = new Vector3(x!.Value, y!.Value, args.OptFloat("z") ?? Game.State.Player?.Position.Z ?? 0);
        var until = seconds is { } s ? now + (long)(s * 1000) : (long?)null;
        var (id, at) = marks.Add(new Mark(0, shape, entity, world, screen, size, color, label, until), now);
        return new JsonObject { ["id"] = id, ["screen"] = at is { } p ? new JsonObject { ["x"] = Num.F(p.X), ["y"] = Num.F(p.Y) } : null };
    }

    static Color Parse(string text)
    {
        if (text.StartsWith('#') && text.Length == 7 && int.TryParse(text[1..], NumberStyles.AllowHexSpecifier, CultureInfo.InvariantCulture, out var rgb)) return Color.FromArgb(255, Color.FromArgb(rgb));
        var named = Color.FromName(text);
        return named.IsKnownColor ? named : throw new ToolError("color must be a name like red or #RRGGBB");
    }
}
