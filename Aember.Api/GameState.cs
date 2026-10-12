using System.Numerics;

namespace Aember;

public enum EntityKind : byte { Monster, Ally, Player, Portal, Npc, Interactive, Coffin, Altar, Elevator, MapObject, Destructible }

public enum Rarity : byte { Normal, Magic, Rare, Boss }

public sealed record GameState(string Status, int AreaId, Player? Player, Entity[] Entities, Terrain? Terrain, float? MinimapRotation = null)
{
    public static readonly GameState Empty = new("starting", 0, null, [], null);

    public MapIcon[] MapIcons { get; init; } = [];

    public ExploredTrail? Explored { get; init; }

    public Camera? Camera { get; init; }
}

public sealed record Player(Vector3 Position, Vector2 Forward);

public readonly record struct Entity(int Id, int CfgId, Vector3 Position, EntityKind Kind, Rarity Rarity)
{
    public nint Address { get; init; }
}

public readonly record struct MapIcon(int Type, string Texture, Vector2 Position, float Size, float Rotation, int EntityId);

public sealed record ExploredTrail(Vector2[] Points, float Radius);
