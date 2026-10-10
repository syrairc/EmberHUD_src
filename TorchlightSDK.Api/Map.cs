using System.Numerics;

namespace TorchlightSDK;

public static class Map
{
    public static Vector2 Project(Vector3 world) => new(world.X, -world.Y);

    public static MapView? View { get; set; }
}

public readonly record struct MapView(Vector2 Offset, float Scale)
{
    public Vector2 ToScreen(Vector2 map) => Offset + map * Scale;

    public Vector2 ToScreen(Vector3 world) => ToScreen(Map.Project(world));

    public static MapView Follow(Vector2 center, Vector2 viewport, float scale) =>
        scale > 0 ? new MapView(viewport / 2 - center * scale, scale) : default;

    public static MapView Fit(Vector2 min, Vector2 max, Vector2 viewport, float margin)
    {
        var size = max - min;
        var room = viewport - new Vector2(2 * margin);
        if (size.X <= 0 || size.Y <= 0 || room.X <= 0 || room.Y <= 0) return default;

        var scale = MathF.Min(room.X / size.X, room.Y / size.Y);
        return new MapView(viewport / 2 - (min + max) / 2 * scale, scale);
    }
}
