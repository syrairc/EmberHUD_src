using System.Numerics;

namespace Aember;

public sealed record Camera(Vector3 Location, Vector3 Rotation, float Fov)
{
    public Vector2? ToScreen(Vector3 world, Vector2 viewport)
    {
        if (!float.IsFinite(Fov) || Fov <= 0 || Fov >= 180) return null;
        var (sp, cp) = MathF.SinCos(Rotation.X * MathF.PI / 180);
        var (sy, cy) = MathF.SinCos(Rotation.Y * MathF.PI / 180);
        var (sr, cr) = MathF.SinCos(Rotation.Z * MathF.PI / 180);
        var forward = new Vector3(cp * cy, cp * sy, sp);
        var right = new Vector3(sr * sp * cy - cr * sy, sr * sp * sy + cr * cy, -sr * cp);
        var up = new Vector3(-(cr * sp * cy + sr * sy), cy * sr - cr * sp * sy, cr * cp);
        var delta = world - Location;
        var depth = Vector3.Dot(delta, forward);
        if (depth < 1f) return null;
        var centre = viewport / 2;
        var focal = centre.X / MathF.Tan(Fov * MathF.PI / 360);
        var screen = new Vector2(centre.X + Vector3.Dot(delta, right) * focal / depth, centre.Y - Vector3.Dot(delta, up) * focal / depth);
        return float.IsFinite(screen.X) && float.IsFinite(screen.Y) ? screen : null;
    }
}
