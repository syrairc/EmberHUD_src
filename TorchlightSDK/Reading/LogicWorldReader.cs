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
        if (!memory.TryReadPtr(scene + Offsets.SceneEntityArray, out var array) ||
            !memory.TryRead(scene + Offsets.SceneEntityCount, out uint count) ||
            count > Offsets.MaxEntities ||
            !memory.TryReadArray(array, (int)count, out nint[] pointers))
            return [];

        var entities = new List<Entity>(pointers.Length);
        foreach (var pointer in pointers)
            if (TryReadEntity(pointer, out var entity))
                entities.Add(entity);
        return [.. entities];
    }

    private bool TryReadEntity(nint entity, out Entity result)
    {
        result = default;
        if (entity == 0 ||
            !memory.TryRead(entity + Offsets.EntityAlive, out byte alive) || alive == 0 ||
            !memory.TryRead(entity + Offsets.EntityHasPosition, out byte hasPosition) || hasPosition == 0 ||
            !memory.TryRead(entity + Offsets.EntityCfgId, out uint cfgId) ||
            !memory.TryReadPtr(entity + Offsets.EntityPosition, out var block) ||
            !memory.TryReadArray(block + Offsets.PositionX, 3, out double[] xyz) ||
            !xyz.All(double.IsFinite))
            return false;

        result = new Entity((int)(entity >> 4), (int)cfgId, new Vector3((float)xyz[0], (float)xyz[1], (float)xyz[2]), EntityKind.Monster, Rarity.Normal);
        return true;
    }
}
