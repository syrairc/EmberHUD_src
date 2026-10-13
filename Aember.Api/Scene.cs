using System.Numerics;

namespace Aember;

public readonly record struct SceneEntity(int Id, int CfgId, Vector3? Position, bool Alive, EntityKind? Kind, string Class)
{
    public nint Address { get; init; }
}

public static class Scene
{
    internal static Func<SceneEntity[]>? Lister { get; set; }

    public static SceneEntity[] Entities() => Lister?.Invoke() ?? [];
}
