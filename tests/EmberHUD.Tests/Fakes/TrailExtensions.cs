namespace EmberHUD.Tests;

internal static class TrailExtensions
{
    public static Vector2[] PointArray(this ExploredTrail trail) => trail.Buffer[..trail.Count];
}
