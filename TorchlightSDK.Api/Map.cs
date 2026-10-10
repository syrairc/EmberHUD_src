using System.Numerics;

namespace TorchlightSDK;

public static class Map
{
    public static Vector2 Project(Vector3 world) => new(world.X, -world.Y);

    public static MapView? View { get; set; }
}

public readonly record struct MapView
{
    readonly float _cos;
    readonly float _sin;

    public MapView(Vector2 offset, float scale, float rotationDegrees = 0, float verticalScale = 1)
    {
        Offset = offset;
        Scale = scale;
        RotationDegrees = rotationDegrees;
        VerticalScale = verticalScale;
        (_sin, _cos) = MathF.SinCos(rotationDegrees * MathF.PI / 180);
    }

    public Vector2 Offset { get; init; }

    public float Scale { get; init; }

    public float RotationDegrees { get; }

    public float VerticalScale { get; }

    public Vector2 ToScreenDirection(Vector2 mapDelta) =>
        new((mapDelta.X * _cos - mapDelta.Y * _sin) * Scale, (mapDelta.X * _sin + mapDelta.Y * _cos) * Scale * VerticalScale);

    public Vector2 ToScreen(Vector2 map) => Offset + ToScreenDirection(map);

    public Vector2 ToScreen(Vector3 world) => ToScreen(Map.Project(world));

    public static MapView Follow(Vector2 center, Vector2 viewport, float scale, float rotationDegrees = 0, float verticalScale = 1)
    {
        var linear = new MapView(Vector2.Zero, scale, rotationDegrees, verticalScale);
        return linear.IsValid() ? linear with { Offset = viewport / 2 - linear.ToScreenDirection(center) } : default;
    }

    public static MapView Fit(Vector2 min, Vector2 max, Vector2 viewport, float margin, float rotationDegrees = 0, float verticalScale = 1)
    {
        var size = max - min;
        var room = viewport - new Vector2(2 * margin);
        var unit = new MapView(Vector2.Zero, 1, rotationDegrees, verticalScale);
        if (size.X <= 0 || size.Y <= 0 || room.X <= 0 || room.Y <= 0 || !unit.IsValid()) return default;

        var corners = new[] { min, new Vector2(max.X, min.Y), new Vector2(min.X, max.Y), max }.Select(unit.ToScreenDirection).ToArray();
        var low = corners.Aggregate(Vector2.Min);
        var high = corners.Aggregate(Vector2.Max);
        var scale = MathF.Min(room.X / (high.X - low.X), room.Y / (high.Y - low.Y));
        return new MapView(viewport / 2 - (low + high) / 2 * scale, scale, rotationDegrees, verticalScale);
    }

    bool IsValid() => Scale > 0 && float.IsFinite(Scale) && float.IsFinite(RotationDegrees) && VerticalScale > 0 && float.IsFinite(VerticalScale);
}
