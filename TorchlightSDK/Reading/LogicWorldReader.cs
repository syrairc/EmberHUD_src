using System.Numerics;
using TorchlightSDK.Host.Memory;

namespace TorchlightSDK.Host.Reading;

internal sealed class LogicWorldReader(IReadBackend memory, nint moduleBase)
{
    public bool TryReadScene(out nint scene, out string? failure)
    {
        scene = 0;
        if (!memory.TryReadPtr(moduleBase + Offsets.LogicWorldGlobalRva, out var world))
        {
            failure = "logic world not created yet";
            return false;
        }

        if (!memory.TryReadPtr(world + Offsets.WorldScene, out scene))
        {
            failure = "no scene - not in a map";
            return false;
        }

        failure = null;
        return true;
    }

    public Entity[] ReadEntities(nint scene)
    {
        if (!memory.TryRead(scene + Offsets.SceneEntityBucketCount, out uint bucketCount) ||
            bucketCount == 0 || bucketCount > Offsets.MaxBuckets ||
            !memory.TryReadPtr(scene + Offsets.SceneEntityBuckets, out var bucketArray) ||
            !memory.TryReadArray(bucketArray, (int)bucketCount, out nint[] buckets))
            return [];

        var entities = new List<Entity>();
        var nodeCount = 0;
        foreach (var head in buckets)
        {
            var chainLength = 0;
            for (var node = head; node != 0; )
            {
                if (++chainLength > Offsets.MaxChain || ++nodeCount > Offsets.MaxEntities ||
                    !memory.TryRead(node + Offsets.NodeKey, out uint id) ||
                    !memory.TryRead(node + Offsets.NodeEntity, out nint entity) ||
                    !memory.TryRead(node + Offsets.NodeNext, out nint next))
                    return [];

                if (TryReadEntity(entity, id, out var result))
                    entities.Add(result);
                node = next;
            }
        }

        return [.. entities];
    }

    private bool TryReadEntity(nint entity, uint id, out Entity result)
    {
        result = default;
        if (entity == 0 ||
            !memory.TryRead(entity + Offsets.EntityAlive, out byte alive) || alive == 0 ||
            !memory.TryRead(entity + Offsets.EntityHasRole, out byte hasRole) || hasRole == 0 ||
            !memory.TryRead(entity + Offsets.EntityHasPosition, out byte hasPosition) || hasPosition == 0 ||
            !memory.TryRead(entity + Offsets.EntityCfgId, out uint cfgId) ||
            !memory.TryReadPtr(entity + Offsets.EntityPosition, out var block) ||
            !memory.TryReadArray(block + Offsets.PositionX, 3, out double[] xyz))
            return false;

        var position = new Vector3((float)xyz[0], (float)xyz[1], (float)xyz[2]);
        if (!float.IsFinite(position.X) || !float.IsFinite(position.Y) || !float.IsFinite(position.Z))
            return false;

        result = new Entity(unchecked((int)id), (int)cfgId, position, EntityKind.Monster, Rarity.Normal);
        return true;
    }
}
